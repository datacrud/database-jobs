using System;
using System.IO;
using System.Threading.Tasks;
using CliWrap;
using DataCrud.DBOps.Core.Models;
using DataCrud.DBOps.Core.Providers;
using MongoDB.Bson;
using MongoDB.Driver;
using Serilog;

namespace DataCrud.DBOps.MongoDb
{
    public class MongoDbProvider : IDatabaseProvider
    {
        private readonly string _connectionString;
        private readonly bool _discoverDatabases;

        public string ProviderName => "MongoDB";
        public string DisplayName { get; }
        public ProviderCapabilities Capabilities => ProviderCapabilities.Backup | ProviderCapabilities.Reindex | ProviderCapabilities.Reorganize;

        public MongoDbProvider(string connectionString, string displayName = null, bool discover = true)
        {
            _connectionString = connectionString;
            _discoverDatabases = discover;
            DisplayName = displayName ?? ProviderName;
        }

        public async Task<string> BackupAsync(string databaseName, string backupDirectory, System.Threading.CancellationToken cancellationToken = default)
        {
            var fileName = Path.Combine(backupDirectory, $"{databaseName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}");
            
            if (!Directory.Exists(backupDirectory)) Directory.CreateDirectory(backupDirectory);

            // CliWrap to run mongodump
            var result = await Cli.Wrap("mongodump")
                .WithArguments(args => args
                    .Add("--uri").Add(_connectionString)
                    .Add("--db").Add(databaseName)
                    .Add("--out").Add(backupDirectory)
                )
                .ExecuteAsync(cancellationToken);

            return fileName;
        }

        public async Task ShrinkAsync(string databaseName, System.Threading.CancellationToken cancellationToken = default)
        {
            var client = new MongoClient(_connectionString);
            var db = client.GetDatabase(databaseName);
            
            // MongoDB 'compact' command reclaims space in a collection.
            var collections = await db.ListCollectionNames(cancellationToken: cancellationToken).ToListAsync(cancellationToken);
            
            foreach (var collectionName in collections)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var command = new BsonDocument { { "compact", collectionName } };
                await db.RunCommandAsync<BsonDocument>(command, cancellationToken: cancellationToken);
            }
        }

        public Task ReorganizeAsync(string databaseName, System.Threading.CancellationToken cancellationToken = default)
        {
            // MongoDB with WiredTiger engine manages storage reorganization internaly.
            return Task.CompletedTask;
        }

        public Task ReindexAsync(string databaseName, System.Threading.CancellationToken cancellationToken = default)
        {
            // MongoDB 'reIndex' is legacy and deprecated in recent versions.
            return Task.CompletedTask;
        }

        public async Task<System.Collections.Generic.IEnumerable<string>> GetDatabasesAsync(System.Threading.CancellationToken cancellationToken = default)
        {
            try
            {
                var url = new MongoUrl(_connectionString);

                if (!_discoverDatabases)
                {
                    var db = !string.IsNullOrEmpty(url.DatabaseName) ? url.DatabaseName : "admin";
                    return new[] { db };
                }

                var settings = MongoClientSettings.FromUrl(url);
                settings.ConnectTimeout = TimeSpan.FromSeconds(3); // Fast fail for discovery
                
                var client = new MongoClient(settings);
                var databases = await client.ListDatabaseNamesAsync(cancellationToken);
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
                Log.Error(ex, "Error fetching MongoDB databases");
                return new string[] { "MainDB (Offline)" };
            }
        }
    }
}
