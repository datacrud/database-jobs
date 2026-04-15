using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DataCrud.DBOps.Core.Models;
using DataCrud.DBOps.Core.Storage;
using Serilog;

namespace DataCrud.DBOps.Core.Services
{
    public class CleanupService
    {
        private readonly IJobStorage _storage;

        public CleanupService(IJobStorage storage)
        {
            _storage = storage;
        }

        public async Task RunCleanupAsync(string backupDirectory, int retentionDays)
        {
            var history = new JobHistory
            {
                JobType = JobType.Cleanup,
                StartTime = DateTime.UtcNow,
                Status = JobStatus.Running,
                Message = $"Starting cleanup from {backupDirectory} (Retention: {retentionDays} days)"
            };

            await _storage.CreateHistoryAsync(history);

            try
            {
                if (!Directory.Exists(backupDirectory))
                {
                    history.Status = JobStatus.Completed;
                    history.EndTime = DateTime.UtcNow;
                    history.Message = "Directory does not exist. Nothing to clean.";
                    return;
                }

                var files = Directory.GetFiles(backupDirectory, "*.bak")
                    .Concat(Directory.GetFiles(backupDirectory, "*.zip"));

                int deletedCount = 0;
                foreach (var file in files)
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.CreationTime < DateTime.Now.AddDays(-retentionDays))
                    {
                        fileInfo.Delete();
                        deletedCount++;
                    }
                }

                history.Status = JobStatus.Completed;
                history.EndTime = DateTime.UtcNow;
                history.Message = $"Cleanup completed. Deleted {deletedCount} files.";
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error during cleanup in {BackupDirectory}", backupDirectory);
                history.Status = JobStatus.Failed;
                history.EndTime = DateTime.UtcNow;
                history.Message = "Error: " + ex.Message;
                history.Details = ex.ToString();
            }
            finally
            {
                await _storage.UpdateHistoryAsync(history);
            }
        }
    }
}

