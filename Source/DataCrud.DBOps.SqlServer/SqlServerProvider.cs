using System;
using System.IO;
using System.Threading.Tasks;
using DataCrud.DBOps.Core.Models;
using DataCrud.DBOps.Core.Providers;
using DataCrud.DBOps.Core.Storage;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using Serilog;

namespace DataCrud.DBOps.SqlServer
{
    public class SqlServerProvider : IDatabaseProvider
    {
        private readonly string _connectionString;
        private readonly IJobStorage _storage;

        public string ProviderName => "SQL Server";
        public ProviderCapabilities Capabilities => ProviderCapabilities.All;

        public SqlServerProvider(string connectionString, IJobStorage storage)
        {
            _connectionString = connectionString;
            _storage = storage;
        }

        public async Task BackupAsync(string databaseName, string backupDirectory)
        {
            var fileName = Path.Combine(backupDirectory, $"{databaseName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.bak");
            var history = await CreateHistoryAsync(databaseName, JobType.Backup, $"Starting backup to {fileName}");

            try
            {
                if (!Directory.Exists(backupDirectory)) Directory.CreateDirectory(backupDirectory);

                var builder = new SqlConnectionStringBuilder(_connectionString);
                var server = ConnectToServer(builder);

                var backup = new Backup
                {
                    Action = BackupActionType.Database,
                    Database = databaseName
                };
                backup.Devices.AddDevice(fileName, DeviceType.File);
                backup.Initialize = true;
                backup.SqlBackup(server);

                await CompleteHistoryAsync(history, "Backup completed successfully.");
            }
            catch (Exception ex)
            {
                await FailHistoryAsync(history, ex);
                throw;
            }
        }

        public async Task ShrinkAsync(string databaseName)
        {
            var history = await CreateHistoryAsync(databaseName, JobType.Shrink, "Starting database shrink.");

            try
            {
                var builder = new SqlConnectionStringBuilder(_connectionString);
                var server = ConnectToServer(builder);
                var db = server.Databases[databaseName];

                db.Shrink(10, ShrinkMethod.Default);

                await CompleteHistoryAsync(history, "Shrink completed successfully.");
            }
            catch (Exception ex)
            {
                await FailHistoryAsync(history, ex);
                throw;
            }
        }

        public async Task ReindexAsync(string databaseName)
        {
            var history = await CreateHistoryAsync(databaseName, JobType.IndexMaintenance, "Starting index maintenance.");

            try
            {
                var builder = new SqlConnectionStringBuilder(_connectionString);
                var server = ConnectToServer(builder);
                var db = server.Databases[databaseName];

                foreach (Table table in db.Tables)
                {
                    if (table.IsSystemObject) continue;
                    table.RebuildIndexes(0);
                }

                await CompleteHistoryAsync(history, "Index maintenance completed successfully.");
            }
            catch (Exception ex)
            {
                await FailHistoryAsync(history, ex);
                throw;
            }
        }

        private Server ConnectToServer(SqlConnectionStringBuilder builder)
        {
            var server = new Server(builder.DataSource);
            if (builder.IntegratedSecurity)
            {
                server.ConnectionContext.LoginSecure = true;
            }
            else
            {
                server.ConnectionContext.LoginSecure = false;
                server.ConnectionContext.Login = builder.UserID;
                server.ConnectionContext.Password = builder.Password;
            }
            server.ConnectionContext.Connect();
            return server;
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
            Console.WriteLine($"Error in {ProviderName} for {history.DatabaseName}: {ex.Message}");
            history.Status = JobStatus.Failed;
            history.EndTime = DateTime.UtcNow;
            history.Message = "Error: " + ex.Message;
            history.Details = ex.ToString();
            await _storage.UpdateHistoryAsync(history);
        }
    }
}
