using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DataCrud.DBOps.AzurePush;
using DataCrud.DBOps.AwsPush;
using DataCrud.DBOps.Shared;
using DataCrud.DBOps.Shared.Extensions;
using DataCrud.DBOps.Zipper;
using DataCrud.DBOps.Zipper.Extensions;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using Serilog;
using System.Configuration;

namespace DataCrud.DBOps.Backup
{
    public class BackupBuilder
    {
        public static async Task GenerateBackupsAsync(Server server, List<Database> databases)
        {
            Console.WriteLine("Starting generate backups...");

            // Use ConfigurationManager for legacy support here as per current architecture
            var azureConnectionString = ConfigurationManager.AppSettings["AzureStorageConnectionString"];

            foreach (var database in databases)
            {
                try
                {
                    //Create backup
                    var backupFileName = CreateFullDbBackup(server, database);

                    //Create zip of backup
                    var zip = ZipBuilder.Zip(backupFileName.ToZip(), new List<string>()
                    {
                        backupFileName
                    });

                    //Clean .bak file
                    if (AppSettings.RemoveBakFileAfterZip)
                    {
                        BackupCleaner.CleanLocalBackup(backupFileName);
                    }

                    //Push to azure storage
                    if (AppSettings.PushToAzureStorage)
                    {
                        var azureManager = new AzureBlobManager(azureConnectionString, AppSettings.PushToAzureStorage);
                        await azureManager.PushAsync(zip);
                    }

                    //Push to aws s3
                    if (AppSettings.PushToAwsS3Bucket)
                    {
                        var awsManager = new AwsS3ObjectManager(
                            AppSettings.AwsAccessKey,
                            AppSettings.AwsSecretKey,
                            AppSettings.S3BucketName,
                            AppSettings.S3BucketRegion,
                            AppSettings.PushToAwsS3Bucket);
                        await awsManager.PushAsync(zip);
                    }
                }
                catch (Exception e)
                {
                    //write exception log
                    Log.Error(e, "Error during GenerateBackupsAsync for database: {DatabaseName}", database.Name);
                }
            }
        }

        private static string CreateFullDbBackup(Server myServer, Database database)
        {
            Console.WriteLine($"Creating backup of {database.Name}");

            var fileName = database.GetBackupFileName(DateTime.Today);
            if (File.Exists(fileName))
            {
                File.Delete(fileName);
            }

            Microsoft.SqlServer.Management.Smo.Backup backup = new Microsoft.SqlServer.Management.Smo.Backup
            {
                Action = BackupActionType.Database,
                Database = database.Name
            };

            backup.Devices.AddDevice(fileName, DeviceType.File);
            backup.BackupSetName = database.Name + " database Backup";
            backup.BackupSetDescription = database.Name + " database - Full Backup";
            backup.Initialize = false;

            /* Wiring up events for progress monitoring */
            backup.PercentComplete += Target;
            ServerMessageEventHandler restoreComplete = Target;
            backup.Complete += restoreComplete;

            try
            {
                backup.SqlBackup(myServer);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Log.Error(e, "SQL Backup failed for {DatabaseName}", database.Name);
            }

            return fileName;
        }

        private static void Target(object sender, PercentCompleteEventArgs percentCompleteEventArgs)
        {
            Console.WriteLine($"{percentCompleteEventArgs.Percent} % completed...");
        }

        private static void Target(object sender, ServerMessageEventArgs serverMessageEventArgs)
        {
            Console.WriteLine(serverMessageEventArgs.ToString());
        }
    }
}
