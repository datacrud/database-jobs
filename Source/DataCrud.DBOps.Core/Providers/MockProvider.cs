using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DataCrud.DBOps.Core.Models;
using DataCrud.DBOps.Core.Storage;

namespace DataCrud.DBOps.Core.Providers
{
    public class MockProvider : IDatabaseProvider
    {
        private readonly IJobStorage _storage;
        public string ProviderName => "MockEngine";
        public string DisplayName { get; }
        public ProviderCapabilities Capabilities => ProviderCapabilities.All;

        public MockProvider(IJobStorage storage, string displayName = "Mock Database")
        {
            _storage = storage;
            DisplayName = displayName;
        }

        public async Task<string> BackupAsync(string databaseName, string backupDirectory)
        {
            var fileName = System.IO.Path.Combine(backupDirectory, $"mock_{databaseName}_{DateTime.UtcNow:yyyyMMdd}.bak");
            var history = await CreateHistoryAsync(databaseName, JobType.Backup, "Starting Mock Backup...");
            await Task.Delay(2000); // Simulate work
            await CompleteHistoryAsync(history, $"Mock Backup of {databaseName} completed to {fileName}.");
            return fileName;
        }

        public async Task ShrinkAsync(string databaseName)
        {
            var history = await CreateHistoryAsync(databaseName, JobType.Shrink, "Starting Mock Shrink...");
            await Task.Delay(1500);
            await CompleteHistoryAsync(history, $"Mock Shrink of {databaseName} completed successfully.");
        }

        public async Task ReindexAsync(string databaseName)
        {
            var history = await CreateHistoryAsync(databaseName, JobType.IndexMaintenance, "Starting Mock Reindex...");
            await Task.Delay(3000);
            await CompleteHistoryAsync(history, $"Mock Reindex of {databaseName} completed successfully.");
        }

        public Task<IEnumerable<string>> GetDatabasesAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IEnumerable<string>>(new List<string> { "Mock_Production_DB", "Mock_Staging_DB", "Mock_Archive" });
        }

        private async Task<JobHistory> CreateHistoryAsync(string dbName, JobType type, string message)
        {
            var history = new JobHistory
            {
                DatabaseName = dbName,
                JobType = type,
                StartTime = DateTime.UtcNow,
                Status = JobStatus.Running,
                Message = message
            };
            history.Id = await _storage.CreateHistoryAsync(history);
            return history;
        }

        private async Task CompleteHistoryAsync(JobHistory history, string message)
        {
            history.Status = JobStatus.Completed;
            history.EndTime = DateTime.UtcNow;
            history.Message = message;
            await _storage.UpdateHistoryAsync(history);
        }
    }
}
