using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DataCrud.DBOps.Core.Models;
using LiteDB;
using System.Linq;

namespace DataCrud.DBOps.Core.Storage
{
    public class LiteDbJobStorage : IJobStorage
    {
        private readonly string _connectionString;
        private const string CollectionName = "job_history";

        public LiteDbJobStorage(string connectionString)
        {
            _connectionString = connectionString ?? "jobs_dbops.db";
        }

        public Task InitializeAsync(string connectionString)
        {
            // Initialization is handled by LiteDB on first access
            return Task.CompletedTask;
        }

        public Task<int> CreateHistoryAsync(JobHistory history)
        {
            using (var db = new LiteDatabase(_connectionString))
            {
                var col = db.GetCollection<JobHistory>(CollectionName);
                var id = col.Insert(history);
                return Task.FromResult(id.AsInt32);
            }
        }

        public Task UpdateHistoryAsync(JobHistory history)
        {
            using (var db = new LiteDatabase(_connectionString))
            {
                var col = db.GetCollection<JobHistory>(CollectionName);
                col.Update(history);
                return Task.CompletedTask;
            }
        }

        public Task<IEnumerable<JobHistory>> GetHistoryAsync(int top = 100)
        {
            using (var db = new LiteDatabase(_connectionString))
            {
                var col = db.GetCollection<JobHistory>(CollectionName);
                var logs = col.Query()
                    .OrderByDescending(x => x.StartTime)
                    .Limit(top)
                    .ToList();
                return Task.FromResult<IEnumerable<JobHistory>>(logs);
            }
        }

        public Task<JobHistory> GetHistoryByIdAsync(int id)
        {
            using (var db = new LiteDatabase(_connectionString))
            {
                var col = db.GetCollection<JobHistory>(CollectionName);
                var log = col.FindById(id);
                return Task.FromResult(log);
            }
        }
    }
}
