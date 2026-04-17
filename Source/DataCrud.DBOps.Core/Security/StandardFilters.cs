using System;
using System.Linq;

namespace DataCrud.DBOps.Core.Security
{
    /// <summary>
    /// A security filter that only allows requests from the local machine.
    /// This is the default behavior used by many dashboards like Hangfire.
    /// </summary>
    public class LocalRequestsOnlyAuthorizationFilter : IDBOpsAuthorizationFilter
    {
        public bool Authorize(DBOpsAuthorizationContext context)
        {
            // If we're not sure, it's safer to deny
            if (context == null) return false;

            // In some environments, IsLocal might be already populated
            if (context.IsLocal) return true;

            // Check IP address
            var ip = context.RemoteIpAddress;
            if (string.IsNullOrEmpty(ip)) return false;

            return ip == "127.0.0.1" || ip == "::1" || ip == "localhost";
        }
    }

    /// <summary>
    /// A security filter that allows all requests. Use only for testing.
    /// </summary>
    public class AllowAllAuthorizationFilter : IDBOpsAuthorizationFilter
    {
        public bool Authorize(DBOpsAuthorizationContext context) => true;
    }
}
