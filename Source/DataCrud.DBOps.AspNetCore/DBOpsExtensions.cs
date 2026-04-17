using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using DataCrud.DBOps.Core;
using DataCrud.DBOps.Core.Storage;
using DataCrud.DBOps.Core.Providers;
using System;
using System.Collections.Generic;

namespace DataCrud.DBOps.AspNetCore
{
    public static class DBOpsExtensions
    {
        public static IServiceCollection AddDBOps(this IServiceCollection services, Action<DBOpsConfiguration> configureOptions)
        {
            var options = new DBOpsConfiguration();
            configureOptions?.Invoke(options);
            
            services.AddSingleton(options);
            
            if (options.Storage != null)
            {
                services.AddSingleton<IJobStorage>(options.Storage);
            }

            services.AddSingleton<DataCrud.DBOps.Core.Services.ICloudPushService, DataCrud.DBOps.Core.Services.CloudPushService>();

            services.AddScoped<MaintenanceManager>(sp => 
            {
                var storage = sp.GetRequiredService<IJobStorage>();
                var cloudPush = sp.GetRequiredService<DataCrud.DBOps.Core.Services.ICloudPushService>();
                
                // Use the first provider as default for the registered manager
                var provider = options.Providers.Count > 0 ? options.Providers[0] : null;
                
                return new MaintenanceManager(storage, provider, cloudPush);
            });
            
            return services;
        }

        public static IApplicationBuilder UseDBOps(this IApplicationBuilder app)
        {
            var options = app.ApplicationServices.GetRequiredService<DBOpsConfiguration>();
            app.UseMiddleware<DBOpsMiddleware>(options);
            return app;
        }
    }
}
