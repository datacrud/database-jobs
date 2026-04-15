using System;
using System.Configuration;
using System.IO;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DataCrud.DBOps.Shared;
using DataCrud.DBOps.Shared.Enums;
using DataCrud.DBOps.Shared.Extensions;

namespace DataCrud.DBOps.AzurePush
{
    public class AzureBlobManager
    {
        private const string BackupBlobContainerName = "backups";
        private const string BackupBlobContainerFolderName = "databases";


        private static BlobContainerClient CloudBlobContainer()
        {
            ConnectionStringSettingsCollection connectionStringSettings = ConfigurationManager.ConnectionStrings;
            string storageConnection = connectionStringSettings[1].ConnectionString;

            BlobServiceClient blobServiceClient = new BlobServiceClient(storageConnection);

            //create a container
            BlobContainerClient blobContainerClient = blobServiceClient.GetBlobContainerClient(BackupBlobContainerName);

            //create a container if it is not already exists
            blobContainerClient.CreateIfNotExists();

            //set permission to public
            blobContainerClient.SetAccessPolicy(PublicAccessType.Blob);


            return blobContainerClient;
        }


        public static void Push(string filePath)
        {
            if(!AppSettings.PushToAzureStorage) return;

            Console.WriteLine($"Pushing {filePath} to azure blob storage...");

            var blobContainer = CloudBlobContainer();

            
            var fileName = Path.GetFileName(filePath);


            string blobName = $"{BackupBlobContainerFolderName}/{fileName}";

            //get Blob reference

            BlobClient blob = blobContainer.GetBlobClient(blobName);

            blob.Upload(filePath, new BlobHttpHeaders()
            {
                ContentType = fileName.GetContentType()
            });

        }


        public static void Delete(string filePath)
        {
            if (!AppSettings.PushToAzureStorage) return;

            var blobContainer = CloudBlobContainer();

            var fileName = Path.GetFileName(filePath);

            string blobName = $"{BackupBlobContainerFolderName}/{fileName}";

            //get blob reference

            BlobClient blob = blobContainer.GetBlobClient(blobName);
            blob.DeleteIfExists();
            
        }

    }
}

