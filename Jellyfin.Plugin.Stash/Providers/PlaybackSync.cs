using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Entities;
using Stash.Helpers;
#if __EMBY__
using MediaBrowser.Model.Querying;
#else
using Jellyfin.Data.Enums;
using Jellyfin.Database.Implementations.Entities;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MediaBrowser.Controller;
#endif

namespace Stash.Providers
{
#if __EMBY__
    public sealed class PlaybackSync : IServerEntryPoint
#else
    public sealed class PlaybackSync : IHostedService, IDisposable
#endif
    {
        private readonly ISessionManager sessions;
        private readonly IUserManager users;
        private readonly IUserDataManager userData;
        private readonly ILibraryManager library;
        private readonly string journalPath;
        private readonly HttpClient http;
        private readonly Func<Configuration.PluginConfiguration> configuration;
        private readonly string providerName;
        private readonly CancellationTokenSource shutdown = new CancellationTokenSource();
        private readonly object queueLock = new object();
        private Task queue = Task.CompletedTask;
        private PlaybackSyncState state;
        private StashPlaybackClient client;
        private bool disposed;

        public PlaybackSync(ISessionManager sessions, IUserManager users, IUserDataManager userData, ILibraryManager library, IApplicationPaths paths)
            : this(sessions, users, userData, library, Path.Combine(paths.DataPath, "stash-playback-sessions.json"),
                new HttpClient { Timeout = TimeSpan.FromSeconds(30) }, () => Plugin.Instance.Configuration, Plugin.Instance.Name)
        {
        }

        internal PlaybackSync(ISessionManager sessions, IUserManager users, IUserDataManager userData, ILibraryManager library,
            string journalPath, HttpClient http, Func<Configuration.PluginConfiguration> configuration, string providerName)
        {
            this.sessions = sessions;
            this.users = users;
            this.userData = userData;
            this.library = library;
            this.journalPath = journalPath;
            this.http = http;
            this.configuration = configuration;
            this.providerName = providerName;
        }

        public static PlaybackSync Instance { get; private set; }

        public string Status { get; private set; } = "Playback synchronization has not started.";

#if __EMBY__
        public void Run() => this.Start();
#else
        public Task StartAsync(CancellationToken cancellationToken)
        {
            this.Start();
            return Task.CompletedTask;
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            this.Unsubscribe();
            this.shutdown.Cancel();
            await this.queue.ConfigureAwait(false);
        }
#endif

        public IEnumerable<User> GetUsers()
        {
#if __EMBY__
            return this.users.GetUserList(new UserQuery());
#else
            return this.users.GetUsers();
#endif
        }

        private void Start()
        {
            Instance = this;
            try
            {
                this.client = new StashPlaybackClient(this.http);
                this.state = new PlaybackSyncState(this.journalPath, this.client, this.Allowed, () => this.configuration().StashAPIKey);
                this.Status = this.state.Status;
                this.sessions.PlaybackStart += this.OnStart;
                this.sessions.PlaybackProgress += this.OnProgress;
                this.sessions.PlaybackStopped += this.OnStop;
                this.Enqueue(() => this.Recover(this.shutdown.Token));
            }
            catch (Exception)
            {
                this.Status = "Playback journal could not be loaded. Synchronization is disabled; inspect the journal before restarting.";
                Logger.Error(this.Status);
            }
        }

        private User SelectedUser()
        {
            var id = this.configuration().PlaybackUserId;
            return Guid.TryParse(id, out var parsed) && parsed != Guid.Empty ? this.users.GetUserById(parsed) : null;
        }

        private bool Allowed(string user, string endpoint)
        {
            var config = this.configuration();
            return config.RecordCompletedPlayback && this.SelectedUser()?.Id.ToString("N") == user && Endpoint() == endpoint;
        }

        private string Endpoint() => (this.configuration().StashEndpoint ?? string.Empty).TrimEnd('/');

        private bool Linked(BaseItem item, out string scene)
        {
            scene = null;
            return (item is Movie || item is Episode || item?.GetType() == typeof(Video))
                && item.ProviderIds.TryGetValue(this.providerName, out scene) && !string.IsNullOrWhiteSpace(scene);
        }

        private void OnStart(object sender, PlaybackProgressEventArgs e) => this.Capture(e, true, false);
        private void OnProgress(object sender, PlaybackProgressEventArgs e) => this.Capture(e, false, false);
        private void OnStop(object sender, PlaybackStopEventArgs e) => this.Capture(e, false, true);

        private void Capture(PlaybackProgressEventArgs e, bool start, bool stop)
        {
            var user = this.SelectedUser();
            var endpoint = Endpoint();
            if (user == null || !this.Allowed(user.Id.ToString("N"), endpoint)
                || e.Users == null || e.Users.Count != 1 || e.Users[0].Id != user.Id
                || !Linked(e.Item, out var scene) || string.IsNullOrEmpty(e.PlaySessionId))
            {
                return;
            }

            var id = user.Id.ToString("N");
            var item = e.Item;
            var session = e.PlaySessionId;
            var position = e.PlaybackPositionTicks;
            var duration = item.RunTimeTicks;
            var now = DateTime.UtcNow;
            this.Enqueue(async () =>
            {
                if (!this.Allowed(id, endpoint))
                {
                    return;
                }

                var record = start
                    ? this.state.Start(session, id, endpoint, scene, item.Id.ToString("N"))
                    : this.state.Find(session, id, endpoint, scene);
                if (record == null)
                {
                    return;
                }

                var threshold = this.configuration().PlaybackCompletionPercent;
                if (!record.CompletedAt.HasValue && duration > 0 && position.HasValue && threshold > 0 && threshold <= 100
                    && (decimal)position.Value * 100 >= (decimal)duration.Value * threshold)
                {
                    this.state.Complete(record, now);
                    if (!this.Allowed(id, endpoint))
                    {
                        return;
                    }
                    this.MarkPlayed(user, item, this.shutdown.Token);
                    if (!record.LocalMarked)
                    {
                        this.state.MarkLocal(record);
                    }
                    await this.state.SendOrReconcile(record, this.shutdown.Token).ConfigureAwait(false);
                }

                if (stop)
                {
                    this.state.Stop(record);
                }
            });
        }

        private void MarkPlayed(User user, BaseItem item, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            var data = this.userData.GetUserData(user, item);
            if (data == null || data.Played)
            {
                return;
            }

            // Mutate only Played on the host's current data. Counts and resume belong to the host.
            data.Played = true;
            try
            {
                this.userData.SaveUserData(user, item, data, UserDataSaveReason.Import, token);
            }
            catch
            {
                // A failed save must not leave our unsaved flag in the host's cached object.
                data.Played = false;
                throw;
            }
        }

        public Task Import(IProgress<double> progress, CancellationToken token)
        {
            return this.Enqueue(async () =>
            {
                var user = this.SelectedUser();
                if (!this.configuration().ImportWatchedStatus || user == null)
                {
                    return;
                }

                var endpoint = Endpoint();
                var query = new InternalItemsQuery
                {
#if __EMBY__
                    IncludeItemTypes = new[] { nameof(Movie), nameof(Video), nameof(Episode) },
#else
                    IncludeItemTypes = new[] { BaseItemKind.Movie, BaseItemKind.Video, BaseItemKind.Episode },
#endif
                    Recursive = true,
                };
                var items = this.library.GetItemList(query).ToArray();
                var index = 0;
                foreach (var item in items)
                {
                    token.ThrowIfCancellationRequested();
                    if (!this.configuration().ImportWatchedStatus || this.SelectedUser()?.Id != user.Id || Endpoint() != endpoint)
                    {
                        return;
                    }

                    if (Linked(item, out var scene))
                    {
                        var history = await this.client.History(endpoint, this.configuration().StashAPIKey, scene, token).ConfigureAwait(false);
                        // A network request can overlap playback or a settings change. Recheck immediately before saving.
                        if (history.Value<int>("play_count") > 0 && this.configuration().ImportWatchedStatus
                            && this.SelectedUser()?.Id == user.Id && Endpoint() == endpoint && !this.IsPlaying(user, item))
                        {
                            this.MarkPlayed(user, item, token);
                        }
                    }

                    progress?.Report(++index * 100.0 / items.Length);
                }

                progress?.Report(100);
            });
        }

        private bool IsPlaying(User user, BaseItem item)
        {
#if __EMBY__
            return this.sessions.Sessions.Any(s => Guid.TryParse(s.UserId, out var id) && id == user.Id && s.NowPlayingItem?.Id == item.Id.ToString("N"));
#else
            return this.sessions.Sessions.Any(s => s.UserId == user.Id && s.NowPlayingItem?.Id == item.Id);
#endif
        }

        public Task Reconcile(CancellationToken token) => this.Enqueue(() => this.Recover(token));

        private async Task Recover(CancellationToken token)
        {
            foreach (var record in this.state.LocalPending.ToArray())
            {
                var user = this.SelectedUser();
                if (user?.Id.ToString("N") == record.User && this.Allowed(record.User, record.Endpoint) && Guid.TryParse(record.Item, out var id))
                {
                    var item = this.library.GetItemById(id);
                    if (Linked(item, out var scene) && scene == record.Scene && this.Allowed(record.User, record.Endpoint))
                    {
                        this.MarkPlayed(user, item, token);
                        this.state.MarkLocal(record);
                    }
                }
            }

            await this.state.Recover(token).ConfigureAwait(false);
        }

        private Task Enqueue(Func<Task> action)
        {
            lock (this.queueLock)
            {
                if (this.disposed || this.state == null || this.shutdown.IsCancellationRequested)
                {
                    return Task.CompletedTask;
                }

                this.queue = this.queue.ContinueWith(async previous =>
                {
                    try
                    {
                        this.shutdown.Token.ThrowIfCancellationRequested();
                        await action().ConfigureAwait(false);
                        this.Status = this.state.Status;
                    }
                    catch (OperationCanceledException)
                    {
                        this.Status = this.state.Status;
                    }
                    catch (Exception)
                    {
                        this.Status = this.state.Status + " Last operation failed. Run Reconcile Stash playback to inspect pending history.";
                        Logger.Error(this.Status);
                    }
                }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default).Unwrap();
                return this.queue;
            }
        }

        private void Unsubscribe()
        {
            this.sessions.PlaybackStart -= this.OnStart;
            this.sessions.PlaybackProgress -= this.OnProgress;
            this.sessions.PlaybackStopped -= this.OnStop;
        }

        public void Dispose()
        {
            this.Unsubscribe();
            lock (this.queueLock)
            {
                this.disposed = true;
                this.shutdown.Cancel();
            }

            this.queue.GetAwaiter().GetResult();
            this.http.Dispose();
            this.shutdown.Dispose();
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }

#if !__EMBY__
    public sealed class PlaybackSyncRegistration : IPluginServiceRegistrator
    {
        public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
            => serviceCollection.AddHostedService<PlaybackSync>();
    }
#endif
}
