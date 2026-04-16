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
        private readonly bool _discoverDatabases;

        public string ProviderName => "SQL Server";
        public string DisplayName { get; }
        public ProviderCapabilities Capabilities => ProviderCapabilities.All;

        public SqlServerProvider(string connectionString, IJobStorage storage, string displayName = null, bool discover = true)
        {
            _connectionString = connectionString;
            _storage = storage;
            _discoverDatabases = discover;
            DisplayName = displayName ?? ProviderName;
        }

        public async Task<string> BackupAsync(string databaseName, string backupDirectory)
        {
            var isPaaS = await IsPaaSAsync();
            var extension = isPaaS ? "bacpac" : "bak";
            var fileName = Path.Combine(backupDirectory, $"sql_{databaseName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.{extension}");
            
            var history = await CreateHistoryAsync(databaseName, JobType.Backup, $"Starting {(isPaaS ? "BACPAC export" : "backup")} to {fileName}");

            try
            {
                if (!Directory.Exists(backupDirectory)) Directory.CreateDirectory(backupDirectory);

                var builder = new SqlConnectionStringBuilder(_connectionString);
                
                if (isPaaS)
                {
                    await ExportBacpacAsync(databaseName, fileName);
                }
                else
                {
                    var server = ConnectToServer(builder);
                    var backup = new Backup
                    {
                        Action = BackupActionType.Database,
                        Database = databaseName
                    };
                    backup.Devices.AddDevice(fileName, DeviceType.File);
                    backup.Initialize = true;
                    backup.SqlBackup(server);
                }

                await CompleteHistoryAsync(history, "Backup completed successfully.");
                return fileName;
            }
            catch (Exception ex)
            {
                await FailHistoryAsync(history, ex);
                throw;
            }
        }

        private async Task<bool> IsPaaSAsync()
        {
            using (var conn = new SqlConnection(_connectionString))
            {
                await conn.OpenAsync();
                using (var cmd = new SqlCommand("SELECT CAST(SERVERPROPERTY('EngineEdition') AS INT)", conn))
                {
                    var edition = (int)await cmd.ExecuteScalarAsync();
                    return edition == 5 || edition == 6 || edition == 8;
                }
            }
        }

        private async Task ExportBacpacAsync(string databaseName, string fileName)
        {
            await Task.Run(() =>
            {
                var services = new Microsoft.SqlServer.Dac.DacServices(_connectionString);
                services.ExportBacpac(fileName, databaseName);
            });
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

        public async Task<System.Collections.Generic.IEnumerable<string>> GetDatabasesAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            try
            {
                var builder = new SqlConnectionStringBuilder(_connectionString);

                if (!_discoverDatabases)
                {
                    var db = !string.IsNullOrEmpty(builder.InitialCatalog) ? builder.InitialCatalog : "master";
                    return new[] { db };
                }

                return await Task.Run(() =>
                {
                    var discoveryBuilder = new SqlConnectionStringBuilder(_connectionString)
                    {
                        ConnectTimeout = 5 // Fast fail for discovery
                    };

                    var server = ConnectToServer(discoveryBuilder, discoveryMode: true);
                    var databases = new System.Collections.Generic.List<string>();

                    foreach (Database db in server.Databases)
                    {
                        if (!db.IsSystemObject && !db.IsDatabaseSnapshot)
                        {
                            databases.Add(db.Name);
                        }
                    }

                    return (System.Collections.Generic.IEnumerable<string>)databases;
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching SQL Server databases: {ex.Message}");
                return new string[] { "MainDB (Offline)" };
            }
        }

        private Server ConnectToServer(SqlConnectionStringBuilder builder, bool discoveryMode = false)
        {
            var server = new Server(builder.DataSource);
            
            // Set SMO specific timeouts
            if (discoveryMode)
            {
                server.ConnectionContext.ConnectTimeout = 5;
            }

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
