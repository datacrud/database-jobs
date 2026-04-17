using System;
using System.Text;
using System.Linq;
using DataCrud.DBOps.Core.Security;

namespace DataCrud.DBOps.Core.Security
{
    /// <summary>
    /// Default authorization filter providing Basic Authentication.
    /// Backward compatible with the legacy Username/Password settings.
    /// </summary>
    public class BasicAuthAuthorizationFilter : IDBOpsAuthorizationFilter
    {
        private readonly DBOpsSecurityConfiguration _config;

        public BasicAuthAuthorizationFilter(DBOpsSecurityConfiguration config)
        {
            _config = config;
        }

        public bool Authorize(DBOpsAuthorizationContext context)
        {
            if (!_config.Enabled) return true;

            var authHeader = context.GetHeader?.Invoke("Authorization");
            if (string.IsNullOrEmpty(authHeader)) return false;

            if (authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var encoded = authHeader.Substring(6);
                    var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
                    var parts = decoded.Split(':');
                    if (parts.Length == 2)
                    {
                        var user = parts[0];
                        var pass = parts[1];
                        return user == _config.Username && pass == _config.Password;
                    }
                }
                catch
                {
                    return false;
                }
            }

            return false;
        }
    }
}
