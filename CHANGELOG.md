# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.0.1] - 2026-04-17

### Added
- **Enriched Documentation**: Added detailed guides to `README.md` for Multi-Database configuration, Security/Authentication, and Legacy .NET Framework integration.
- **Multi-Server Discovery**: Documented the "Discovery Mode" for automatically finding all databases on a target server.
- **Branding**: Set `DataCrud` as the primary owner and company for all NuGet packages.

### Changed
- **Centralized Metadata**: Migrated all NuGet metadata (Version, Authors, License, Repository) to `Source/Directory.Build.props` for solution-wide consistency.
- **Solution Cleanup**: Removed the redundant `DataCrud.DBOps.Maintenance` project and consolidated all maintenance logic into the `Core` package.

### Fixed
- **NuGet Readiness**: Fixed "Missing README" warning on NuGet.org by embedding the project `README.md` directly into the `.nupkg` artifacts.
- **Release Stability**: Synchronized all stabilization fixes across `master` and `release/1.0.1` branches.

## [1.0.0] - 2026-04-16

### Added
- **Provider-Agnostic Architecture**: Introduced `IDatabaseProvider` to support SQL Server, PostgreSQL, MySQL, and Oracle.
- **Azure SQL (PaaS) Support**: Implemented BACPAC export for SQL Server instances running in Azure PaaS.
- **Cloud Storage Integration**: Added `CloudPushService` for automatic backup synchronization to Azure Blob Storage and AWS S3.
- **Provider-Prefixed Naming**: All backup files now include a provider prefix (`sql_`, `pg_`, `my_`, `ora_`) for better organization.
- **Modern Dashboard**: Embedded HTML/JS dashboard using Tailwind CSS, Alpine.js, and HTMX.
- **Busy Overlay**: Global UI indicator for asynchronous maintenance jobs and database discovery.
- **BACPAC Stabilization**: Optimized SQL Server SMO for Azure environments.

### Changed
- Standardized NuGet dependencies on `Dapper`, `Microsoft.Data.SqlClient`, and `Npgsql`.
- Refactored `MaintenanceManager` to orchestrate multi-step maintenance workflows including cloud pushes.
- Updated `BackupAsync` return types to enable path-aware orchestration.

### Fixed
- SQL Server SMO connection issues during backup operations.
- Version mismatch in cross-provider dependencies.
- UI state persistence during provider switching.
