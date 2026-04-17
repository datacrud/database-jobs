using System;
using System.IO;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
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
        private readonly string _pgDumpPath;
        private readonly bool _discoverDatabases;

        public string ProviderName => "Postgres";
        public string DisplayName { get; }
        public ProviderCapabilities Capabilities => ProviderCapabilities.All;

        public PostgresProvider(string connectionString, string displayName = null, bool discover = true, string pgDumpPath = "pg_dump")
        {
            _connectionString = connectionString;
            _pgDumpPath = pgDumpPath;
            _discoverDatabases = discover;
            DisplayName = displayName ?? ProviderName;
        }

        private string ResolvePgDumpPath()
        {
            // 1. If absolute path provided, use it
            if (Path.IsPathRooted(_pgDumpPath) && File.Exists(_pgDumpPath))
                return _pgDumpPath;

            // 2. Try common Windows locations
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var commonPaths = new[]
                {
                    @"C:\Program Files\PostgreSQL\18\bin\pg_dump.exe",
                    @"C:\Program Files\PostgreSQL\17\bin\pg_dump.exe",
                    @"C:\Program Files\PostgreSQL\16\bin\pg_dump.exe",
                    @"C:\Program Files\PostgreSQL\15\bin\pg_dump.exe"
                };

                foreach (var path in commonPaths)
                {
                    if (File.Exists(path)) return path;
                }
            }

            // 3. Fallback to default (hope it is in PATH)
            return _pgDumpPath;
        }

        private string GetConnectionString(string databaseName)
        {
            var builder = new NpgsqlConnectionStringBuilder(_connectionString);
            builder.Database = databaseName;
            return builder.ConnectionString;
        }

        public async Task<string> BackupAsync(string databaseName, string backupDirectory, System.Threading.CancellationToken cancellationToken = default)
        {
            var fileName = Path.Combine(backupDirectory, $"pg_{databaseName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.sql");

            if (!Directory.Exists(backupDirectory)) Directory.CreateDirectory(backupDirectory);

            var resolvedPath = ResolvePgDumpPath();
            var builder = new NpgsqlConnectionStringBuilder(_connectionString);
            
            // Use CliWrap to run pg_dump
            try 
            {
                var result = await Cli.Wrap(resolvedPath)
                    .WithArguments(args => args
                        .Add("--host").Add(builder.Host)
                        .Add("--username").Add(builder.Username)
                        .Add("--dbname").Add(databaseName)
                        .Add("--file").Add(fileName)
                    )
                    .WithEnvironmentVariables(env => env
                        .Set("PGPASSWORD", builder.Password)
                    )
                    .ExecuteAsync(cancellationToken);

                return fileName;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                throw new InvalidOperationException($"Postgres backup utility 'pg_dump' was not found at '{resolvedPath}'. Please ensure PostgreSQL is installed and in the system PATH, or specify the full path in configuration.");
            }
        }

        public async Task ShrinkAsync(string databaseName, System.Threading.CancellationToken cancellationToken = default)
        {
            using (var conn = new NpgsqlConnection(GetConnectionString(databaseName)))
            {
                // Full Vacuum reclaims space but locks the table
                await conn.ExecuteAsync(new CommandDefinition("VACUUM FULL;", cancellationToken: cancellationToken));
            }
        }

        public async Task ReindexAsync(string databaseName, System.Threading.CancellationToken cancellationToken = default)
        {
            using (var conn = new NpgsqlConnection(GetConnectionString(databaseName)))
            {
                await conn.ExecuteAsync(new CommandDefinition($"REINDEX DATABASE \"{databaseName}\";", cancellationToken: cancellationToken));
            }
        }

        public async Task ReorganizeAsync(string databaseName, System.Threading.CancellationToken cancellationToken = default)
        {
            using (var conn = new NpgsqlConnection(GetConnectionString(databaseName)))
            {
                // VACUUM ANALYZE updates statistics and removes dead tuples without exclusive locks
                await conn.ExecuteAsync(new CommandDefinition("VACUUM ANALYZE;", cancellationToken: cancellationToken));
            }
        }

        public async Task<System.Collections.Generic.IEnumerable<string>> GetDatabasesAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            try
            {
                var builder = new NpgsqlConnectionStringBuilder(_connectionString);
                
                if (!_discoverDatabases)
                {
                    var db = !string.IsNullOrEmpty(builder.Database) ? builder.Database : "postgres";
                    return new[] { db };
                }

                builder.Timeout = 3; // Fast fail for discovery
                using (var conn = new NpgsqlConnection(builder.ConnectionString))
                {
                    // Query for non-template databases
                    var databases = await conn.QueryAsync<string>(@"
                        SELECT datname 
                        FROM pg_database 
                        WHERE datistemplate = false 
                          AND datname != 'postgres' AND datname != 'template1';");
                    return databases;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error fetching Postgres databases");
                return new string[] { "MainDB (Offline)" };
            }
        }

    }
}
