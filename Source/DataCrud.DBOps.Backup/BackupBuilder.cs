using System;
using System.Collections.Generic;
using System.IO;
using DataCrud.DBOps.AzurePush;
using DataCrud.DBOps.AwsPush;
using DataCrud.DBOps.Shared;
using DataCrud.DBOps.Shared.Extensions;
using DataCrud.DBOps.Zipper;
using DataCrud.DBOps.Zipper.Extensions;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using Serilog;

namespace DataCrud.DBOps.Backup
{
    public class BackupBuilder
    {
        public static void GenerateBackups(Server server, List<Database> databases)
        {
            Console.WriteLine("Starting generate backups...");

            foreach (var database in databases)
            {
                try
                {
                    //Create backup
                    var backupFileName = CreateFullDbBackup(server, database);

                    
                    //Create zip of backup
                    var zip = ZipBuilder.Zip(backupFileName.BakToZip(), new List<string>()
                    {
                        backupFileName
                    });


                    //Clean .bak file
                    if (AppSettings.RemoveBakFileAfterZip)
                    {
                        BackupCleaner.CleanLocalBackup(backupFileName);
                    }


                    //Push to azure storage
                    AzureBlobManager.Push(zip);

                    //Push to aws s3
                    AwsS3ObjectManager.Push(zip);

                }
                catch (Exception e)
                {
                    //write exception log
                    Log.Error(e.ToString());
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
            /* Specify whether you want to back up database or files or log */
            /* Specify the name of the database to back up */
            /* You can take backup on several media type (disk or tape), here I am
             * using File type and storing backup on the file system */
            backup.Devices.AddDevice(fileName, DeviceType.File);
            backup.BackupSetName = database.Name + "database Backup";
            backup.BackupSetDescription = database.Name + " database - Full Backup";
            /* You can specify the expiration date for your backup data
             * after that date backup data would not be relevant */
            //backup.ExpirationDate = DateTime.Today.AddDays(30);

            /* You can specify Initialize = false (default) to create a new 
             * backup set which will be appended as last backup set on the media. You
             * can specify Initialize = true to make the backup as first set on the
             * medium and to overwrite any other existing backup sets if the all the
             * backup sets have expired and specified backup set name matches with
             * the name on the medium */
            backup.Initialize = false;

            /* Wiring up events for progress monitoring */
            backup.PercentComplete += Target;
            ServerMessageEventHandler restoreComplete = Target;
            backup.Complete += restoreComplete;

            /* SqlBackup method starts to take back up
             * You can also use SqlBackupAsync method to perform the backup 
             * operation asynchronously */
            try
            {
                backup.SqlBackup(myServer);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
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
