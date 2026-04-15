using System;
using System.Configuration;

namespace DataCrud.DBOps.Shared
{
    public class AppSettings
    {
        public static string ServerName => ConfigurationManager.AppSettings["ServerName"];
        public static bool EnableShrink => Convert.ToBoolean(ConfigurationManager.AppSettings["EnableShrink"]);
        public static bool EnableIndexMaintenance => Convert.ToBoolean(ConfigurationManager.AppSettings["EnableIndexMaintenance"]);
        public static bool BackupAllDatabases => Convert.ToBoolean(ConfigurationManager.AppSettings["BackupAllDatabases"]);
        public static string BackupDatabases => ConfigurationManager.AppSettings["BackupDatabases"];
        public static bool UseRootBackupDirectory => Convert.ToBoolean(ConfigurationManager.AppSettings["UseRootBackupDirectory"]);
        public static string BackupDirectoryPath => ConfigurationManager.AppSettings["BackupDirectoryPath"];
        public static bool PushToAzureStorage => Convert.ToBoolean(ConfigurationManager.AppSettings["PushToAzureStorage"]);
        public static bool RemoveBakFileAfterZip => Convert.ToBoolean(ConfigurationManager.AppSettings["RemoveBakFileAfterZip"]);
        public static int? RemoveBackupAfterXDays => GetRemoveBackupAfterDays();


        private static int? GetRemoveBackupAfterDays()
        {
            var backupRemoveDays = ConfigurationManager.AppSettings["RemoveBackupAfterXDays"];
            if(string.IsNullOrWhiteSpace(backupRemoveDays)) return null;

            int.TryParse(backupRemoveDays, out int days);
            return days;
        }


        public static string AwsAccessKey => ConfigurationManager.AppSettings["AwsAccessKey"];
        public static string AwsSecretKey => ConfigurationManager.AppSettings["AwsSecretKey"];
        public static string S3BucketName => ConfigurationManager.AppSettings["S3BucketName"];
        public static string S3BucketRegion => ConfigurationManager.AppSettings["S3BucketRegion"];
        public static bool PushToAwsS3Bucket => Convert.ToBoolean(ConfigurationManager.AppSettings["PushToAwsS3Bucket"]);
    }
}

