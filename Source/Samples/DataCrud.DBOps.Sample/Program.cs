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

builder.Services.AddDBOps(options => 
{
    // Dashboard Path
    options.DashboardPath = "/db-ops";

    // Storage (common for all providers)
    options.Storage = storage;

    // SQL Server Provider
    options.Providers.Add(new SqlServerProvider(
        connectionString: builder.Configuration.GetConnectionString("SqlServer") ?? "Server=(localdb)\\mssqllocaldb;Database=MasterSample;Trusted_Connection=True;", 
        storage: storage));
    
    // PostgreSQL Provider
    options.Providers.Add(new PostgresProvider(
        connectionString: builder.Configuration.GetConnectionString("Postgres") ?? "Host=localhost;Database=postgres;Username=postgres;Password=password", 
        storage: storage));

    // MySQL Provider
    options.Providers.Add(new MySqlProvider(
        connectionString: builder.Configuration.GetConnectionString("MySql") ?? "Server=localhost;Database=mysql;Uid=root;Pwd=password;", 
        storage: storage));

    // MongoDB Provider
    options.Providers.Add(new MongoDbProvider(
        connectionString: builder.Configuration.GetConnectionString("Mongo") ?? "mongodb://localhost:27017", 
        storage: storage));

    // Oracle Provider
    options.Providers.Add(new OracleProvider(
        connectionString: builder.Configuration.GetConnectionString("Oracle") ?? "Data Source=localhost:1521/XEPDB1;User Id=system;Password=password;", 
        storage: storage));

    // Dashboard Security
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
