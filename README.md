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
builder.Services.AddDatabaseOps(options =>
{
    // Configure Storage (Centralized History)
    options.UseSqlServerStorage("YourHistoryDbConnectionString");
    
    // Configure Jobs
    options.EnableBackupJob(cron: "0 0 * * *"); // Daily midnight backup
    options.EnableIndexMaintenance = true;
    
    // Multi-Cloud Sync
    options.AddAwsPush("bucket-name", "region");
});
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

## 🔒 Security

DBOps includes built-in security filters for the dashboard, supporting:
- **Basic Auth**: For quick internal deployments.
- **Role-based Authorization**: Full integration with ASP.NET Identity and OWIN security contexts.

---

## 📜 Professional Services & Support

DataCrud.DBOps is maintained for enterprise-grade stability. For custom provider development or specialized cloud integrations, please reach out to the [DataCrud](https://github.com/datacrud) team.

---

## 📄 License

This project is licensed under the **MIT License**. See the `LICENSE` file for details.
