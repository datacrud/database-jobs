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
        private readonly bool _discoverDatabases;

        public string ProviderName => "SQL Server";
        public string DisplayName { get; }
        public ProviderCapabilities Capabilities => ProviderCapabilities.All;

        public SqlServerProvider(string connectionString, string displayName = null, bool discover = true)
        {
            _connectionString = connectionString;
            _discoverDatabases = discover;
            DisplayName = displayName ?? ProviderName;
        }

        public async Task<string> BackupAsync(string databaseName, string backupDirectory, System.Threading.CancellationToken cancellationToken = default)
        {
            backupDirectory = Path.GetFullPath(backupDirectory);
            if (!Directory.Exists(backupDirectory)) Directory.CreateDirectory(backupDirectory);

            var isPaaS = await IsPaaSAsync();
            var extension = isPaaS ? "bacpac" : "bak";
            var fileName = Path.Combine(backupDirectory, $"sql_{databaseName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.{extension}");
            
            cancellationToken.ThrowIfCancellationRequested();
            var builder = new SqlConnectionStringBuilder(_connectionString);
            
            if (isPaaS)
            {
                await ExportBacpacAsync(databaseName, fileName, cancellationToken);
            }
            else
            {
                await Task.Run(() =>
                {
                    var server = ConnectToServer(builder);
                    var backup = new Backup
                    {
                        Action = BackupActionType.Database,
                        Database = databaseName
                    };
                    backup.Devices.AddDevice(fileName, DeviceType.File);
                    backup.Initialize = true;
                    cancellationToken.ThrowIfCancellationRequested();
                    backup.SqlBackup(server);
                });
            }

            return fileName;
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

        private async Task ExportBacpacAsync(string databaseName, string fileName, System.Threading.CancellationToken cancellationToken)
        {
            await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var services = new Microsoft.SqlServer.Dac.DacServices(_connectionString);
                services.ExportBacpac(fileName, databaseName);
            });
        }

        public async Task ShrinkAsync(string databaseName, System.Threading.CancellationToken cancellationToken = default)
        {
            await Task.Run(() =>
            {
                var builder = new SqlConnectionStringBuilder(_connectionString);
                var server = ConnectToServer(builder);
                var db = server.Databases[databaseName];

                cancellationToken.ThrowIfCancellationRequested();
                db.Shrink(10, ShrinkMethod.Default);
            });
        }

        public async Task ReindexAsync(string databaseName, System.Threading.CancellationToken cancellationToken = default)
        {
            await Task.Run(() =>
            {
                var builder = new SqlConnectionStringBuilder(_connectionString);
                var server = ConnectToServer(builder);
                var db = server.Databases[databaseName];

                foreach (Table table in db.Tables)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (table.IsSystemObject) continue;
                    table.RebuildIndexes(0);
                }
            });
        }

        public async Task ReorganizeAsync(string databaseName, System.Threading.CancellationToken cancellationToken = default)
        {
            await Task.Run(() =>
            {
                var builder = new SqlConnectionStringBuilder(_connectionString);
                var server = ConnectToServer(builder);
                var db = server.Databases[databaseName];

                foreach (Table table in db.Tables)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (table.IsSystemObject) continue;
                    
                    foreach (Index index in table.Indexes)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        index.Reorganize();
                    }
                }
            });
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

                return await Task.Run(async () =>
                {
                    var databases = new System.Collections.Generic.List<string>();
                    
                    using (var conn = new SqlConnection(_connectionString))
                    {
                        // Use a short timeout for discovery
                        var connectionBuilder = new SqlConnectionStringBuilder(_connectionString) { ConnectTimeout = 5 };
                        using (var discoveryConn = new SqlConnection(connectionBuilder.ConnectionString))
                        {
                            await discoveryConn.OpenAsync(cancellationToken);
                            var sql = "SELECT name FROM sys.databases WHERE state = 0 AND is_read_only = 0 AND name NOT IN ('master', 'model', 'msdb', 'tempdb')";
                            using (var cmd = new SqlCommand(sql, discoveryConn))
                            {
                                using (var reader = await cmd.ExecuteReaderAsync(cancellationToken))
                                {
                                    while (await reader.ReadAsync(cancellationToken))
                                    {
                                        databases.Add(reader.GetString(0));
                                    }
                                }
                            }
                        }
                    }

                    if (databases.Count == 0) databases.Add("MainDB (No Access)");
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

    }
}
