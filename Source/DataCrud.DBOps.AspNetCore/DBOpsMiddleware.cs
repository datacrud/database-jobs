using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using DataCrud.DBOps.Core;
using DataCrud.DBOps.Core.Models;
using DataCrud.DBOps.Core.Storage;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using DataCrud.DBOps.Core.Services;
using System.Text.Json;
using DataCrud.DBOps.Core.Providers;
using System.Collections.Generic;
using DataCrud.DBOps.Core.Security;

namespace DataCrud.DBOps.AspNetCore
{
    public class DBOpsMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly DBOpsConfiguration _options;

        public DBOpsMiddleware(RequestDelegate next, DBOpsConfiguration options)
        {
            _next = next;
            _options = options;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var path = context.Request.Path.Value;

            if (!path.StartsWith(_options.DashboardPath, StringComparison.OrdinalIgnoreCase))
            {
                await _next(context);
                return;
            }

            // Redirect to trailing slash if missing
            if (path.Equals(_options.DashboardPath, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.Redirect(path + "/");
                return;
            }

            // Security Check
            if (!Authorize(context))
            {
                context.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
                context.Response.Headers["WWW-Authenticate"] = "Basic realm=\"DataCrud.DBOps Dashboard\"";
                return;
            }

            var subPath = path.Substring(_options.DashboardPath.Length).TrimStart('/');

            // Route handling
            if (string.IsNullOrEmpty(subPath) || subPath.Equals("index.html", StringIgnoreCase))
            {
                await ServeEmbeddedFile(context, "index.html", "text/html");
                return;
            }

            if (subPath.Equals("api/config", StringIgnoreCase))
            {
                await HandleConfigApi(context);
                return;
            }

            if (subPath.Equals("api/databases", StringIgnoreCase))
            {
                await HandleDatabasesApi(context);
                return;
            }

            if (subPath.Equals("api/history", StringIgnoreCase))
            {
                await HandleHistoryApi(context);
                return;
            }

            if (subPath.Equals("api/history/cancel", StringIgnoreCase))
            {
                await HandleCancelJobApi(context);
                return;
            }

            if (subPath.StartsWith("api/jobs/", StringIgnoreCase))
            {
                await HandleJobActionApi(context, subPath);
                return;
            }

            if (subPath.Equals("api/applog", StringIgnoreCase) || subPath.Equals("api/applogs", StringIgnoreCase))
            {
                await HandleAppLogsApi(context);
                return;
            }

            if (subPath.Equals("api/logs", StringIgnoreCase))
            {
                await HandleLogsListApi(context);
                return;
            }

            if (subPath.Equals("api/logs/content", StringIgnoreCase))
            {
                await HandleLogContentApi(context);
                return;
            }

            await _next(context);
        }

        private static readonly StringComparison StringIgnoreCase = StringComparison.OrdinalIgnoreCase;

        private bool Authorize(HttpContext context)
        {
            if (!_options.Security.Enabled)
                return true;

            var authContext = new DBOpsAuthorizationContext
            {
                Environment = context,
                IsLocal = context.Connection.RemoteIpAddress == null || context.Connection.LocalIpAddress == null || context.Connection.RemoteIpAddress.Equals(context.Connection.LocalIpAddress),
                RemoteIpAddress = context.Connection.RemoteIpAddress?.ToString(),
                GetHeader = (headerName) => context.Request.Headers[headerName].ToString()
            };

            var filters = _options.Security.AuthorizationFilters;
            
            // If no filters are provided, fall back to legacy Basic Auth behavior
            if (filters == null || filters.Count == 0)
            {
                return new BasicAuthAuthorizationFilter(_options.Security).Authorize(authContext);
            }

            foreach (var filter in filters)
            {
                if (!filter.Authorize(authContext))
                    return false;
            }

            return true;
        }

        private async Task ServeEmbeddedFile(HttpContext context, string fileName, string contentType)
        {
            var assembly = typeof(MaintenanceManager).Assembly;
            var resourceName = $"DataCrud.DBOps.Core.EmbeddedDashboard.{fileName}";

            using (var stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                    return;
                }

                context.Response.ContentType = contentType;
                await stream.CopyToAsync(context.Response.Body);
            }
        }

