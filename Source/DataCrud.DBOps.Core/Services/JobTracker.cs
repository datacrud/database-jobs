using System;
using System.Collections.Concurrent;
using System.Threading;

namespace DataCrud.DBOps.Core.Services
{
    /// <summary>
    /// Singleton service to track and manage cancellation tokens for active jobs.
    /// </summary>
    public class JobTracker
    {
        private static readonly Lazy<JobTracker> _instance = new Lazy<JobTracker>(() => new JobTracker());
        public static JobTracker Instance => _instance.Value;

        private readonly ConcurrentDictionary<int, CancellationTokenSource> _activeJobs = new ConcurrentDictionary<int, CancellationTokenSource>();

        private JobTracker() { }

        public CancellationToken Register(int historyId)
        {
            var cts = new CancellationTokenSource();
            _activeJobs.TryAdd(historyId, cts);
            return cts.Token;
        }

        public bool Cancel(int historyId)
        {
            if (_activeJobs.TryGetValue(historyId, out var cts))
            {
                cts.Cancel();
                return true;
            }
            return false;
        }

        public void Unregister(int historyId)
        {
            if (_activeJobs.TryRemove(historyId, out var cts))
            {
                cts.Dispose();
            }
        }
    }
}
