using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DataCrud.DBOps.AzurePush;
using DataCrud.DBOps.AwsPush;
using DataCrud.DBOps.Shared;

namespace DataCrud.DBOps.Backup
{
    public static class BackupCleaner
    {
        public static void CleanAllBackups()
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
                        CleanCloudBackup(directoryFile.FullName);

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


        public static bool CleanCloudBackup(string fileName)
        {
            if (AppSettings.PushToAzureStorage)
            {
                AzureBlobManager.Delete(fileName);
            }

            if (AppSettings.PushToAwsS3Bucket)
            {
                AwsS3ObjectManager.Delete(fileName);
            }

            return true;
        }


       
    }
}

