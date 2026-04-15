using System;
using System.IO;
using System.Threading.Tasks;
using CliWrap;
using DataCrud.DBOps.Core.Models;
using DataCrud.DBOps.Core.Providers;
using DataCrud.DBOps.Core.Storage;
using Oracle.ManagedDataAccess.Client;
using Dapper;
using Serilog;

namespace DataCrud.DBOps.Oracle
{
    public class OracleProvider : IDatabaseProvider
    {
        private readonly string _connectionString;
        private readonly IJobStorage _storage;
        private readonly string _expdpPath;
        private readonly bool _discoverDatabases;

        public string ProviderName => "Oracle";
        public string DisplayName { get; }
        public ProviderCapabilities Capabilities => ProviderCapabilities.All;

        public OracleProvider(string connectionString, IJobStorage storage, string displayName = null, bool discover = true, string expdpPath = "expdp")
        {
            _connectionString = connectionString;
            _storage = storage;
            _expdpPath = expdpPath;
            _discoverDatabases = discover;
            DisplayName = displayName ?? ProviderName;
        }

        public async Task BackupAsync(string databaseName, string backupDirectory)
        {
            var fileName = $"{databaseName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.dmp";
            var history = await CreateHistoryAsync(databaseName, JobType.Backup, "Starting Oracle Data Pump export (expdp).");

            try
            {
                var builder = new OracleConnectionStringBuilder(_connectionString);
                
                // expdp usually requires a DIRECTORY object defined in Oracle.
                // For a 'lite' implementation, we assume the environment is set up or use simple export.
                var result = await Cli.Wrap(_expdpPath)
                    .WithArguments(args => args
                        .Add($"{builder.UserID}/{builder.Password}@{builder.DataSource}")
                        .Add($"DUMPFILE={fileName}")
                        .Add($"DIRECTORY=DATA_PUMP_DIR")
                        .Add($"SCHEMAS={builder.UserID}")
                    )
                    .ExecuteAsync();

                await CompleteHistoryAsync(history, $"Oracle export completed successfully. DUMPFILE: {fileName}");
            }
            catch (Exception ex)
            {
                await FailHistoryAsync(history, ex);
                throw;
            }
        }

        public async Task ShrinkAsync(string databaseName)
        {
            var history = await CreateHistoryAsync(databaseName, JobType.Shrink, "Starting segment shrink operation.");

            try
            {
                using (var conn = new OracleConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    
                    // Enable row movement and shrink space for all tables in the schema
                    var tables = await conn.QueryAsync<string>("SELECT table_name FROM user_tables");
                    
                    foreach (var table in tables)
                    {
                        await conn.ExecuteAsync($"ALTER TABLE {table} ENABLE ROW MOVEMENT");
                        await conn.ExecuteAsync($"ALTER TABLE {table} SHRINK SPACE");
                    }
                }

                await CompleteHistoryAsync(history, "Oracle segments shrunk successfully.");
            }
            catch (Exception ex)
            {
                await FailHistoryAsync(history, ex);
                throw;
            }
        }

        public async Task ReindexAsync(string databaseName)
        {
            var history = await CreateHistoryAsync(databaseName, JobType.IndexMaintenance, "Starting index rebuild operation.");

            try
            {
                using (var conn = new OracleConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    
                    var indexes = await conn.QueryAsync<string>("SELECT index_name FROM user_indexes WHERE index_type = 'NORMAL'");
                    
                    foreach (var index in indexes)
                    {
                        await conn.ExecuteAsync($"ALTER INDEX {index} REBUILD ONLINE");
                    }
                }

                await CompleteHistoryAsync(history, "Oracle indexes rebuilt successfully.");
            }
            catch (Exception ex)
            {
                await FailHistoryAsync(history, ex);
                throw;
            }
        }

        public async Task<System.Collections.Generic.IEnumerable<string>> GetDatabasesAsync()
        {
            try
            {
                var builder = new OracleConnectionStringBuilder(_connectionString);

                if (!_discoverDatabases)
                {
                    return new[] { builder.UserID };
                }

                using (var conn = new OracleConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    // In Oracle, 'database' is usually the instance. 
                    var dbName = await conn.QueryFirstOrDefaultAsync<string>("SELECT name FROM v$database");
                    return new string[] { dbName ?? builder.UserID };
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error fetching Oracle databases");
                return new string[] { new OracleConnectionStringBuilder(_connectionString).UserID };
            }
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

        private async Task FailHistoryAsync(JobHistory history, Exception ex)
        {
            Log.Error(ex, "Error in OracleProvider for {DatabaseName}", history.DatabaseName);
            history.Status = JobStatus.Failed;
            history.EndTime = DateTime.UtcNow;
            history.Message = "Error: " + ex.Message;
            history.Details = ex.ToString();
            await _storage.UpdateHistoryAsync(history);
        }
    }
}
