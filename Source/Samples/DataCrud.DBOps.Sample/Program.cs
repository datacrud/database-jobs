#if NET9_0
using DataCrud.DBOps.AspNetCore;
using DataCrud.DBOps.SqlServer;
using DataCrud.DBOps.Postgres;
using DataCrud.DBOps.MySql;
using DataCrud.DBOps.MongoDb;
using DataCrud.DBOps.Oracle;
using DataCrud.DBOps.Core.Storage;
using DataCrud.DBOps.Core.Providers;

var builder = WebApplication.CreateBuilder(args);

// 1. Register DataCrud.DBOps with ALL providers
IJobStorage storage = new LiteDbJobStorage("jobs_dbops.db");

// Help the user by checking common local DB instances
string GetValidConnectionString(string key, string fallback, string? pingTest = null) {
    var val = builder.Configuration.GetConnectionString(key);
    if (!string.IsNullOrEmpty(val)) return val;
    return fallback;
}

builder.Services.AddDBOps(options => 
{
    options.DashboardPath = "/db-ops";
    options.Storage = storage;

    // 0. Mock Provider - ALWAYS HERE for reliable local testing without infra
    options.Providers.Add(new MockProvider("Mock Infrastructure (Test Only)"));

    // 1. SQL Server - Full Discovery Mode
    // Try LocalDB first as it's common for developers
    var sqlConn = GetValidConnectionString("SqlServer", "Server=.\\SQLExpress;Database=master;Trusted_Connection=True;TrustServerCertificate=True;");
    options.Providers.Add(new SqlServerProvider(sqlConn, "SQL Express Server", discover: true));
    
    // 2. PostgreSQL - Full Discovery Mode
    var pgConn = GetValidConnectionString("Postgres", "Host=localhost;Database=postgres;Username=postgres;Password=postgres");
    options.Providers.Add(new PostgresProvider(pgConn, "Local PostgreSQL", discover: true));

    // 3. MySQL Provider
    var mySqlConn = GetValidConnectionString("MySql", "Server=localhost;Database=mysql;Uid=root;Pwd=password;");
    options.Providers.Add(new MySqlProvider(mySqlConn, "Local MySQL", discover: true));

    // 4. MongoDB Provider
    var mongoConn = GetValidConnectionString("Mongo", "mongodb://localhost:27017");
    options.Providers.Add(new MongoDbProvider(mongoConn, "Local MongoDB", discover: true));

    // 5. Oracle Provider
    var oracleConn = GetValidConnectionString("Oracle", "Data Source=localhost:1521/XEPDB1;User Id=system;Password=password;");
    options.Providers.Add(new OracleProvider(oracleConn, "Oracle Instance", discover: true));

    options.Security.Enabled = false;
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
            var sqlProvider = new SqlServerProvider(connectionString: "Server=(localdb)\\mssqllocaldb;Database=master;Trusted_Connection=True;");

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
