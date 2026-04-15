using System;
using System.Collections.Generic;

namespace DataCrud.DBOps.Core.Models
{
    public class MaintenanceContext
    {
        public string ServerName { get; set; }
        public bool UseIntegratedSecurity { get; set; } = true;
        public string Username { get; set; }
        public string Password { get; set; }
        
        public List<string> TargetDatabases { get; set; } = new List<string>();
        public bool AllDatabases { get; set; } = false;
        
        public string BackupDirectory { get; set; }
        public int BackupRetentionDays { get; set; } = 7;
        
        public bool EnableShrink { get; set; } = true;
        public bool EnableIndexMaintenance { get; set; } = true;
        public bool RemoveBakAfterZip { get; set; } = true;
    }

    public enum JobStatus
    {
        Pending,
        Running,
        Completed,
        Failed
    }

    public enum JobType
    {
        Backup,
        Shrink,
        IndexMaintenance,
        Cleanup
    }

    public class JobHistory
    {
        public int Id { get; set; }
        public string DatabaseName { get; set; }
        public JobType JobType { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public JobStatus Status { get; set; }
        public string Message { get; set; }
        public string Details { get; set; }
    }
}

