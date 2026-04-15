using System;
using System.IO;
using System.Threading.Tasks;
using CliWrap;
using DataCrud.DBOps.Core.Models;
using DataCrud.DBOps.Core.Providers;
using DataCrud.DBOps.Core.Storage;
using MongoDB.Bson;
using MongoDB.Driver;
using Serilog;

namespace DataCrud.DBOps.MongoDb
{
    public class MongoDbProvider : IDatabaseProvider
    {
        private readonly string _connectionString;
        private readonly IJobStorage _storage;
        private readonly bool _discoverDatabases;

        public string ProviderName => "MongoDB";
        public string DisplayName { get; }
        public ProviderCapabilities Capabilities => ProviderCapabilities.Backup | ProviderCapabilities.Shrink;

        public MongoDbProvider(string connectionString, IJobStorage storage, string displayName = null, bool discover = true)
        {
            _connectionString = connectionString;
            _storage = storage;
            _discoverDatabases = discover;
            DisplayName = displayName ?? ProviderName;
        }

        public async Task BackupAsync(string databaseName, string backupDirectory)
        {
            var fileName = Path.Combine(backupDirectory, $"{databaseName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}");
            var history = await CreateHistoryAsync(databaseName, JobType.Backup, $"Starting MongoDB backup (mongodump) to {fileName}");

            try
            {
                if (!Directory.Exists(backupDirectory)) Directory.CreateDirectory(backupDirectory);

                // CliWrap to run mongodump
                // Example: mongodump --uri="mongodb://user:pass@host:port" --db=dbname --out=backupdir
                var result = await Cli.Wrap("mongodump")
                    .WithArguments(args => args
                        .Add("--uri").Add(_connectionString)
                        .Add("--db").Add(databaseName)
                        .Add("--out").Add(backupDirectory)
                    )
                    .ExecuteAsync();

                // mongodump creates a folder per DB. 
                // In a production scenario, we might want to zip this.
                await CompleteHistoryAsync(history, $"MongoDB backup completed. Data saved to {fileName}");
            }
            catch (Exception ex)
            {
                await FailHistoryAsync(history, ex);
                throw;
            }
        }

        public async Task ShrinkAsync(string databaseName)
        {
            var history = await CreateHistoryAsync(databaseName, JobType.Shrink, "Starting MongoDB Compact operation.");

            try
            {
                var client = new MongoClient(_connectionString);
                var db = client.GetDatabase(databaseName);
                
                // MongoDB 'compact' command reclaims space in a collection.
                // We'll iterate through all collections.
                var collections = await db.ListCollectionNames().ToListAsync();
                
                foreach (var collectionName in collections)
                {
                    // compact requires admin/db privileges
                    var command = new BsonDocument { { "compact", collectionName } };
                    await db.RunCommandAsync<BsonDocument>(command);
                }

                await CompleteHistoryAsync(history, "MongoDB Compact operation completed for all collections.");
            }
            catch (Exception ex)
            {
                await FailHistoryAsync(history, ex);
                throw;
            }
        }

        public Task ReindexAsync(string databaseName)
        {
            // MongoDB 'reIndex' is legacy and deprecated in recent versions.
            // Modern MongoDB reindexing involves rebuilding indexes explicitly.
            // For now, we'll mark it as Not Supported by the provider capabilities.
            return Task.CompletedTask;
        }

        public async Task<System.Collections.Generic.IEnumerable<string>> GetDatabasesAsync()
        {
            try
            {
                var url = new MongoUrl(_connectionString);

                if (!_discoverDatabases)
                {
                    var db = !string.IsNullOrEmpty(url.DatabaseName) ? url.DatabaseName : "admin";
                    return new[] { db };
                }

                var client = new MongoClient(url);
                var databases = await client.ListDatabaseNamesAsync();
                var result = new System.Collections.Generic.List<string>();
                
                await databases.ForEachAsync(db => 
                {
                    if (db != "admin" && db != "local" && db != "config")
                    {
                        result.Add(db);
                    }
                });
                
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching MongoDB databases: {ex.Message}");
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
            Log.Error(ex, "Error in MongoDbProvider for {DatabaseName}", history.DatabaseName);
            history.Status = JobStatus.Failed;
            history.EndTime = DateTime.UtcNow;
            history.Message = "Error: " + ex.Message;
            history.Details = ex.ToString();
            await _storage.UpdateHistoryAsync(history);
        }
    }
}
