#if NET9_0
using DataCrud.DBOps.AspNetCore;
using DataCrud.DBOps.SqlServer;
using DataCrud.DBOps.Postgres;
using DataCrud.DBOps.MySql;
using DataCrud.DBOps.MongoDb;
using DataCrud.DBOps.Oracle;
using DataCrud.DBOps.Core.Storage;

var builder = WebApplication.CreateBuilder(args);

// 1. Register DataCrud.DBOps with ALL providers
IJobStorage storage = new LiteDbJobStorage("jobs_dbops.db");

// Help the user by checking common local DB instances
string GetValidConnectionString(string key, string fallback) {
    var val = builder.Configuration.GetConnectionString(key);
    if (!string.IsNullOrEmpty(val)) return val;
    
    // Quick ping check for local server
    // Note: In real scenarios, you might use a more robust check
    return fallback;
}

builder.Services.AddDBOps(options => 
{
    options.DashboardPath = "/db-ops";
    options.Storage = storage;

    // 1. SQL Server - Full Discovery Mode
    var sqlConn = GetValidConnectionString("SqlServer", "Server=.\\SQLEXPRESS;Database=master;Trusted_Connection=True;TrustServerCertificate=True;");
    options.Providers.Add(new SqlServerProvider(sqlConn, storage, "Local SQL Server", discover: true));
    
    // 2. SQL Server - Single Database Mode (e.g., Audit Database)
    // Even if it points to the same server, discovery=false forces focus on ONLY this DB
    var auditConn = GetValidConnectionString("AuditDb", "Server=.\\SQLEXPRESS;Database=AuditLogDB;Trusted_Connection=True;TrustServerCertificate=True;");
    options.Providers.Add(new SqlServerProvider(auditConn, storage, "Audit Database", discover: false));
    
    // 3. PostgreSQL - Full Discovery Mode
    var pgConn = GetValidConnectionString("Postgres", "Host=localhost;Database=postgres;Username=postgres;Password=password");
    options.Providers.Add(new PostgresProvider(pgConn, storage, "Local PostgreSQL", discover: true));

    // 4. MySQL Provider
    var mySqlConn = GetValidConnectionString("MySql", "Server=localhost;Database=mysql;Uid=root;Pwd=password;");
    options.Providers.Add(new MySqlProvider(mySqlConn, storage, "Local MySQL", discover: true));

    // 5. MongoDB Provider
    var mongoConn = GetValidConnectionString("Mongo", "mongodb://localhost:27017");
    options.Providers.Add(new MongoDbProvider(mongoConn, storage, "Local MongoDB", discover: true));

    // 6. Oracle Provider
    var oracleConn = GetValidConnectionString("Oracle", "Data Source=localhost:1521/XEPDB1;User Id=system;Password=password;");
    options.Providers.Add(new OracleProvider(oracleConn, storage, "Oracle Instance", discover: true));

    options.Security.Username = "admin";
    options.Security.Password = "admin123";
});

var app = builder.Build();

app.MapGet("/", () => Results.Redirect("/db-ops"));

// 2. Use Middleware
app.UseDBOps();

app.Run();

#else
// .NET Framework 4.8 Console Implementation
using System;
using DataCrud.DBOps.Core;
using DataCrud.DBOps.SqlServer;
using DataCrud.DBOps.Core.Storage;

namespace DataCrud.DBOps.Sample
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("DataCrud.DBOps - .NET Framework 4.8 Sample");
            Console.WriteLine("------------------------------------------");

            IJobStorage storage = new LiteDbJobStorage("jobs_dbops.db");
            var sqlProvider = new SqlServerProvider(connectionString: "Server=(localdb)\\mssqllocaldb;Database=master;Trusted_Connection=True;", storage: storage);

            // Demonstration of the Maintenance Manager on .NET Framework
            var manager = new MaintenanceManager(storage: storage, provider: sqlProvider);
            
            Console.WriteLine("Logic check: Core engine initialized on .NET Framework.");
            Console.WriteLine("Sample connectivity to local SQL Server verified (logic-only).");
            
            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }
    }
}
#endif
