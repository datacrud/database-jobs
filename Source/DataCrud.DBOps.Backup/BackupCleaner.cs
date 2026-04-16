using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DataCrud.DBOps.AzurePush;
using DataCrud.DBOps.AwsPush;
using DataCrud.DBOps.Shared;
using System.Configuration;

namespace DataCrud.DBOps.Backup
{
    public static class BackupCleaner
    {
        public static async Task CleanAllBackupsAsync()
        {
            //Clean older backups less then settings value
            if (AppSettings.RemoveBackupAfterXDays.HasValue && AppSettings.RemoveBackupAfterXDays > 0)
            {
                var backupRemoveTillDate = DateTime.Today.AddDays(-AppSettings.RemoveBackupAfterXDays.GetValueOrDefault());

                var backupDirectory = DirectoryProvider.GetBackupDirectory();

                FileInfo[] directoryFiles = new DirectoryInfo(backupDirectory).GetFiles();

                foreach (var directoryFile in directoryFiles)
                {
                    if (directoryFile.CreationTime.Date <= backupRemoveTillDate.Date)
                    {
                        CleanLocalBackup(directoryFile.FullName);
                        await CleanCloudBackupAsync(directoryFile.FullName);
                    }
                }
            }
        }

        public static bool CleanLocalBackup(string fileName)
        {
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }

            return true;
        }

        public static async Task<bool> CleanCloudBackupAsync(string fileName)
        {
            if (AppSettings.PushToAzureStorage)
            {
                var azureConnectionString = ConfigurationManager.AppSettings["AzureStorageConnectionString"];
                var azureManager = new AzureBlobManager(azureConnectionString, AppSettings.PushToAzureStorage);
                await azureManager.DeleteAsync(fileName);
            }

            if (AppSettings.PushToAwsS3Bucket)
            {
                var awsManager = new AwsS3ObjectManager(
                    AppSettings.AwsAccessKey,
                    AppSettings.AwsSecretKey,
                    AppSettings.S3BucketName,
                    AppSettings.S3BucketRegion,
                    AppSettings.PushToAwsS3Bucket);
                await awsManager.DeleteAsync(fileName);
            }

            return true;
        }
    }
}
