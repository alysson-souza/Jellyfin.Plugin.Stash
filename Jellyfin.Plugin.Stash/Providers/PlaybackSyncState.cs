using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Stash.Providers
{
    internal sealed class PlaybackSyncState
    {
        internal const int Capacity = 10000;
        private readonly string path;
        private readonly StashPlaybackClient client;
        private readonly Func<string, string, bool> allowed;
        private readonly Func<string> apiKey;
        private readonly List<PlaybackRecord> records;
        private bool writeFailed;

        public PlaybackSyncState(string path, StashPlaybackClient client, Func<string, string, bool> allowed, Func<string> apiKey)
        {
            this.path = path;
            this.client = client;
            this.allowed = allowed;
            this.apiKey = apiKey;
            this.records = File.Exists(path)
                ? JsonConvert.DeserializeObject<List<PlaybackRecord>>(File.ReadAllText(path)) ?? throw new InvalidDataException("Playback journal is empty.")
                : new List<PlaybackRecord>();
            if (this.records.Count > Capacity || this.records.Any(r => string.IsNullOrEmpty(r.Session) || string.IsNullOrEmpty(r.User) || string.IsNullOrEmpty(r.Scene)))
            {
                throw new InvalidDataException("Playback journal is invalid; synchronization is disabled.");
            }
        }

        public string Status => $"Playback journal: {this.records.Count}/{Capacity} sessions, {this.records.Count(r => r.CompletedAt.HasValue && !r.Confirmed)} pending, {this.records.Count(r => r.Attempted && !r.Confirmed)} unresolved sends. Unresolved sends are never automatically resent.";
        public IEnumerable<PlaybackRecord> LocalPending => this.records.Where(r => r.CompletedAt.HasValue && !r.LocalMarked);

        public void MarkLocal(PlaybackRecord record)
        {
            record.LocalMarked = true;
            this.Save();
        }

        public PlaybackRecord Start(string session, string user, string endpoint, string scene, string item)
        {
            var existing = this.Find(session, user, endpoint, scene);
            if (existing != null)
            {
                return existing;
            }

            if (this.records.Count >= Capacity)
            {
                throw new InvalidOperationException("Playback journal is full; new sessions are blocked to preserve duplicate protection.");
            }

            var record = new PlaybackRecord { Session = session, User = user, Endpoint = endpoint, Scene = scene, Item = item };
            this.records.Add(record);
            this.Save();
            return record;
        }

        public PlaybackRecord Find(string session, string user, string endpoint, string scene)
            => this.records.FirstOrDefault(r => r.Session == session && r.User == user && r.Endpoint == endpoint && r.Scene == scene);

        public void Complete(PlaybackRecord record, DateTime now)
        {
            if (!record.CompletedAt.HasValue)
            {
                record.CompletedAt = new DateTime(now.ToUniversalTime().Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, DateTimeKind.Utc);
                this.Save();
            }
        }

        public void Stop(PlaybackRecord record)
        {
            if (!record.CompletedAt.HasValue)
            {
                this.records.Remove(record);
                this.Save();
            }
        }

        public async Task Recover(CancellationToken token)
        {
            foreach (var record in this.records.Where(r => r.CompletedAt.HasValue && !r.Confirmed).ToArray())
            {
                token.ThrowIfCancellationRequested();
                await this.SendOrReconcile(record, token).ConfigureAwait(false);
            }
        }

        public async Task SendOrReconcile(PlaybackRecord record, CancellationToken token)
        {
            if (this.writeFailed)
            {
                throw new IOException("Playback journal write failed; restart after repairing storage.");
            }
            if (!record.CompletedAt.HasValue || record.Confirmed || !this.allowed(record.User, record.Endpoint))
            {
                return;
            }

            var history = await this.client.History(record.Endpoint, this.apiKey(), record.Scene, token).ConfigureAwait(false);
            var occurrences = StashPlaybackClient.Occurrences(history, record.CompletedAt.Value);
            if (record.Attempted)
            {
                if (occurrences > record.PreviousOccurrences)
                {
                    record.Confirmed = true;
                    this.Save();
                }

                return;
            }

            if (!this.allowed(record.User, record.Endpoint))
            {
                return;
            }

            token.ThrowIfCancellationRequested();
            record.PreviousOccurrences = occurrences;
            record.Attempted = true;
            this.Save();
            if (!this.allowed(record.User, record.Endpoint))
            {
                // No request was issued. Keep this operation eligible for later recovery.
                record.Attempted = false;
                this.Save();
                return;
            }
            await this.client.AddPlay(record.Endpoint, this.apiKey(), record.Scene, record.CompletedAt.Value, token).ConfigureAwait(false);
            record.Confirmed = true;
            this.Save();
        }

        private void Save()
        {
            if (this.writeFailed)
            {
                throw new IOException("Playback journal write failed; restart after repairing storage.");
            }

            this.writeFailed = true;
            Directory.CreateDirectory(Path.GetDirectoryName(this.path));
            var temporary = this.path + ".tmp";
            using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(JsonConvert.SerializeObject(this.records));
                writer.Flush();
                stream.Flush(true);
            }

            if (File.Exists(this.path))
            {
                File.Replace(temporary, this.path, null);
            }
            else
            {
                File.Move(temporary, this.path);
            }
            this.writeFailed = false;
        }
    }

    internal sealed class PlaybackRecord
    {
        public string Session { get; set; }
        public string User { get; set; }
        public string Endpoint { get; set; }
        public string Scene { get; set; }
        public string Item { get; set; }
        public DateTime? CompletedAt { get; set; }
        public int PreviousOccurrences { get; set; }
        public bool Attempted { get; set; }
        public bool Confirmed { get; set; }
        public bool LocalMarked { get; set; }
    }
}
