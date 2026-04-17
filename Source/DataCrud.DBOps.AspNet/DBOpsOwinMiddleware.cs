using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using DataCrud.DBOps.Core.Services;
using System.Text;
using System.Threading.Tasks;
using DataCrud.DBOps.Core;
using DataCrud.DBOps.Core.Models;
using DataCrud.DBOps.Core.Storage;
using DataCrud.DBOps.Core.Providers;
using DataCrud.DBOps.Core.Security;
using Microsoft.Owin;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace DataCrud.DBOps.AspNet
{
    public class DBOpsOwinMiddleware : OwinMiddleware
    {
        private readonly DBOpsConfiguration _options;

        public DBOpsOwinMiddleware(OwinMiddleware next, DBOpsConfiguration options) : base(next)
        {
            _options = options;
        }

        public override async Task Invoke(IOwinContext context)
        {
            var path = context.Request.Path.Value;

            if (!path.StartsWith(_options.DashboardPath, StringComparison.OrdinalIgnoreCase))
            {
                await Next.Invoke(context);
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
                await context.Response.WriteAsync("Forbidden: You do not have permission to access the DBOps Dashboard.");
                return;
            }

            var subPath = path.Substring(_options.DashboardPath.Length).TrimStart('/');

            // Route handling
            if (string.IsNullOrEmpty(subPath) || subPath.Equals("index.html", StringComparison.OrdinalIgnoreCase))
            {
                await ServeEmbeddedFile(context, "index.html", "text/html");
                return;
            }

            if (subPath.Equals("api/config", StringComparison.OrdinalIgnoreCase))
            {
                await HandleConfigApi(context);
                return;
            }

            if (subPath.Equals("api/databases", StringComparison.OrdinalIgnoreCase))
            {
                await HandleDatabasesApi(context);
                return;
            }

            if (subPath.Equals("api/history", StringComparison.OrdinalIgnoreCase))
            {
                await HandleHistoryApi(context);
                return;
            }

            if (subPath.Equals("api/history/cancel", StringComparison.OrdinalIgnoreCase))
            {
                await HandleCancelJobApi(context);
                return;
            }

            if (subPath.Equals("api/applog", StringComparison.OrdinalIgnoreCase))
            {
                await HandleAppLogsApi(context);
                return;
            }

            if (subPath.StartsWith("api/jobs/", StringComparison.OrdinalIgnoreCase))
            {
                await HandleJobActionApi(context, subPath);
                return;
            }

            await Next.Invoke(context);
        }

        private bool Authorize(IOwinContext context)
        {
            var filters = _options.Security.AuthorizationFilters;
            if (filters == null || !filters.Any())
            {
                return !_options.Security.Enabled;
            }

            var authContext = new DBOpsAuthorizationContext
            {
                Environment = context.Environment,
                RemoteIpAddress = context.Request.RemoteIpAddress,
                GetHeader = (headerName) => 
                {
                    if (context.Request.Headers.TryGetValue(headerName, out var values) && values.Length > 0)
                        return values[0];
                    return null;
                }
            };

            return filters.All(filter => filter.Authorize(authContext));
        }

        private async Task ServeEmbeddedFile(IOwinContext context, string fileName, string contentType)
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

        private async Task HandleHistoryApi(IOwinContext context)
        {
            var storage = _options.Storage ?? new LiteDbJobStorage("jobs_dbops.db");
            var history = await storage.GetHistoryAsync(50);

            if (context.Request.Headers.Get("HX-Request") != null)
            {
                var isJobRunning = history.Any(x => x.Status == JobStatus.Pending || x.Status == JobStatus.Running);
                var pollAttr = isJobRunning ? " hx-get='api/history' hx-trigger='every 3s' hx-swap='outerHTML'" : "";

                var sb = new StringBuilder();
                sb.Append($"<tbody id='history-table'{pollAttr} class='bg-white divide-y divide-gray-200'>");

                foreach (var item in history)
                {
                    var statusColor = item.Status == JobStatus.Completed ? "text-green-600 bg-green-50" : 
                                     item.Status == JobStatus.Failed ? "text-red-600 bg-red-50" : 
                                     item.Status == JobStatus.Cancelled ? "text-orange-600 bg-orange-50" :
                                     item.Status == JobStatus.Pending ? "text-gray-600 bg-gray-50" : "text-blue-600 bg-blue-50";
                    
                    var statusIcon = item.Status == JobStatus.Running ? "<svg class='animate-spin -ml-1 mr-2 h-3 w-3 text-blue-600 inline' xmlns='http://www.w3.org/2000/svg' fill='none' viewBox='0 0 24 24'><circle class='opacity-25' cx='12' cy='12' r='10' stroke='currentColor' stroke-width='4'></circle><path class='opacity-75' fill='currentColor' d='M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z'></path></svg>" : 
                                     item.Status == JobStatus.Completed ? "<svg class='-ml-1 mr-2 h-3 w-3 text-green-600 inline' fill='none' viewBox='0 0 24 24' stroke='currentColor'><path stroke-linecap='round' stroke-linejoin='round' stroke-width='2' d='M5 13l4 4L19 7'></path></svg>" :
                                     item.Status == JobStatus.Cancelled ? "<svg class='-ml-1 mr-2 h-3 w-3 text-orange-600 inline' fill='none' viewBox='0 0 24 24' stroke='currentColor'><path stroke-linecap='round' stroke-linejoin='round' stroke-width='2' d='M6 18L18 6M6 6l12 12'></path></svg>" : "";

                    sb.Append("<tr class='hover:bg-gray-50 transition'>");
                    sb.Append($"<td class='px-6 py-4 whitespace-nowrap text-xs text-gray-400 font-mono'>#{item.Id}</td>");
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
                    sb.Append("<tr><td colspan='8' class='px-6 py-10 text-center text-gray-400 italic'>No history found yet. Start a job to see results.</td></tr>");
                }

                sb.Append("</tbody>");
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync(sb.ToString());
            }
            else
            {
                context.Response.ContentType = "application/json";
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(history);
                await context.Response.WriteAsync(json);
            }
        }

        private async Task HandleCancelJobApi(IOwinContext context)
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
                    var storage = _options.Storage ?? new LiteDbJobStorage("jobs_dbops.db");
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

            if (context.Request.Headers.Get("HX-Request") != null)
            {
                context.Response.Headers["HX-Trigger"] = "job-updated";
                await context.Response.WriteAsync(cancelled ? "<div class='alert alert-warning'>Cancellation requested...</div>" : "<div class='alert alert-info'>Job already finished or not found.</div>");
            }
            else
            {
                await context.Response.WriteAsync(cancelled ? "Cancelled" : "Not Found");
            }
        }

        private async Task HandleConfigApi(IOwinContext context)
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

            var settings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
            var json = JsonConvert.SerializeObject(config, settings);
            
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(json);
        }

        private System.Collections.Generic.List<string> GetCapabilities(IDatabaseProvider provider)
        {
            var capabilitiesList = new System.Collections.Generic.List<string>();
            if (provider.Capabilities.HasFlag(ProviderCapabilities.Backup)) capabilitiesList.Add("Backup");
            if (provider.Capabilities.HasFlag(ProviderCapabilities.Shrink)) capabilitiesList.Add("Shrink");
            if (provider.Capabilities.HasFlag(ProviderCapabilities.Reindex)) capabilitiesList.Add("Reindex");
            return capabilitiesList;
        }

        private async Task HandleDatabasesApi(IOwinContext context)
        {
            var providerIndexStr = context.Request.Query["providerIndex"];
            
            // Use a 10s cancellation token for the overall request, or the request aborted token
            using (var cts = System.Threading.CancellationTokenSource.CreateLinkedTokenSource(context.Request.CallCancelled))
            {
                cts.CancelAfter(TimeSpan.FromSeconds(10));
                
                try
                {
                    if (int.TryParse(providerIndexStr, out int providerIndex) && providerIndex >= 0 && providerIndex < _options.Providers.Count)
                    {
                        var provider = _options.Providers[providerIndex];
                        var databases = await provider.GetDatabasesAsync(cts.Token);
                        
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsync(Newtonsoft.Json.JsonConvert.SerializeObject(databases));
                    }
                    else
                    {
                        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                        await context.Response.WriteAsync("Invalid provider index");
                    }
                }
                catch (OperationCanceledException)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.RequestTimeout;
                    await context.Response.WriteAsync(Newtonsoft.Json.JsonConvert.SerializeObject(new string[] { "Timeout fetching databases" }));
                }
                catch (Exception ex)
                {
                    context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                    await context.Response.WriteAsync(Newtonsoft.Json.JsonConvert.SerializeObject(new string[] { $"Error: {ex.Message}" }));
                }
            }
        }

        private async Task HandleJobActionApi(IOwinContext context, string subPath)
        {
            if (context.Request.Method != "POST")
            {
                context.Response.StatusCode = (int)HttpStatusCode.MethodNotAllowed;
                return;
            }

            var form = await context.Request.ReadFormAsync();
            var providerIndexStr = context.Request.Query["providerIndex"] ?? form["providerIndex"];
            int.TryParse(providerIndexStr, out int providerIndex);

            if (providerIndex < 0 || providerIndex >= _options.Providers.Count)
            {
                context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                await context.Response.WriteAsync("Invalid provider index");
                return;
            }

            var databaseName = context.Request.Query["dbName"] ?? form["dbName"] ?? 
                              context.Request.Query["database"] ?? form["database"] ?? 
                              context.Request.Query["db"] ?? "MainDB";

            if (string.IsNullOrEmpty(databaseName) || databaseName == "MainDB")
            {
                // Fallback for empty selections
            }

            var storage = _options.Storage ?? new LiteDbJobStorage("jobs_dbops.db");
            var provider = _options.Providers[providerIndex];
            
            var jobType = subPath.Split('/').Last();
            var jobTypeEnum = ParseJobType(jobType);

            // Create history record synchronously
            var historyId = await storage.CreateHistoryAsync(new JobHistory 
            {
                DatabaseName = databaseName,
                JobType = jobTypeEnum,
                StartTime = DateTime.UtcNow,
                Status = JobStatus.Pending,
                Message = $"Queued: {jobType} operation"
            });

            // Register for cancellation BEFORE starting the task
            var ct = JobTracker.Instance.Register(historyId);

            _ = Task.Run(async () => {
                try {
                    var manager = new MaintenanceManager(storage: storage, provider: provider);
                    switch (jobType.ToLower())
                    {
                        case "backup": await manager.RunAsync(databaseName, true, false, false, false, true, _options.BackupPath, 7, _options.EnableZipping, historyId, ct); break;
                        case "shrink": await manager.RunAsync(databaseName, false, true, false, false, false, historyId: historyId, ct: ct); break;
                        case "reindex": await manager.RunAsync(databaseName, false, false, true, false, false, historyId: historyId, ct: ct); break;
                        case "reorganize": await manager.RunAsync(databaseName, false, false, false, true, false, historyId: historyId, ct: ct); break;
                        case "shield-secure":
                        case "complete": await manager.RunAsync(databaseName, true, true, true, true, true, _options.BackupPath, 7, _options.EnableZipping, historyId, ct); break;
                        default: await manager.RunAsync(databaseName, (jobType == "backup"), (jobType == "shrink"), (jobType == "reindex"), (jobType == "reorganize"), false, historyId: historyId, ct: ct); break;
                    }
                } catch { }
            });

            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync($"<div hx-get='api/history' hx-trigger='load' hx-target='#history-table' hx-swap='outerHTML' class='bg-green-600 text-white p-4 rounded-lg shadow-xl mb-3 pointer-events-auto border border-white/20' x-data='{{ show: true }}' x-show='show' x-init='setTimeout(() => show = false, 5000)'><strong>Success!</strong> {jobType} triggered successfully. Tracking active.</div>");
        }

        private JobType ParseJobType(string type)
        {
            if (string.IsNullOrEmpty(type)) return JobType.Backup;
            switch (type.ToLower())
            {
                case "shrink": return JobType.Shrink;
                case "index":
                case "reindex": return JobType.IndexMaintenance;
                case "reorganize": return JobType.Reorganize;
                case "cleanup": return JobType.Cleanup;
                case "complete":
                case "shield-secure": return JobType.Backup;
                default: return JobType.Backup;
            }
        }

        private async Task HandleAppLogsApi(IOwinContext context)
        {
            var storage = _options.Storage ?? new LiteDbJobStorage("jobs_dbops.db");
            int? jobId = null;
            if (int.TryParse(context.Request.Query["jobId"], out var id)) jobId = id;

            var logs = await storage.GetLogsAsync(100, jobId);
            // ... (rest of the code depends on the JSON serialization)

            context.Response.ContentType = "application/json";
            var settings = new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() };
            var json = JsonConvert.SerializeObject(logs, settings);
            await context.Response.WriteAsync(json);
        }
    }

}

