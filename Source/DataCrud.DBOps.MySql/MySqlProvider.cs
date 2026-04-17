using System;
using System.IO;
using System.Threading.Tasks;
using DataCrud.DBOps.Core.Models;
using DataCrud.DBOps.Core.Providers;
using DataCrud.DBOps.Core.Storage;
using MySqlConnector;
using Dapper;

namespace DataCrud.DBOps.MySql
{
    public class MySqlProvider : IDatabaseProvider
    {
        private readonly string _connectionString;
        private readonly bool _discoverDatabases;

        public string ProviderName => "MySQL";
        public string DisplayName { get; }
        public ProviderCapabilities Capabilities => ProviderCapabilities.Backup | ProviderCapabilities.Reindex | ProviderCapabilities.Reorganize;

        public MySqlProvider(string connectionString, string displayName = null, bool discover = true)
        {
            _connectionString = connectionString;
            _discoverDatabases = discover;
            DisplayName = displayName ?? ProviderName;
        }

        public async Task<string> BackupAsync(string databaseName, string backupDirectory, System.Threading.CancellationToken cancellationToken = default)
        {
            var fileName = Path.Combine(backupDirectory, $"my_{databaseName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.sql");

            if (!Directory.Exists(backupDirectory)) Directory.CreateDirectory(backupDirectory);

            using (var conn = new MySqlConnection(_connectionString))
            {
                using (var cmd = new MySqlCommand())
                {
                    using (var mb = new MySqlBackup(cmd))
                    {
                        cmd.Connection = conn;
                        await conn.OpenAsync(cancellationToken);
                        cancellationToken.ThrowIfCancellationRequested();
                        mb.ExportToFile(fileName);
                    }
                }
            }

            return fileName;
        }

        public Task ShrinkAsync(string databaseName, System.Threading.CancellationToken cancellationToken = default)
        {
            // MySQL doesn't have a direct 'Shrink' like SQL Server. 
            // 'OPTIMIZE TABLE' is the closest equivalent for reclaiming space.
            return ReindexAsync(databaseName, cancellationToken);
        }

        public async Task ReindexAsync(string databaseName, System.Threading.CancellationToken cancellationToken = default)
        {
            using (var conn = new MySqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                
                // Get all tables in the database
                var tables = await conn.QueryAsync<string>(new CommandDefinition("SHOW TABLES", cancellationToken: cancellationToken));
                
                foreach (var table in tables)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await conn.ExecuteAsync(new CommandDefinition($"OPTIMIZE TABLE {table}", cancellationToken: cancellationToken));
                }
            }
        }

        public Task ReorganizeAsync(string databaseName, System.Threading.CancellationToken cancellationToken = default)
        {
            // In MySQL, Reorganize and Reindex are both mapped to OPTIMIZE TABLE
            return ReindexAsync(databaseName, cancellationToken);
        }

        public async Task<System.Collections.Generic.IEnumerable<string>> GetDatabasesAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            try
            {
                var builder = new MySqlConnectionStringBuilder(_connectionString);

                if (!_discoverDatabases)
                {
                    var db = !string.IsNullOrEmpty(builder.Database) ? builder.Database : "mysql";
                    return new[] { db };
                }

                builder.ConnectionTimeout = 3; // Fast fail for discovery
                using (var conn = new MySqlConnection(builder.ConnectionString))
                {
                    await conn.OpenAsync(cancellationToken);
                    var databases = await conn.QueryAsync<string>(new CommandDefinition("SHOW DATABASES", cancellationToken: cancellationToken));
                    
                    // Filter out system databases
                    var result = new System.Collections.Generic.List<string>();
                    foreach (var db in databases)
                    {
                        if (db != "information_schema" && db != "performance_schema" && db != "mysql" && db != "sys")
                        {
                            result.Add(db);
                        }
                    }
                    return result;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching MySQL databases: {ex.Message}");
                return new string[] { "MainDB (Offline)" };
            }
        }

    }
}
