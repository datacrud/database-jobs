using System;
using System.Threading.Tasks;

namespace DataCrud.DBOps.Core.Providers
{
    public interface IDatabaseProvider
    {
        string ProviderName { get; }
        string DisplayName { get; }
        ProviderCapabilities Capabilities { get; }
        
        Task<string> BackupAsync(string databaseName, string backupDirectory);
        Task ShrinkAsync(string databaseName);
        Task ReindexAsync(string databaseName);
        Task<System.Collections.Generic.IEnumerable<string>> GetDatabasesAsync(System.Threading.CancellationToken cancellationToken = default);
    }

    [Flags]
    public enum ProviderCapabilities
    {
        None = 0,
        Backup = 1,
        Shrink = 2,
        Reindex = 4,
        All = Backup | Shrink | Reindex
    }
}
