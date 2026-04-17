using System;
using System.IO;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using CliWrap;
using DataCrud.DBOps.Core.Models;
using DataCrud.DBOps.Core.Providers;
using DataCrud.DBOps.Core.Storage;
using DataCrud.DBOps.Core.Providers;
using Oracle.ManagedDataAccess.Client;
using Dapper;
using Serilog;

namespace DataCrud.DBOps.Oracle
{
    public class OracleProvider : IDatabaseProvider
    {
        private readonly string _connectionString;
        private readonly string _expdpPath;
        private readonly bool _discoverDatabases;

        public string ProviderName => "Oracle";
        public string DisplayName { get; }
        public ProviderCapabilities Capabilities => ProviderCapabilities.All;

        public OracleProvider(string connectionString, string displayName = null, bool discover = true, string expdpPath = "expdp")
        {
            _connectionString = connectionString;
            _expdpPath = expdpPath;
            _discoverDatabases = discover;
            DisplayName = displayName ?? ProviderName;
        }

        public async Task<string> BackupAsync(string databaseName, string backupDirectory, System.Threading.CancellationToken cancellationToken = default)
        {
            var fileName = $"ora_{databaseName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.dmp";
            var fullPath = Path.Combine(backupDirectory, fileName);

            var builder = new OracleConnectionStringBuilder(_connectionString);
            
            // expdp usually requires a DIRECTORY object defined in Oracle.
            // We assume DATA_PUMP_DIR is configured on the server.
            var result = await Cli.Wrap(_expdpPath)
                .WithArguments(args => args
                    .Add($"{builder.UserID}/{builder.Password}@{builder.DataSource}")
                    .Add($"DUMPFILE={fileName}")
                    .Add($"DIRECTORY=DATA_PUMP_DIR")
                    .Add($"SCHEMAS={builder.UserID}")
                )
                .ExecuteAsync(cancellationToken);

            return fullPath; // Return the path where we expect it to be
        }

        public async Task ShrinkAsync(string databaseName, System.Threading.CancellationToken cancellationToken = default)
        {
            using (var conn = new OracleConnection(_connectionString))
            {
                await conn.OpenAsync();
                
                // Enable row movement and shrink space for all tables in the schema
                var tables = await conn.QueryAsync<string>(new CommandDefinition("SELECT table_name FROM user_tables", cancellationToken: cancellationToken));
                
                foreach (var table in tables)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await conn.ExecuteAsync(new CommandDefinition($"ALTER TABLE {table} ENABLE ROW MOVEMENT", cancellationToken: cancellationToken));
                    await conn.ExecuteAsync(new CommandDefinition($"ALTER TABLE {table} SHRINK SPACE", cancellationToken: cancellationToken));
                }
            }
        }

        public async Task ReindexAsync(string databaseName, System.Threading.CancellationToken cancellationToken = default)
        {
            using (var conn = new OracleConnection(_connectionString))
            {
                await conn.OpenAsync();
                
                var indexes = await conn.QueryAsync<string>(new CommandDefinition("SELECT index_name FROM user_indexes WHERE index_type = 'NORMAL'", cancellationToken: cancellationToken));
                
                foreach (var index in indexes)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await conn.ExecuteAsync(new CommandDefinition($"ALTER INDEX {index} REBUILD ONLINE", cancellationToken: cancellationToken));
                }
            }
        }

        public async Task ReorganizeAsync(string databaseName, System.Threading.CancellationToken cancellationToken = default)
        {
            using (var conn = new OracleConnection(_connectionString))
            {
                await conn.OpenAsync();
                
                var indexes = await conn.QueryAsync<string>(new CommandDefinition("SELECT index_name FROM user_indexes WHERE index_type = 'NORMAL'", cancellationToken: cancellationToken));
                
                foreach (var index in indexes)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    // COALESCE is the Oracle equivalent of reorganizing (merging leaf blocks)
                    await conn.ExecuteAsync(new CommandDefinition($"ALTER INDEX {index} COALESCE", cancellationToken: cancellationToken));
                }
            }
        }

        public async Task<System.Collections.Generic.IEnumerable<string>> GetDatabasesAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            try
            {
                var builder = new OracleConnectionStringBuilder(_connectionString);

                if (!_discoverDatabases)
                {
                    return new[] { builder.UserID };
                }

                // OracleConnection doesn't have a simple Timeout property on ConnectionBuilder in all versions,
                // but we can pass a 5-second timeout in the string or use the connection attempt with cancellation.
                if (_connectionString.IndexOf("Connection Timeout", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    // Add a default small timeout for discovery
                    var timeoutStr = _connectionString.Contains("?") ? "&Connection Timeout=5" : ";Connection Timeout=5";
                    // Note: Oracle CS format varies, for discovery we'll just try to open with cancellation.
                }

                using (var conn = new OracleConnection(_connectionString))
                {
                    await conn.OpenAsync(cancellationToken);
                    // In Oracle, 'database' is usually the instance. 
                    var dbName = await conn.QueryFirstOrDefaultAsync<string>(new CommandDefinition("SELECT name FROM v$database", cancellationToken: cancellationToken));
                    return new string[] { dbName ?? builder.UserID };
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error fetching Oracle databases");
                return new string[] { new OracleConnectionStringBuilder(_connectionString).UserID };
            }
        }

    }
}
