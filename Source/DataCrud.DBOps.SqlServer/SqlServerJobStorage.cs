using System;
using System.Collections.Generic;
using Microsoft.Data.SqlClient;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using DataCrud.DBOps.Core.Models;
using DataCrud.DBOps.Core.Storage;

namespace DataCrud.DBOps.SqlServer
{
    public class SqlServerJobStorage : IJobStorage
    {
        private readonly string _connectionString;
        private const string SchemaName = "_dbops";

        public SqlServerJobStorage(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task InitializeAsync(string connectionString = null)
        {
            var connStr = connectionString ?? _connectionString;
            using (var db = new SqlConnection(connStr))
            {
                await db.ExecuteAsync($@"
                    IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = '{SchemaName}')
                    BEGIN
                        EXEC('CREATE SCHEMA {SchemaName}')
                    END

                    IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'History' AND schema_id = SCHEMA_ID('{SchemaName}'))
                    BEGIN
                        CREATE TABLE {SchemaName}.History (
                            Id INT PRIMARY KEY IDENTITY(1,1),
                            DatabaseName NVARCHAR(255) NULL,
                            JobType NVARCHAR(50) NOT NULL,
                            StartTime DATETIME NOT NULL,
                            EndTime DATETIME NULL,
                            Status NVARCHAR(50) NOT NULL,
                            Message NVARCHAR(MAX) NULL,
                            Details NVARCHAR(MAX) NULL
                        )
                    END
                ");
            }
        }

        public async Task<int> CreateHistoryAsync(JobHistory history)
        {
            using (var db = new SqlConnection(_connectionString))
            {
                var id = await db.QuerySingleAsync<int>($@"
                    INSERT INTO {SchemaName}.History (DatabaseName, JobType, StartTime, Status, Message, Details)
                    VALUES (@DatabaseName, @JobType, @StartTime, @Status, @Message, @Details);
                    SELECT CAST(SCOPE_IDENTITY() AS INT);
                ", history);
                history.Id = id;
                return id;
            }
        }

        public async Task UpdateHistoryAsync(JobHistory history)
        {
            using (var db = new SqlConnection(_connectionString))
            {
                await db.ExecuteAsync($@"
                    UPDATE {SchemaName}.History 
                    SET Status = @Status, EndTime = @EndTime, Message = @Message, Details = @Details
                    WHERE Id = @Id
                ", history);
            }
        }

        public async Task<IEnumerable<JobHistory>> GetHistoryAsync(int top = 100)
        {
            using (var db = new SqlConnection(_connectionString))
            {
                return await db.QueryAsync<JobHistory>($@"
                    SELECT TOP (@top) * FROM {SchemaName}.History ORDER BY StartTime DESC
                ", new { top });
            }
        }

        public async Task<JobHistory> GetHistoryByIdAsync(int id)
        {
            using (var db = new SqlConnection(_connectionString))
            {
                return await db.QueryFirstOrDefaultAsync<JobHistory>($@"
                    SELECT * FROM {SchemaName}.History WHERE Id = @id
                ", new { id });
            }
        }
    }
}
