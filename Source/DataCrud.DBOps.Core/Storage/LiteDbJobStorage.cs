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
        private const string HistoryCollection = "job_history";
        private const string LogCollection = "system_logs";

        public LiteDbJobStorage(string connectionString)
        {
            var path = connectionString ?? "jobs_dbops.db";
            // Ensure Connection=Shared for multi-process/multi-instance stability
            _connectionString = path.Contains(";") ? path : $"Filename={path};Connection=shared";
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
                var col = db.GetCollection<JobHistory>(HistoryCollection);
                var id = col.Insert(history);
                return Task.FromResult(id.AsInt32);
            }
        }

        public Task UpdateHistoryAsync(JobHistory history)
        {
            using (var db = new LiteDatabase(_connectionString))
            {
                var col = db.GetCollection<JobHistory>(HistoryCollection);
                col.Update(history);
                return Task.CompletedTask;
            }
        }

        public Task<IEnumerable<JobHistory>> GetHistoryAsync(int top = 100)
        {
            using (var db = new LiteDatabase(_connectionString))
            {
                var col = db.GetCollection<JobHistory>(HistoryCollection);
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
                var col = db.GetCollection<JobHistory>(HistoryCollection);
                var log = col.FindById(id);
                return Task.FromResult(log);
            }
        }

        public Task AddLogAsync(AppLog log)
        {
            using (var db = new LiteDatabase(_connectionString))
            {
                var col = db.GetCollection<AppLog>(LogCollection);
                col.Insert(log);
                return Task.CompletedTask;
            }
        }

        public Task<IEnumerable<AppLog>> GetLogsAsync(int limit = 100, int? jobId = null)
        {
            using (var db = new LiteDatabase(_connectionString))
            {
                var col = db.GetCollection<AppLog>(LogCollection);
                var query = col.Query();

                if (jobId.HasValue)
                {
                    query = query.Where(x => x.JobId == jobId.Value);
                }

                var logs = query
                    .OrderByDescending(x => x.Timestamp)
                    .Limit(limit)
                    .ToList();
                return Task.FromResult<IEnumerable<AppLog>>(logs);
            }
        }
    }
}
