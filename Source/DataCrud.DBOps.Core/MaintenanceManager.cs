using System;
using System.Threading.Tasks;
using DataCrud.DBOps.Core.Models;
using DataCrud.DBOps.Core.Providers;
using DataCrud.DBOps.Core.Services;
using DataCrud.DBOps.Core.Storage;
using Serilog;

namespace DataCrud.DBOps.Core
{
    public class MaintenanceManager
    {
        private readonly IJobStorage _storage;
        private readonly IDatabaseProvider _provider;

        public MaintenanceManager(IJobStorage storage, IDatabaseProvider provider)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        public async Task RunAsync(string databaseName, bool backup, bool shrink, bool index, bool cleanup, string backupDir = null, int retentionDays = 7)
        {
            try
            {
                // Ensure storage is initialized
                await _storage.InitializeAsync(null);

                if (shrink && _provider.Capabilities.HasFlag(ProviderCapabilities.Shrink))
                {
                    await _provider.ShrinkAsync(databaseName);
                }

                if (index && _provider.Capabilities.HasFlag(ProviderCapabilities.Reindex))
                {
                    await _provider.ReindexAsync(databaseName);
                }

                if (backup && _provider.Capabilities.HasFlag(ProviderCapabilities.Backup))
                {
                    if (string.IsNullOrEmpty(backupDir))
                    {
                        throw new ArgumentException("Backup directory must be specified for backup jobs.", nameof(backupDir));
                    }
                    await _provider.BackupAsync(databaseName, backupDir);
                }

                if (cleanup && !string.IsNullOrEmpty(backupDir))
                {
                    var cleanupService = new CleanupService(_storage);
                    await cleanupService.RunCleanupAsync(backupDir, retentionDays);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Maintenance run failed for database {DatabaseName} using provider {ProviderName}", databaseName, _provider.ProviderName);
                throw;
            }
        }
    }
}
