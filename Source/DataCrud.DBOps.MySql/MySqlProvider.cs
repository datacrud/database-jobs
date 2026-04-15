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
        private readonly IJobStorage _storage;
        private readonly bool _discoverDatabases;

        public string ProviderName => "MySQL";
        public string DisplayName { get; }
        public ProviderCapabilities Capabilities => ProviderCapabilities.Backup | ProviderCapabilities.Reindex;

        public MySqlProvider(string connectionString, IJobStorage storage, string displayName = null, bool discover = true)
        {
            _connectionString = connectionString;
            _storage = storage;
            _discoverDatabases = discover;
            DisplayName = displayName ?? ProviderName;
        }

        public async Task BackupAsync(string databaseName, string backupDirectory)
        {
            var fileName = Path.Combine(backupDirectory, $"{databaseName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.sql");
            var history = await CreateHistoryAsync(databaseName, JobType.Backup, $"Starting MySQL backup to {fileName}");

            try
            {
                if (!Directory.Exists(backupDirectory)) Directory.CreateDirectory(backupDirectory);

                using (var conn = new MySqlConnection(_connectionString))
                {
                    using (var cmd = new MySqlCommand())
                    {
                        using (var mb = new MySqlBackup(cmd))
                        {
                            cmd.Connection = conn;
                            await conn.OpenAsync();
                            mb.ExportToFile(fileName);
                        }
                    }
                }

                await CompleteHistoryAsync(history, "MySQL backup completed successfully.");
            }
            catch (Exception ex)
            {
                await FailHistoryAsync(history, ex);
                throw;
            }
        }

        public Task ShrinkAsync(string databaseName)
        {
            // MySQL doesn't have a direct 'Shrink' like SQL Server. 
            // 'OPTIMIZE TABLE' is the closest equivalent for reclaiming space.
            return ReindexAsync(databaseName);
        }

        public async Task ReindexAsync(string databaseName)
        {
            var history = await CreateHistoryAsync(databaseName, JobType.IndexMaintenance, "Starting OPTIMIZE TABLE operation.");

            try
            {
                using (var conn = new MySqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    
                    // Get all tables in the database
                    var tables = await conn.QueryAsync<string>("SHOW TABLES");
                    
                    foreach (var table in tables)
                    {
                        await conn.ExecuteAsync($"OPTIMIZE TABLE {table}");
                    }
                }

                await CompleteHistoryAsync(history, "OPTIMIZE TABLE (Reindex/Shrink) completed successfully.");
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
                var builder = new MySqlConnectionStringBuilder(_connectionString);

                if (!_discoverDatabases)
                {
                    var db = !string.IsNullOrEmpty(builder.Database) ? builder.Database : "mysql";
                    return new[] { db };
                }

                using (var conn = new MySqlConnection(_connectionString))
                {
                    await conn.OpenAsync();
                    var databases = await conn.QueryAsync<string>("SHOW DATABASES");
                    
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
            Console.WriteLine($"Error in MySqlProvider for {history.DatabaseName}: {ex.Message}");
            history.Status = JobStatus.Failed;
            history.EndTime = DateTime.UtcNow;
            history.Message = "Error: " + ex.Message;
            history.Details = ex.ToString();
            await _storage.UpdateHistoryAsync(history);
        }
    }
}