        private async Task HandleHistoryApi(HttpContext context)
        {
            var storage = context.RequestServices.GetRequiredService<IJobStorage>();
            var history = await storage.GetHistoryAsync(50);

            if (context.Request.Headers["HX-Request"].Count > 0)
            {
                var isJobRunning = history.Any(x => x.Status == JobStatus.Pending || x.Status == JobStatus.Running);
                var pollAttr = isJobRunning ? " hx-get='api/history' hx-trigger='every 3s' hx-swap='outerHTML'" : "";

                // Serve HTML Partial for HTMX
                var sb = new StringBuilder();
                sb.Append($"<tbody id='history-table'{pollAttr} class='bg-white divide-y divide-gray-200'>");

                foreach (var item in history)
                {
                    var statusColor = item.Status == JobStatus.Completed ? "text-green-600 bg-green-50" : 
                                     item.Status == JobStatus.Failed ? "text-red-600 bg-red-50" : 
                                     item.Status == JobStatus.Cancelled ? "text-orange-600 bg-orange-50" :
                                     item.Status == JobStatus.Pending ? "text-gray-600 bg-gray-50" : "text-blue-600 bg-blue-50";
                    
                    var statusIcon = item.Status == JobStatus.Running ? "<svg class='animate-spin -ml-1 mr-2 h-3 w-3 text-blue-600 inline' xmlns='http://www.w3.org/2000/svg' fill='none' viewBox='0 0 24 24'><circle class='opacity-25' cx='12' cy='12' r='10' stroke='currentColor' stroke-width='4'></circle><path class='opacity-75' fill='currentColor' d='M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z'></path></svg>" : 
                                     item.Status == JobStatus.Cancelled ? "<svg class='-ml-1 mr-2 h-3 w-3 text-orange-600 inline' fill='none' viewBox='0 0 24 24' stroke='currentColor'><path stroke-linecap='round' stroke-linejoin='round' stroke-width='2' d='M6 18L18 6M6 6l12 12'></path></svg>" : "";

                    sb.Append("<tr class='hover:bg-gray-50 transition'>");
                    sb.Append($"<td class='px-6 py-4 whitespace-nowrap text-sm font-bold text-indigo-600'>#{item.Id}</td>");
                    sb.Append($"<td class='px-6 py-4 whitespace-nowrap text-sm font-medium text-gray-900'>{item.DatabaseName}</td>");
                    sb.Append($"<td class='px-6 py-4 whitespace-nowrap text-sm text-gray-500'>{item.JobType}</td>");
                    sb.Append($"<td class='px-6 py-4 whitespace-nowrap text-sm text-gray-500'>{item.StartTime:yyyy-MM-dd HH:mm:ss}</td>");
                    sb.Append($"<td class='px-6 py-4 whitespace-nowrap text-sm text-gray-500'>{item.EndTime?.ToString("HH:mm:ss") ?? "-"}</td>");
                    sb.Append($"<td class='px-6 py-4 whitespace-nowrap text-sm font-medium text-indigo-600'>{item.Duration ?? "-"}</td>");
                    sb.Append($"<td class='px-6 py-4 whitespace-nowrap text-sm'>");
                    sb.Append($"<span class='px-2 inline-flex items-center text-xs leading-5 font-semibold rounded-full {statusColor}'>{statusIcon}{item.Status}</span>");
                    sb.Append("</td>");
                    sb.Append($"<td class='px-6 py-4 text-sm text-gray-500 max-w-xs truncate' title='{item.Message}'>{item.Message}</td>");
                    sb.Append($"<td class='px-6 py-4 whitespace-nowrap text-right text-sm font-medium space-x-3'>");
                    sb.Append($"<button @click=\"$dispatch('open-job-logs', {{ id: {item.Id} }})\" class='text-indigo-600 hover:text-indigo-900 font-bold'>Logs</button>");
                    if (item.Status == JobStatus.Pending || item.Status == JobStatus.Running)
                    {
                        sb.Append($"<button hx-get='api/history/cancel?id={item.Id}' hx-target='closest td' class='text-red-600 hover:text-red-900 font-bold'>Cancel</button>");
                    }
                    sb.Append("</td>");
                    sb.Append("</tr>");
                }
                
                if (!history.Any())
                {
                    sb.Append("<tr><td colspan='9' class='px-6 py-10 text-center text-gray-400 italic'>No history found yet. Start a job to see results.</td></tr>");
                }
                
                sb.Append("</tbody>");
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync(sb.ToString());
            }
            else
            {
                // Serve JSON
                context.Response.ContentType = "application/json";
                await JsonSerializer.SerializeAsync(context.Response.Body, history);
            }
        }

