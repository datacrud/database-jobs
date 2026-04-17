using System;
using System.Threading.Tasks;

namespace DataCrud.DBOps.Core.Providers
{
    public interface IDatabaseProvider
    {
        string ProviderName { get; }
        string DisplayName { get; }
        ProviderCapabilities Capabilities { get; }
        
        Task<string> BackupAsync(string databaseName, string backupDirectory, System.Threading.CancellationToken cancellationToken = default);
        Task ShrinkAsync(string databaseName, System.Threading.CancellationToken cancellationToken = default);
        Task ReindexAsync(string databaseName, System.Threading.CancellationToken cancellationToken = default);
        Task ReorganizeAsync(string databaseName, System.Threading.CancellationToken cancellationToken = default);
        Task<System.Collections.Generic.IEnumerable<string>> GetDatabasesAsync(System.Threading.CancellationToken cancellationToken = default);
    }

    [Flags]
    public enum ProviderCapabilities
    {
        None = 0,
        Backup = 1,
        Shrink = 2,
        Reindex = 4,
        Reorganize = 8,
        All = Backup | Shrink | Reindex | Reorganize
    }
}
