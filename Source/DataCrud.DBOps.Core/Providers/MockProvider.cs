using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DataCrud.DBOps.Core.Models;

namespace DataCrud.DBOps.Core.Providers
{
    public class MockProvider : IDatabaseProvider
    {
        public string ProviderName => "MockEngine";
        public string DisplayName { get; }
        public ProviderCapabilities Capabilities => ProviderCapabilities.All;

        public MockProvider(string displayName = "Mock Database")
        {
            DisplayName = displayName;
        }

        public async Task<string> BackupAsync(string databaseName, string backupDirectory, System.Threading.CancellationToken cancellationToken = default)
        {
            var fileName = System.IO.Path.Combine(backupDirectory, $"mock_{databaseName}_{DateTime.UtcNow:yyyyMMdd}.bak");
            await Task.Delay(2000, cancellationToken); // Simulate work
            return fileName;
        }

        public async Task ShrinkAsync(string databaseName, System.Threading.CancellationToken cancellationToken = default)
        {
            await Task.Delay(1500, cancellationToken);
        }

        public async Task ReindexAsync(string databaseName, System.Threading.CancellationToken cancellationToken = default)
        {
            await Task.Delay(3000, cancellationToken);
        }

        public async Task ReorganizeAsync(string databaseName, System.Threading.CancellationToken cancellationToken = default)
        {
            await Task.Delay(1000, cancellationToken);
        }

        public Task<IEnumerable<string>> GetDatabasesAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IEnumerable<string>>(new List<string> { "Mock_Production_DB", "Mock_Staging_DB", "Mock_Archive" });
        }
    }
}
