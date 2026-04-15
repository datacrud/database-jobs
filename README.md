# DataCrud.DBOps

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-9.0%20%7C%20Standard%202.0%20%7C%204.8-blue)](https://dotnet.microsoft.com/)

**DataCrud.DBOps** is a high-performance, resilient database maintenance and job orchestration library for .NET. Designed to bridge the gap between modern cloud-native applications and legacy enterprise systems, it provides a unified interface for database backups, index optimization, and cloud synchronization.

---

## 🚀 Key Features

*   **Intelligent Maintenance**: Automated database shrinking and index management (Reorganize/Rebuild) to ensure peak performance.
*   **Resilient Backups**: Automated full-database backup workflows with high-ratio compression (Zip).
*   **Multi-Cloud Sync**: Native, high-performance integration with **AWS S3** and **Azure Blob Storage**.
*   **Persistent Tracking**: Integrated **LiteDB** storage engine for reliable, local job history and state management.
*   **Cross-Platform Heritage**: First-class support for **ASP.NET Core (DI-friendly)** and **Legacy .NET Framework (OWIN/Console)**.

---

## 🛠 Supported Ecosystem

| Category | Supported Providers |
| :--- | :--- |
| **Databases** | SQL Server, PostgreSQL, MySQL, Oracle, MongoDB |
| **Cloud Storage** | AWS S3, Azure Blob Storage |
| **Target Frameworks** | .NET 9.0, .NET Standard 2.0, .NET Framework 4.7+ |
| **Persistence** | LiteDB (Default Job Storage) |

---

## 💻 Getting Started

### Modern .NET (ASP.NET Core)

Install the core and your preferred provider via NuGet:
```bash
dotnet add package DataCrud.DBOps.AspNetCore
dotnet add package DataCrud.DBOps.SqlServer
```

Configure the services in `Program.cs`:
```csharp
builder.Services.AddDatabaseOps(options =>
{
    options.ConnectionString = "YourConnectionString";
    options.EnableBackupJob(cron: "0 0 * * *"); // Daily midnight backup
    options.EnableIndexMaintenance = true;
    
    // Multi-Cloud Sync
    options.AddAwsPush("bucket-name", "region");
});
```

### Legacy .NET Framework (OWIN)

For legacy applications using OWIN, register the middleware in your `Startup` class:
```csharp
public void Configuration(IAppBuilder app)
{
    app.UseDBOps(options =>
    {
        options.ConnectionString = "YourConnectionString";
        options.AuthUsername = "admin";
        options.AuthPassword = "securePassword";
        options.EnableAuth = true;
    });
}
```

---

## ⚙️ Configuration (AppSettings)

For standalone console applications or legacy implementations, use `App.config`:

| Key | Description | Default |
| :--- | :--- | :--- |
| `ServerName` | Target SQL Server instance | `.\SQLEXPRESS` |
| `BackupAllDatabases` | Process all non-system databases | `true` |
| `BackupDirectoryPath` | Local path for backup storage | `C:\temp\backups\` |
| `RemoveBakFileAfterZip` | Delete uncompressed `.bak` file after zipping | `true` |
| `PushToAwsS3Bucket` | Enable AWS S3 synchronization | `false` |

---

## 📜 Professional Services & Support

DataCrud.DBOps is designed for enterprise-grade stability. For advanced automation such as **Index Maintenance** across specialized environments, use our optimized SQL procedures:

```sql
-- Global Index Rebuild
EXEC sp_msforeachtable 'SET QUOTED_IDENTIFIER ON; ALTER INDEX ALL ON ? REBUILD'
```

---

## 📄 License

This project is licensed under the **MIT License**. See the `LICENSE` file for details.



