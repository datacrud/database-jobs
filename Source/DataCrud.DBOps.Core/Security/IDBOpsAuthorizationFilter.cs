using System.Collections.Generic;

namespace DataCrud.DBOps.Core.Security
{
    /// <summary>
    /// Context for authorization filters, providing access to request information.
    /// Wraps platform-specific objects (like HttpContext or OwinContext).
    /// </summary>
    public class DBOpsAuthorizationContext
    {
        public IDictionary<string, object> Items { get; } = new Dictionary<string, object>();
        
        /// <summary>
        /// The original request container (HttpContext for ASP.NET Core, IDictionary<string, object> for OWIN).
        /// </summary>
        public object Environment { get; set; }

        public bool IsLocal { get; set; }
        
        public string RemoteIpAddress { get; set; }

        /// <summary>
        /// A function to retrieve a request header in a platform-agnostic way.
        /// </summary>
        public System.Func<string, string> GetHeader { get; set; }
    }

    /// <summary>
    /// Represents a filter for authorizing dashboard requests.
    /// Inspired by Hangfire's IDashboardAuthorizationFilter.
    /// </summary>
    public interface IDBOpsAuthorizationFilter
    {
        bool Authorize(DBOpsAuthorizationContext context);
    }
}
