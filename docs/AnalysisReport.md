# Project Analysis: DataCrud.DBOps

Status: **Fully Stabilized** (NuGet Ready)  
Version: **1.0.0**  
Generated Packages: **8** (Core + Providers + Middleware)

This project is a high-performance C# library designed to automate database administration tasks, including maintenance, backups, and off-site cloud storage synchronization.

## Core Features

- **Multi-Engine Support**: Native providers for SQL Server, PostgreSQL, MySQL, Oracle, and MongoDB.
- **Automated Backups**: Intelligent generation of full database backups with automated retention policies.
- **Index Optimization**: Automated reorganization and rebuilding of indexes based on fragmentation metrics.
- **Persistent Orchestration**: Integrated **LiteDB** engine for tracking job history, failures, and state across restarts.
- **Cloud Synchronization**: High-speed, multi-threaded uploads to Azure Blob Storage and AWS S3.
- **Hybrid Compatibility**: Unified codebase supporting both modern .NET 9.0 (Dependency Injection) and legacy .NET Framework 4.8 (OWIN/Static).

## Solution Structure

The solution is architected for modularity and extensibility:

- **[DataCrud.DBOps.Core](file:///d:/Me/Projects/database-jobs/Source/DataCrud.DBOps.Core)**: The engine core containing the orchestrator, LiteDB storage implementation, and unified abstraction layers.
- **[DataCrud.DBOps.Sample](file:///d:/Me/Projects/database-jobs/Source/Samples/DataCrud.DBOps.Sample)**: A comprehensive, multi-targeted implementation demonstrating both modern DI and legacy service usage.
- **Provider Layer**:
    - `DataCrud.DBOps.SqlServer`: SMO-based SQL Server integration.
    - `DataCrud.DBOps.Postgres`, `MySql`, `Oracle`, `MongoDb`: Dedicated database drivers.
- **Cloud Extensions**:
    - `DataCrud.DBOps.AzurePush`: Native Azure Storage SDK integration.
    - `DataCrud.DBOps.AwsPush`: High-performance AWS S3 integration (rebranded from PushToAws).
- **Utility Layer**:
    - `DataCrud.DBOps.Zipper`: GZip/Deflate compression logic for backup files.

## Technical Details

- **Language**: C# (Latest)
- **Frameworks**: .NET 9.0, .NET Framework 4.8, .NET Standard 2.0.
- **Key Dependencies**:
    - **LiteDB**: Embedded NoSQL database for server-less job tracking.
    - **SMO / Dapper**: Advanced database management and performant script execution.
    - **Azure.Storage.Blobs & AWSSDK.S3**: Enterprise cloud SDKs.
    - **Serilog**: Structured logging for observability.

## Configuration & Deployment

The system supports multiple configuration strategies:
- **Dependency Injection**: Via `AddDatabaseOps` in modern .NET.
- **Middleware**: OWIN middleware for integration into existing ASP.NET applications.
- **Classic AppSettings**: Configurable via `App.config` or `Web.config` for standalone agents.

## Workflow Execution Lifecycle

1. **Initialize**: Loads configuration and establishes connection to the target database and the local LiteDB state store.
2. **Pre-Maintenance**: Executes index reorganization and database shrinking if enabled.
3. **Backup Execution**: Generates full backups and compresses them using the Zipper utility.
4. **Cloud Sync**: Simultaneously pushes compressed backups to configured cloud providers (Azure/AWS).
5. **Local Cleanup**: Removes uncompressed artifacts and enforces retention policies for older backups.
6. **Finalize**: Records the job outcome and performance metrics in LiteDB for dashboard visualization.

