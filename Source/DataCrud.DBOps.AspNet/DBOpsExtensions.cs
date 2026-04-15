using System;
using Owin;
using DataCrud.DBOps.Core;

namespace DataCrud.DBOps.AspNet
{
    public static class AppBuilderExtensions
    {
        public static IAppBuilder UseDBOps(this IAppBuilder app, Action<DBOpsConfiguration> configureOptions = null)
        {
            var options = new DBOpsConfiguration();
            configureOptions?.Invoke(options);

            if (string.IsNullOrEmpty(options.ConnectionString))
            {
                throw new ArgumentException("DataCrud.DBOps connection string is required.");
            }

            app.Use<DBOpsOwinMiddleware>(options);
            return app;
        }
    }
}

