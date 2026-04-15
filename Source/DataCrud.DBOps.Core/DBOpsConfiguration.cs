using System;
using System.Collections.Generic;
using DataCrud.DBOps.Core.Providers;
using DataCrud.DBOps.Core.Storage;

namespace DataCrud.DBOps.Core
{
    public class DBOpsSecurityConfiguration
    {
        public bool Enabled { get; set; } = true;
        public string Username { get; set; } = "admin";
        public string Password { get; set; } = "admin123";
        public string[] AllowedRoles { get; set; }
    }

    public class DBOpsConfiguration
    {
        /// <summary>
        /// The path where the dashboard will be hosted. Default is "/dbops".
        /// </summary>
        public string DashboardPath { get; set; } = "/dbops";

        /// <summary>
        /// Security configuration for the dashboard.
        /// </summary>
        public DBOpsSecurityConfiguration Security { get; set; } = new DBOpsSecurityConfiguration();

        /// <summary>
        /// The storage provider for job history.
        /// </summary>
        public IJobStorage Storage { get; set; }

        /// <summary>
        /// Registered database providers.
        /// </summary>
        public List<IDatabaseProvider> Providers { get; set; } = new List<IDatabaseProvider>();

        /// <summary>
        /// Legacy connection string (for backward compatibility or simple setup).
        /// </summary>
        public string ConnectionString { get; set; }
    }
}
