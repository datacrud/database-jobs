using System.Collections.Generic;
using System.Threading.Tasks;
using DataCrud.DBOps.Core.Models;

namespace DataCrud.DBOps.Core.Storage
{
    public interface IJobStorage
    {
        Task InitializeAsync(string connectionString);
        Task<int> CreateHistoryAsync(JobHistory history);
        Task UpdateHistoryAsync(JobHistory history);
        Task<IEnumerable<JobHistory>> GetHistoryAsync(int top = 100);
        Task<JobHistory> GetHistoryByIdAsync(int id);

        // System Logs
        Task AddLogAsync(AppLog log);
        Task<IEnumerable<AppLog>> GetLogsAsync(int limit = 100, int? jobId = null);
    }
}