        private async Task HandleCancelJobApi(HttpContext context)
        {
            if (!int.TryParse(context.Request.Query["id"], out var id))
            {
                context.Response.StatusCode = 400;
                await context.Response.WriteAsync("Invalid Job ID");
                return;
            }

            var cancelled = JobTracker.Instance.Cancel(id);
            if (cancelled)
            {
                try
                {
                    var storage = context.RequestServices.GetRequiredService<IJobStorage>();
                    var historyList = await storage.GetHistoryAsync(100);
                    var item = historyList.FirstOrDefault(x => x.Id == id);
                    if (item != null && (item.Status == JobStatus.Pending || item.Status == JobStatus.Running))
                    {
                        item.Status = JobStatus.Cancelled;
                        item.EndTime = DateTime.UtcNow;
                        item.Message = "Cancellation requested by user.";
                        await storage.UpdateHistoryAsync(item);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error updating status for cancelled job {id}: {ex.Message}");
                }
            }

            if (context.Request.Headers["HX-Request"].Count > 0)
            {
                context.Response.Headers["HX-Trigger"] = "job-updated";
                await context.Response.WriteAsync(cancelled ? "<div class='alert alert-warning'>Cancellation requested...</div>" : "<div class='alert alert-info'>Job already finished or not found.</div>");
            }
            else
            {
                await context.Response.WriteAsync(cancelled ? "Cancelled" : "Not Found");
            }
        }

        private async Task HandleConfigApi(HttpContext context)
        {
            var config = new
            {
                providers = _options.Providers.Select((p, index) => new { 
                    index, 
                    name = p.ProviderName,
                    displayName = p.DisplayName,
                    capabilities = new {
                        backup = p.Capabilities.HasFlag(ProviderCapabilities.Backup),
                        shrink = p.Capabilities.HasFlag(ProviderCapabilities.Shrink),
                        reindex = p.Capabilities.HasFlag(ProviderCapabilities.Reindex),
                        reorganize = p.Capabilities.HasFlag(ProviderCapabilities.Reorganize)
                    }
                }),
                activeProviderName = _options.Providers.FirstOrDefault()?.DisplayName ?? "None",
                storageType = _options.Storage?.GetType().Name ?? "LiteDb",
                securityEnabled = _options.Security.Enabled
            };

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(config);
        }

        private async Task HandleDatabasesApi(HttpContext context)
        {
            var providerIndexStr = context.Request.Query["providerIndex"].FirstOrDefault();
            
            // Use a 10s cancellation token for the overall request, or the request aborted token
            using (var cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted))
            {
                cts.CancelAfter(TimeSpan.FromSeconds(10));
                
                try
                {
                    if (int.TryParse(providerIndexStr, out int index) && index >= 0 && index < _options.Providers.Count)
                    {
                        var provider = _options.Providers[index];
                        var databases = await provider.GetDatabasesAsync(cts.Token);
                        
                        context.Response.ContentType = "application/json";
                        await JsonSerializer.SerializeAsync(context.Response.Body, databases, cancellationToken: cts.Token);
                    }
                    else
                    {
                        // Default to first provider if none specified
                        var provider = _options.Providers.FirstOrDefault();
                        var databases = provider != null ? await provider.GetDatabasesAsync(cts.Token) : new string[] { "No Providers Configured" };
                        
                        context.Response.ContentType = "application/json";
                        await JsonSerializer.SerializeAsync(context.Response.Body, databases, cancellationToken: cts.Token);
                    }
                }
                catch (OperationCanceledException)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
                    await context.Response.WriteAsJsonAsync(new string[] { "Timeout fetching databases" });
                }
                catch (Exception ex)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    await context.Response.WriteAsJsonAsync(new string[] { $"Error: {ex.Message}" });
                }
            }
        }

        private async Task HandleJobActionApi(HttpContext context, string subPath)
        {
            if (context.Request.Method != "POST")
            {
                context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                return;
            }

            var manager = context.RequestServices.GetRequiredService<MaintenanceManager>();
            var jobPathPart = subPath.Split('/').Last();
            var dbName = "MainDB";
            if (context.Request.Query.ContainsKey("dbName")) dbName = context.Request.Query["dbName"];
            else if (context.Request.HasFormContentType && context.Request.Form.ContainsKey("dbName")) dbName = context.Request.Form["dbName"];
            else if (context.Request.Query.ContainsKey("db")) dbName = context.Request.Query["db"];

            var providerIndexStr = context.Request.Query["providerIndex"].FirstOrDefault();
            if (string.IsNullOrEmpty(providerIndexStr) && context.Request.HasFormContentType)
            {
                providerIndexStr = context.Request.Form["providerIndex"].FirstOrDefault();
            }
            int.TryParse(providerIndexStr, out int providerIndex);

            // Create history record synchronously
            var storage = context.RequestServices.GetRequiredService<IJobStorage>();
            var jobTypeEnum = ParseJobType(jobPathPart);
            var historyId = await storage.CreateHistoryAsync(new JobHistory 
            {
                DatabaseName = dbName,
                JobType = jobTypeEnum,
                StartTime = DateTime.UtcNow,
                Status = JobStatus.Pending,
                Message = $"Queued: {jobTypeEnum} operation"
            });
            // Register for cancellation BEFORE starting the task
            var ct = JobTracker.Instance.Register(historyId);

            // Run in background 
            _ = Task.Run(async () => {
                try {
                    // Resolve the specific provider from the options
                    var provider = _options.Providers.Count > providerIndex ? _options.Providers[providerIndex] : _options.Providers.FirstOrDefault();
                    if (provider == null) return;

                    // Create a manager for this specific provider run
                    var mgr = new MaintenanceManager(storage, provider);

                    switch (jobPathPart.ToLower())
                    {
                        case "backup": 
                            await mgr.RunAsync(databaseName: dbName, backup: true, shrink: false, index: false, reorganize: false, cleanup: true, backupDir: _options.BackupPath, retentionDays: 7, enableZipping: _options.EnableZipping, historyId: historyId, ct: ct); 
                            break;
                        case "shrink":
                            await mgr.RunAsync(databaseName: dbName, backup: false, shrink: true, index: false, reorganize: false, cleanup: false, historyId: historyId, ct: ct);
                            break;
                        case "reindex":
                            await mgr.RunAsync(databaseName: dbName, backup: false, shrink: false, index: true, reorganize: false, cleanup: false, historyId: historyId, ct: ct);
                            break;
                        case "reorganize":
                            await mgr.RunAsync(databaseName: dbName, backup: false, shrink: false, index: false, reorganize: true, cleanup: false, historyId: historyId, ct: ct);
                            break;
                        case "shield-secure":
                            await mgr.RunAsync(databaseName: dbName, backup: true, shrink: true, index: true, reorganize: true, cleanup: true, backupDir: _options.BackupPath, retentionDays: 7, enableZipping: _options.EnableZipping, historyId: historyId, ct: ct);
                            break;
                        case "complete":
                            await mgr.RunAsync(databaseName: dbName, backup: true, shrink: true, index: false, reorganize: true, cleanup: true, backupDir: _options.BackupPath, retentionDays: 7, enableZipping: _options.EnableZipping, historyId: historyId);
                            break;
                        default:
                            await mgr.RunAsync(databaseName: dbName, backup: (jobPathPart == "backup"), shrink: (jobPathPart == "shrink"), index: (jobPathPart == "reindex"), reorganize: (jobPathPart == "reorganize"), cleanup: false, historyId: historyId);
                            break;
                    }
                } catch (Exception ex) {
                    Console.WriteLine($"Background job execution failed for {jobPathPart} on {dbName}: {ex.Message}");
                }
            });

            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync($"<div hx-get='api/history' hx-trigger='load' hx-target='#history-table' hx-swap='outerHTML' class='bg-green-600 text-white p-4 rounded-lg shadow-xl mb-3 pointer-events-auto border border-white/20' x-data='{{ show: true }}' x-show='show' x-init='setTimeout(() => show = false, 5000)'><strong>Success!</strong> {jobTypeEnum} triggered successfully. Tracking active.</div>");
        }

        private async Task HandleAppLogsApi(HttpContext context)
        {
            var storage = context.RequestServices.GetRequiredService<IJobStorage>();
            int? jobId = null;
            if (int.TryParse(context.Request.Query["jobId"], out var id)) jobId = id;

            var logs = await storage.GetLogsAsync(100, jobId);

            if (context.Request.Headers["HX-Request"].Count > 0)
            {
                var sb = new StringBuilder();
                sb.Append("<tbody id='applogs-table' class='bg-white divide-y divide-gray-200'>");

                foreach (var log in logs)
                {
                    var levelColor = log.Level == LogLevel.Error ? "text-red-600 font-bold" : 
                                     log.Level == LogLevel.Warning ? "text-orange-600" : "text-gray-600";
                    
                    sb.Append("<tr class='hover:bg-gray-50 transition font-mono'>");
                    sb.Append($"<td class='px-6 py-4 whitespace-nowrap text-xs text-gray-400'>{log.Timestamp:yyyy-MM-dd HH:mm:ss.fff}</td>");
                    sb.Append($"<td class='px-6 py-4 whitespace-nowrap text-xs {levelColor}'>{log.Level}</td>");
                    sb.Append($"<td class='px-6 py-4 text-xs text-gray-700'>{log.Message}</td>");
                    sb.Append("</tr>");
                }

                if (!logs.Any())
                {
                    sb.Append("<tr><td colspan='3' class='px-6 py-10 text-center text-gray-400 italic'>No system logs available.</td></tr>");
                }

                sb.Append("</tbody>");
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync(sb.ToString());
            }
            else
            {
                context.Response.ContentType = "application/json";
                await JsonSerializer.SerializeAsync(context.Response.Body, logs);
            }
        }

        private JobType ParseJobType(string type)
        {
            if (string.IsNullOrEmpty(type)) return JobType.Backup;
            switch (type.ToLower())
            {
                case "shrink": return JobType.Shrink;
                case "reindex": return JobType.IndexMaintenance;
                case "reorganize": return JobType.Reorganize;
                case "cleanup": return JobType.Cleanup;
                case "backup": return JobType.Backup;
                case "shield-secure": return JobType.FullMaintenance;
                default: return JobType.Backup;
            }
        }

        private async Task HandleLogsListApi(HttpContext context)
        {
            var logsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            if (!Directory.Exists(logsDir))
            {
                context.Response.ContentType = "application/json";
                await JsonSerializer.SerializeAsync(context.Response.Body, new string[] { });
                return;
            }

            var logs = Directory.GetFiles(logsDir, "*.log")
                .Select(Path.GetFileName)
                .OrderByDescending(f => f)
                .ToList();

            context.Response.ContentType = "application/json";
            await JsonSerializer.SerializeAsync(context.Response.Body, logs);
        }

        private async Task HandleLogContentApi(HttpContext context)
        {
            var fileName = context.Request.Query["file"].FirstOrDefault();
            if (string.IsNullOrEmpty(fileName))
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                return;
            }

            // Sanitize filename to prevent directory traversal
            fileName = Path.GetFileName(fileName);
            var logsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            var filePath = Path.Combine(logsDir, fileName);

            if (!File.Exists(filePath))
            {
                context.Response.StatusCode = (int)HttpStatusCode.NotFound;
                return;
            }

            context.Response.ContentType = "text/plain";
            // Use FileShare.ReadWrite because Serilog might be holding the file open
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var reader = new StreamReader(stream))
            {
                var content = await reader.ReadToEndAsync();
                await context.Response.WriteAsync(content);
            }
        }
    }
}
