using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Tasks;
using Stash.Providers;

namespace Stash.ScheduledTasks
{
    public sealed class ImportWatchedStatus : IScheduledTask
    {
        public string Key => "StashImportWatchedStatus";

        public string Name => "Import watched status from Stash";

        public string Description => "Opt-in import for the selected user only. Preserves watched status, counts and resume; skips currently playing items.";

        public string Category => Plugin.Instance.Name;

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => Array.Empty<TaskTriggerInfo>();

#if __EMBY__
        public Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
#else
        public Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
#endif
            => (PlaybackSync.Instance ?? throw new InvalidOperationException("Playback synchronization is not running."))
                .Import(progress, cancellationToken);
    }

    public sealed class ReconcilePlayback : IScheduledTask
    {
        public string Key => "StashReconcilePlayback";

        public string Name => "Reconcile Stash playback";

        public string Description => "Checks pending plays against Stash history. Never resends an ambiguous mutation. Status appears in plugin settings and server logs.";

        public string Category => Plugin.Instance.Name;

        public IEnumerable<TaskTriggerInfo> GetDefaultTriggers() => Array.Empty<TaskTriggerInfo>();

#if __EMBY__
        public async Task Execute(CancellationToken cancellationToken, IProgress<double> progress)
#else
        public async Task ExecuteAsync(IProgress<double> progress, CancellationToken cancellationToken)
#endif
        {
            var synchronization = PlaybackSync.Instance ?? throw new InvalidOperationException("Playback synchronization is not running.");
            await synchronization.Reconcile(cancellationToken).ConfigureAwait(false);
            Helpers.Logger.Info(synchronization.Status);

            progress?.Report(100);
        }
    }
}
