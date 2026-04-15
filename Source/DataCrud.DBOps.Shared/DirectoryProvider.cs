using System;
using System.IO;

namespace DataCrud.DBOps.Shared
{
    public class DirectoryProvider
    {
        public static string GetBackupDirectory()
        {
            var isUseBaseBackupDirectory = AppSettings.UseRootBackupDirectory;

            var backupDirectory = isUseBaseBackupDirectory
                ? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Backups\\")
                : AppSettings.BackupDirectoryPath;

            string directory = CheckDirectory(backupDirectory);


            return directory;
        }


        private static string CheckDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
            }

            return directoryPath;
        }

    }
}
