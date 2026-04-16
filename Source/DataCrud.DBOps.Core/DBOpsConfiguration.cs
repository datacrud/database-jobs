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
        /// The local path where backup files will be staged before compression and push.
        /// Defaults to a 'Backups' folder in the application root.
        /// </summary>
        public string BackupPath { get; set; } = "Backups";
        public bool PushToAzure { get; set; } = false;
        public string AzureStorageConnectionString { get; set; }
        public bool PushToAws { get; set; } = false;
        public string AwsAccessKey { get; set; }
        public string AwsSecretKey { get; set; }
        public string AwsBucketName { get; set; }
        public string AwsRegion { get; set; }

        /// <summary>
        /// Legacy connection string (for backward compatibility or simple setup).
        /// </summary>
        public string ConnectionString { get; set; }
    }
}
