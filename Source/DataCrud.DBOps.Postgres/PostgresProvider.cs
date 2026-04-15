using System;
using System.IO;
using System.Threading.Tasks;
using CliWrap;
using DataCrud.DBOps.Core.Models;
using DataCrud.DBOps.Core.Providers;
using DataCrud.DBOps.Core.Storage;
using Npgsql;
using Dapper;
using Serilog;

namespace DataCrud.DBOps.Postgres
{
    public class PostgresProvider : IDatabaseProvider
    {
        private readonly string _connectionString;
        private readonly IJobStorage _storage;
        private readonly string _pgDumpPath;

        public string ProviderName => "Postgres";
        public ProviderCapabilities Capabilities => ProviderCapabilities.All;

        public PostgresProvider(string connectionString, IJobStorage storage, string pgDumpPath = "pg_dump")
        {
            _connectionString = connectionString;
            _storage = storage;
            _pgDumpPath = pgDumpPath;
        }

        public async Task BackupAsync(string databaseName, string backupDirectory)
        {
            var fileName = Path.Combine(backupDirectory, $"{databaseName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.sql");
            var history = await CreateHistoryAsync(databaseName, JobType.Backup, $"Starting Postgres backup to {fileName}");

            try
            {
                if (!Directory.Exists(backupDirectory)) Directory.CreateDirectory(backupDirectory);

                var builder = new NpgsqlConnectionStringBuilder(_connectionString);
                
                // Use CliWrap to run pg_dump
                // Note: pg_dump requires PGPASSWORD env var or .pgpass file if no password provided in command
                var result = await Cli.Wrap(_pgDumpPath)
                    .WithArguments(args => args
                        .Add("--host").Add(builder.Host)
                        .Add("--username").Add(builder.Username)
                        .Add("--dbname").Add(databaseName)
                        .Add("--file").Add(fileName)
                    )
                    .WithEnvironmentVariables(env => env
                        .Set("PGPASSWORD", builder.Password)
                    )
                    .ExecuteAsync();

                await CompleteHistoryAsync(history, "Postgres backup completed successfully.");
            }
            catch (Exception ex)
            {
                await FailHistoryAsync(history, ex);
                throw;
            }
        }

        public async Task ShrinkAsync(string databaseName)
        {
            var history = await CreateHistoryAsync(databaseName, JobType.Shrink, "Starting VACUUM (Shrink) operation.");

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    // Full Vacuum reclaims space but locks the table
                    await conn.ExecuteAsync("VACUUM FULL;");
                }

                await CompleteHistoryAsync(history, "VACUUM completed successfully.");
            }
            catch (Exception ex)
            {
                await FailHistoryAsync(history, ex);
                throw;
            }
        }

        public async Task ReindexAsync(string databaseName)
        {
            var history = await CreateHistoryAsync(databaseName, JobType.IndexMaintenance, "Starting REINDEX operation.");

            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    await conn.ExecuteAsync($"REINDEX DATABASE {databaseName};");
                }

                await CompleteHistoryAsync(history, "REINDEX completed successfully.");
            }
            catch (Exception ex)
            {
                await FailHistoryAsync(history, ex);
                throw;
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
            Log.Error(ex, "Error in PostgresProvider for {DatabaseName}", history.DatabaseName);
            history.Status = JobStatus.Failed;
            history.EndTime = DateTime.UtcNow;
            history.Message = "Error: " + ex.Message;
            history.Details = ex.ToString();
            await _storage.UpdateHistoryAsync(history);
        }
    }
}
