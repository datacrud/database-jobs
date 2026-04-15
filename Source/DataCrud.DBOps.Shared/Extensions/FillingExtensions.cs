using System;
using System.IO;
using DataCrud.DBOps.Shared.Enums;
using Microsoft.SqlServer.Management.Smo;

namespace DataCrud.DBOps.Shared.Extensions
{
    public static class FillingExtensions
    {
        public static string GetBackupFileName(this Database database, DateTime date)
        {
            string directory = DirectoryProvider.GetBackupDirectory();

            string day = date.Day < 10 ? $"0{date.Day}" : $"{date.Day}";
            string month = date.Month < 10 ? $"0{date.Month}" : $"{date.Month}";


            string fileName = directory + database.Name + "_" + date.Date.Year + "-" + month + "-" + day + ".bak";

            return fileName;
        }

        public static string GetContentType(this string fileName)
        {
            var fileExtension = Path.GetExtension(fileName);
            
            string contentType = "";

            if (fileExtension != null && fileExtension.ToLower().Equals($".{nameof(BackupFileType.Bak).ToLower()}"))
            {
                contentType = "application/octet-stream";
            }
            else if (fileExtension != null && fileExtension.ToLower().Equals($".{nameof(BackupFileType.Zip).ToLower()}"))
            {
                contentType = "application/zip";
            }

            return contentType;
        }
    }
}

