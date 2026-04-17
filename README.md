# DataCrud.DBOps

[![NuGet Version](https://img.shields.io/nuget/v/DataCrud.DBOps.Core.svg)](https://www.nuget.org/packages/DataCrud.DBOps.Core)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-9.0%20%7C%20Standard%202.0%20%7C%204.8-blue)](https://dotnet.microsoft.com/)

**DataCrud.DBOps** is a premium, high-performance database maintenance and job orchestration library for .NET. Designed to bridge the gap between modern cloud-native architectures and legacy enterprise systems, it provides a unified interface for database backups, index optimization, and real-time operational monitoring.

---

## ✨ Key Features

*   **⚡ Intelligent Maintenance**: Automated database shrinking and sophisticated index management (Reorganize/Rebuild) to ensure peak performance.
*   **🛡️ Resilient Backups**: Automated full-database backup workflows with high-ratio compression and integrity verification.
*   **☁️ Multi-Cloud Sync**: Native, high-performance integration with **AWS S3** and **Azure Blob Storage** for off-site redundancy.
*   **📊 Embedded Dashboard**: A stunning, lightweight operational dashboard built with **HTMX** and **Alpine.js**, providing real-time job control and log visibility.
*   **📝 Centralized Logging**: Robust `IJobStorage` architecture supporting **SQL Server** and **LiteDB**, featuring parent/child log relationships for granular auditing.
*   **🚀 Cross-Platform Heritage**: First-class support for **ASP.NET Core (DI-friendly)** and **Legacy .NET Framework (OWIN/Console)**.

---

## 🏗️ Architecture: Centralized History

Unlike traditional maintenance scripts, DBOps utilizes a centralized orchestration engine. All database providers report to a unified `IJobStorage` backend, ensuring that your maintenance history is consistent across all servers.

*   **Parent Logs**: Summary of job type, execution status, and duration.
*   **Child Logs**: Detailed execution traces, warnings, and error stack traces for every step of the process.

---

## 🛠 Supported Ecosystem

| Category | Supported Providers | NuGet Status |
| :--- | :--- | :--- |
| **Databases** | SQL Server, PostgreSQL, MySQL, Oracle, MongoDB | [![SqlServer](https://img.shields.io/nuget/v/DataCrud.DBOps.SqlServer.svg?label=SqlServer)](https://www.nuget.org/packages/DataCrud.DBOps.SqlServer) [![Postgres](https://img.shields.io/nuget/v/DataCrud.DBOps.Postgres.svg?label=Postgres)](https://www.nuget.org/packages/DataCrud.DBOps.Postgres) [![MySql](https://img.shields.io/nuget/v/DataCrud.DBOps.MySql.svg?label=MySql)](https://www.nuget.org/packages/DataCrud.DBOps.MySql) |
| **Cloud Storage** | AWS S3, Azure Blob Storage | [![AwsPush](https://img.shields.io/nuget/v/DataCrud.DBOps.AwsPush.svg?label=AWS)](https://www.nuget.org/packages/DataCrud.DBOps.AwsPush) [![AzurePush](https://img.shields.io/nuget/v/DataCrud.DBOps.AzurePush.svg?label=Azure)](https://www.nuget.org/packages/DataCrud.DBOps.AzurePush) |
| **Target Frameworks** | .NET 9.0, .NET Standard 2.0, .NET Framework 4.7+ | [![Frameworks](https://img.shields.io/badge/.NET-9.0%20%7C%20Standard%202.0%20%7C%204.8-blue)](https://dotnet.microsoft.com/) |
| **Core Components** | Core Engine, Middleware, Backup, Maintenance | [![Core](https://img.shields.io/nuget/v/DataCrud.DBOps.Core.svg?label=Core)](https://www.nuget.org/packages/DataCrud.DBOps.Core) [![AspNetCore](https://img.shields.io/nuget/v/DataCrud.DBOps.AspNetCore.svg?label=AspNetCore)](https://www.nuget.org/packages/DataCrud.DBOps.AspNetCore) [![Backup](https://img.shields.io/nuget/v/DataCrud.DBOps.Backup.svg?label=Backup)](https://www.nuget.org/packages/DataCrud.DBOps.Backup) |

---

## 💻 Getting Started

### 1. Modern .NET (ASP.NET Core)

Install the core and your preferred provider via NuGet:

```bash
dotnet add package DataCrud.DBOps.AspNetCore
dotnet add package DataCrud.DBOps.SqlServer
```

Configure with Dependency Injection:

```csharp
builder.Services.AddDBOps(options =>
{
    // Configure Storage (Centralized History)
    options.Storage = new SqlServerJobStorage("YourHistoryDbConnectionString");
    
    // Multi-Cloud Sync
    options.PushToAws = true;
    options.AwsBucketName = "bucket-name";
    options.AwsRegion = "us-east-1";
});
```

### 2. Multi-Database Configuration

DBOps is designed to manage multiple database instances (and different providers) simultaneously from a single dashboard.

```csharp
builder.Services.AddDBOps(options =>
{
    options.Storage = new LiteDbJobStorage("history.db");

    // --- Zipping Configuration ---
    options.EnableZipping = true; // Set false to skip compression (default: true)
    options.BackupPath = "C:\\Backups"; // Custom local staging directory

    // --- Azure Storage Configuration ---
    // Backups will be pushed to the 'backups' container in 'databases/' folder
    options.PushToAzure = true;
    options.AzureStorageConnectionString = "DefaultEndpointsProtocol=https;AccountName=...";

    // --- AWS S3 Configuration ---
    options.PushToAws = true;
    options.AwsAccessKey = "YOUR-ACCESS-KEY";
    options.AwsSecretKey = "YOUR-SECRET-KEY";
    options.AwsBucketName = "your-bucket-name";
    options.AwsRegion = "us-east-1";

    // --- Multi-Server Registration ---
    options.Providers.Add(new SqlServerProvider("ConnString1", "Production ERP"));
    options.Providers.Add(new PostgresProvider("PostgresConn", "Analytics App"));

    // Enable "Discovery Mode" to automatically find all DBs on a server
    options.Providers.Add(new SqlServerProvider("ServerConn", "Main Cluster", discover: true));
});
```

### 2. .NET Framework / Legacy (OWIN)

For legacy applications, install the AspNet integration:

```bash
Install-Package DataCrud.DBOps.AspNet
```

Configure in your `Startup.cs`:

```csharp
public void Configuration(IAppBuilder app)
{
    app.UseDBOps(options => 
    {
        options.DashboardPath = "/dbops";
        options.Storage = new LiteDbJobStorage("jobs_dbops.db");
        
        // Register your database providers
        options.Providers.Add(new SqlServerProvider("YourConnectionString", "Data Server 01"));
    });
}
```

### 2. Embedded Dashboard

Access the premium dashboard by mapping the middleware in your startup:

```csharp
app.UseDatabaseOpsDashboard(options => 
{
    options.Path = "/dbops";
    options.Security.RequireAdminRole = true;
});
```

---

## 🔒 Security & Authentication

DBOps is secure by default. You can configure authentication for the dashboard using several methods:

### 1. Basic Authentication
The simplest way to protect your dashboard is using the built-in Basic Auth.

```csharp
options.Security.Enabled = true; // Enabled by default
options.Security.Username = "admin";
options.Security.Password = "strong-password-here";
```

### 2. Role-Based Authorization
If your application uses ASP.NET Identity or OWIN Security, you can restrict access to specific roles:

```csharp
options.Security.AllowedRoles = new[] { "Administrator", "DBManager" };
```

### 3. Custom Authorization Filters
For advanced security requirements (IP filtering, custom headers, etc.), implement `IDBOpsAuthorizationFilter`:

```csharp
options.Security.AuthorizationFilters.Add(new MyCustomAuthFilter());
```

---

### 3. Scheduling & Background Jobs

You can easily automate your database maintenance using popular schedulers like **Hangfire** or native **.NET Background Services**.

#### Using Hangfire
```csharp
// In your Startup or Program.cs
RecurringJob.AddOrUpdate<MaintenanceManager>(
    "daily-backup",
    manager => manager.RunAsync("MyDatabase", true, true, true, true, true, "C:\\Backups", 7, true, null, default),
    Cron.Daily
);
```

#### Using .NET BackgroundService
```csharp
public class DatabaseBackupWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    public DatabaseBackupWorker(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var manager = scope.ServiceProvider.GetRequiredService<MaintenanceManager>();
                await manager.RunAsync("MyDatabase", backup: true, shrink: true, index: true, 
                                     reorganize: true, cleanup: true, backupDir: "C:\\Backups");
            }
            
            // Wait 24 hours
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}
```

## 📜 Professional Services & Support

DataCrud.DBOps is maintained for enterprise-grade stability. For custom provider development or specialized cloud integrations, please reach out to the [DataCrud](https://datacrud.com) team.

---

## 📄 License

This project is licensed under the **MIT License**. See the `LICENSE` file for details.
