using System;
using System.Threading.Tasks;
using System.IO;
using System.Collections.Generic;
using DataCrud.DBOps.Core.Models;
using DataCrud.DBOps.Core.Providers;
using DataCrud.DBOps.Core.Services;
using DataCrud.DBOps.Core.Storage;
using DataCrud.DBOps.Zipper;
using Serilog;

namespace DataCrud.DBOps.Core
{
    public class MaintenanceManager
    {
        private readonly IJobStorage _storage;
        private readonly IDatabaseProvider _provider;
        private readonly ICloudPushService _cloudPush;

        public MaintenanceManager(IJobStorage storage, IDatabaseProvider provider, ICloudPushService cloudPush = null)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _cloudPush = cloudPush;
        }

        public async Task RunAsync(string databaseName, bool backup, bool shrink, bool index, bool reorganize, bool cleanup, string backupDir = null, int retentionDays = 7, bool enableZipping = true, int? historyId = null, System.Threading.CancellationToken ct = default)
        {
            var startTime = DateTime.UtcNow;
            var history = historyId.HasValue ? await _storage.GetHistoryByIdAsync(historyId.Value) : null;
            var loggerContext = $"[DB: {databaseName}] ";

            try
            {
                // Ensure storage is initialized
                await _storage.InitializeAsync(null);
                await _storage.AddLogAsync(new AppLog(LogLevel.Information, loggerContext + "Starting maintenance job execution.", historyId));

                ct.ThrowIfCancellationRequested();

                if (index && _provider.Capabilities.HasFlag(ProviderCapabilities.Reindex))
                {
                    await UpdateStatusAsync(history, "Step: Rebuilding indexes...");
                    await _storage.AddLogAsync(new AppLog(LogLevel.Information, loggerContext + "Rebuilding indexes...", historyId));
                    await _provider.ReindexAsync(databaseName, ct);
                }

                ct.ThrowIfCancellationRequested();

                if (reorganize && _provider.Capabilities.HasFlag(ProviderCapabilities.Reorganize))
                {
                    await UpdateStatusAsync(history, "Step: Reorganizing indexes...");
                    await _storage.AddLogAsync(new AppLog(LogLevel.Information, loggerContext + "Reorganizing indexes...", historyId));
                    await _provider.ReorganizeAsync(databaseName, ct);
                }

                ct.ThrowIfCancellationRequested();

                if (shrink && _provider.Capabilities.HasFlag(ProviderCapabilities.Shrink))
                {
                    await UpdateStatusAsync(history, "Step: Shrinking database...");
                    await _storage.AddLogAsync(new AppLog(LogLevel.Information, loggerContext + "Shrinking database...", historyId));
                    await _provider.ShrinkAsync(databaseName, ct);
                }

                ct.ThrowIfCancellationRequested();

                if (backup && _provider.Capabilities.HasFlag(ProviderCapabilities.Backup))
                {
                    if (string.IsNullOrEmpty(backupDir))
                    {
                        throw new ArgumentException("Backup directory must be specified for backup jobs.", nameof(backupDir));
                    }
                    
                    await UpdateStatusAsync(history, "Step: Creating backup...");
                    await _storage.AddLogAsync(new AppLog(LogLevel.Information, loggerContext + $"Starting backup to {backupDir}...", historyId));
                    var backupFile = await _provider.BackupAsync(databaseName, backupDir, ct);

                    ct.ThrowIfCancellationRequested();

                    if (enableZipping && !string.IsNullOrEmpty(backupFile) && File.Exists(backupFile) && !backupFile.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            await UpdateStatusAsync(history, "Step: Compressing backup...");
                            await _storage.AddLogAsync(new AppLog(LogLevel.Information, loggerContext + $"Compressing backup file: {Path.GetFileName(backupFile)}", historyId));
                            var zipPath = backupFile + ".zip";
                            Log.Information("Compressing backup file: {BackupFile} to {ZipPath}", backupFile, zipPath);
                            
                            ZipBuilder.Zip(zipPath, new List<string> { backupFile });
                            
                            if (File.Exists(zipPath))
                            {
                                File.Delete(backupFile);
                                backupFile = zipPath;
                                Log.Information("Compression successful. Original file deleted.");
                                await _storage.AddLogAsync(new AppLog(LogLevel.Information, loggerContext + "Compression successful.", historyId));
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Error(ex, "Failed to compress backup file {BackupFile}", backupFile);
                            await _storage.AddLogAsync(new AppLog(LogLevel.Error, loggerContext + $"Compression failed: {ex.Message}", historyId));
                        }
                    }

                    ct.ThrowIfCancellationRequested();

                    if (_cloudPush != null && !string.IsNullOrEmpty(backupFile))
                    {
                        await UpdateStatusAsync(history, "Step: Pushing to cloud...");
                        await _storage.AddLogAsync(new AppLog(LogLevel.Information, loggerContext + "Pushing backup to cloud storage.", historyId));
                        await _cloudPush.PushAsync(backupFile, _provider.ProviderName.ToLower());
                    }
                }

                ct.ThrowIfCancellationRequested();

                if (cleanup && !string.IsNullOrEmpty(backupDir))
                {
                    await UpdateStatusAsync(history, "Step: Cleaning up old backups...");
                    await _storage.AddLogAsync(new AppLog(LogLevel.Information, loggerContext + $"Cleaning up backups older than {retentionDays} days.", historyId));
                    var cleanupService = new CleanupService(_storage);
                    await cleanupService.RunCleanupAsync(backupDir, retentionDays);
                }

                if (history != null)
                {
                    var endTime = DateTime.UtcNow;
                    var duration = endTime - startTime;
                    history.Status = JobStatus.Completed;
                    history.EndTime = endTime;
                    history.Duration = FormatDuration(duration);
                    history.Message = "Job completed successfully.";
                    await _storage.UpdateHistoryAsync(history);
                    await _storage.AddLogAsync(new AppLog(LogLevel.Information, loggerContext + $"Job completed successfully in {history.Duration}.", historyId));
                }
            }
            catch (OperationCanceledException)
            {
                Log.Warning("Maintenance run cancelled for database {DatabaseName}", databaseName);
                await _storage.AddLogAsync(new AppLog(LogLevel.Warning, loggerContext + "Job execution was cancelled.", historyId));
                if (history != null)
                {
                    history.Status = JobStatus.Cancelled;
                    history.EndTime = DateTime.UtcNow;
                    history.Message = "Job was cancelled by user.";
                    await _storage.UpdateHistoryAsync(history);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Maintenance run failed for database {DatabaseName} using provider {ProviderName}", databaseName, _provider.ProviderName);
                await _storage.AddLogAsync(new AppLog(LogLevel.Error, loggerContext + $"Job failed: {ex.Message}", historyId));
                
                if (history != null)
                {
                    history.Status = JobStatus.Failed;
                    history.EndTime = DateTime.UtcNow;
                    history.Message = $"Error: {ex.Message}";
                    await _storage.UpdateHistoryAsync(history);
                }
                throw;
            }
            finally
            {
                if (historyId.HasValue)
                {
                    Services.JobTracker.Instance.Unregister(historyId.Value);
                }
            }
        }

        private async Task UpdateStatusAsync(JobHistory history, string message)
        {
            if (history == null) return;
            history.Status = JobStatus.Running;
            history.Message = message;
            await _storage.UpdateHistoryAsync(history);
        }

        private string FormatDuration(TimeSpan ts)
        {
            if (ts.TotalDays >= 1) return $"{(int)ts.TotalDays}d {ts.Hours}h";
            if (ts.TotalHours >= 1) return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            if (ts.TotalMinutes >= 1) return $"{(int)ts.TotalMinutes}m {ts.Seconds}s";
            return $"{(int)ts.TotalSeconds}s";
        }
    }
}
