using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using DataCrud.DBOps.Core.Providers;
using DataCrud.DBOps.Core.Services;
using DataCrud.DBOps.Shared;
using DataCrud.DBOps.Shared.Extensions;
using DataCrud.DBOps.Zipper;
using DataCrud.DBOps.Zipper.Extensions;
using Serilog;

namespace DataCrud.DBOps.Backup
{
    public class BackupBuilder
    {
        public static async Task GenerateBackupsAsync(IDatabaseProvider provider, IEnumerable<string> databases, string backupDirectory, ICloudPushService cloudPush = null)
        {
            Console.WriteLine($"Starting generate backups using {provider.ProviderName} provider...");

            foreach (var database in databases)
            {
                try
                {
                    // Create backup using the provider
                    var backupFileName = await provider.BackupAsync(database, backupDirectory);

                    if (string.IsNullOrEmpty(backupFileName))
                        continue;

                    string finalFileToPush = backupFileName;

                    // Create zip of backup if it's not already zipped or a BACPAC
                    if (!backupFileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) && 
                        !backupFileName.EndsWith(".bacpac", StringComparison.OrdinalIgnoreCase))
                    {
                        var zipPath = backupFileName.ToZip();
                        ZipBuilder.Zip(zipPath, new List<string>()
                        {
                            backupFileName
                        });
                        
                        finalFileToPush = zipPath;

                        // Clean .bak file
                        if (AppSettings.RemoveBakFileAfterZip)
                        {
                            BackupCleaner.CleanLocalBackup(backupFileName);
                        }
                    }

                    // Push to cloud storage if service is provided
                    if (cloudPush != null)
                    {
                        await cloudPush.PushAsync(finalFileToPush, provider.ProviderName.ToLower());
                    }
                }
                catch (Exception e)
                {
                    Log.Error(e, "Error during GenerateBackupsAsync for database: {DatabaseName}", database);
                }
            }
        }
    }
}
