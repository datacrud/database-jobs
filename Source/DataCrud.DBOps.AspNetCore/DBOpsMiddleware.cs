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
using System.Text.Json;
using DataCrud.DBOps.Core.Providers;

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

            if (subPath.StartsWith("api/jobs/", StringComparison.OrdinalIgnoreCase))
            {
                await HandleJobActionApi(context, subPath);
                return;
            }

            await _next(context);
        }

        private bool Authorize(HttpContext context)
        {
            if (!_options.Security.Enabled)
                return true;

            if (!context.Request.Headers.ContainsKey("Authorization"))
                return false;

            var authHeader = context.Request.Headers["Authorization"].ToString();
            if (authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                var encoded = authHeader.Substring(6);
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                var parts = decoded.Split(':');
                if (parts.Length == 2)
                {
                    var user = parts[0];
                    var pass = parts[1];
                    return user == _options.Security.Username && pass == _options.Security.Password;
                }
            }

            return false;
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
                // Serve HTML Partial for HTMX
                var sb = new StringBuilder();
                foreach (var item in history)
                {
                    var statusColor = item.Status == JobStatus.Completed ? "text-green-600 bg-green-50" : 
                                     item.Status == JobStatus.Failed ? "text-red-600 bg-red-50" : "text-blue-600 bg-blue-50";
                    
                    sb.Append("<tr class='hover:bg-gray-50 transition'>");
                    sb.Append($"<td class='px-6 py-4 whitespace-nowrap text-sm font-medium text-gray-900'>{item.DatabaseName}</td>");
                    sb.Append($"<td class='px-6 py-4 whitespace-nowrap text-sm text-gray-500'>{item.JobType}</td>");
                    sb.Append($"<td class='px-6 py-4 whitespace-nowrap text-sm text-gray-500'>{item.StartTime:yyyy-MM-dd HH:mm:ss}</td>");
                    sb.Append($"<td class='px-6 py-4 whitespace-nowrap text-sm'>");
                    sb.Append($"<span class='px-2 inline-flex text-xs leading-5 font-semibold rounded-full {statusColor}'>{item.Status}</span>");
                    sb.Append("</td>");
                    sb.Append($"<td class='px-6 py-4 text-sm text-gray-500 max-w-xs truncate' title='{item.Message}'>{item.Message}</td>");
                    sb.Append("</tr>");
                }
                
                if (!history.Any())
                {
                    sb.Append("<tr><td colspan='5' class='px-6 py-10 text-center text-gray-400 italic'>No history found yet. Start a job to see results.</td></tr>");
                }
                
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
                        reindex = p.Capabilities.HasFlag(ProviderCapabilities.Reindex)
                    }
                }),
                storageType = _options.Storage?.GetType().Name ?? "LiteDB",
                securityEnabled = _options.Security.Enabled
            };

            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(config);
        }

        private System.Collections.Generic.List<string> GetCapabilitiesList(IDatabaseProvider provider)
        {
            var list = new System.Collections.Generic.List<string>();
            if (provider.Capabilities.HasFlag(ProviderCapabilities.Backup)) list.Add("Backup");
            if (provider.Capabilities.HasFlag(ProviderCapabilities.Shrink)) list.Add("Shrink");
            if (provider.Capabilities.HasFlag(ProviderCapabilities.Reindex)) list.Add("Reindex");
            return list;
        }

        private async Task HandleDatabasesApi(HttpContext context)
        {
            var providerIndexStr = context.Request.Query["providerIndex"].FirstOrDefault();
            if (int.TryParse(providerIndexStr, out int index) && index >= 0 && index < _options.Providers.Count)
            {
                var provider = _options.Providers[index];
                var databases = await provider.GetDatabasesAsync();
                
                context.Response.ContentType = "application/json";
                await JsonSerializer.SerializeAsync(context.Response.Body, databases);
            }
            else
            {
                // Default to first provider if none specified
                var provider = _options.Providers.FirstOrDefault();
                var databases = provider != null ? await provider.GetDatabasesAsync() : new string[] { "No Providers Configured" };
                
                context.Response.ContentType = "application/json";
                await JsonSerializer.SerializeAsync(context.Response.Body, databases);
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
            var jobType = subPath.Split('/').Last();
            var dbName = context.Request.Query["db"].FirstOrDefault() ?? "MainDB";
            var providerIndexStr = context.Request.Query["providerIndex"].FirstOrDefault();
            int.TryParse(providerIndexStr, out int providerIndex);

            // Run in background 
            _ = Task.Run(async () => {
                try {
                    // Resolve the specific provider from the options
                    var provider = _options.Providers.Count > providerIndex ? _options.Providers[providerIndex] : _options.Providers.FirstOrDefault();
                    if (provider == null) return;

                    // Create a manager for this specific provider run
                    var storage = context.RequestServices.GetRequiredService<IJobStorage>();
                    var instanceManager = new MaintenanceManager(storage, provider);

                    switch (jobType.ToLower())
                    {
                        case "backup": await instanceManager.RunAsync(dbName, true, false, false, true, "C:\\Backups", 7); break;
                        case "shrink": await instanceManager.RunAsync(dbName, false, true, false, false); break;
                        case "index": await instanceManager.RunAsync(dbName, false, false, true, false); break;
                    }
                } catch (Exception ex) {
                    Console.WriteLine($"Background job execution failed for {jobType} on {dbName}: {ex.Message}");
                }
            });

            context.Response.ContentType = "text/html";
            await context.Response.WriteAsync("<div class='fixed bottom-4 right-4 bg-green-600 text-white p-4 rounded-lg shadow-xl' x-data='{ show: true }' x-show='show' x-init='setTimeout(() => show = false, 3000)'>Job triggered successfully! Check history in a moment.</div>");
        }
    }
}
