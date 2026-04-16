using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using DataCrud.DBOps.Shared.Extensions;

namespace DataCrud.DBOps.AzurePush
{
    public class AzureBlobManager
    {
        private const string BackupBlobContainerName = "backups";
        private const string BackupBlobContainerFolderName = "databases";

        private readonly string _connectionString;
        private readonly bool _isEnabled;

        public AzureBlobManager(string connectionString, bool isEnabled)
        {
            _connectionString = connectionString;
            _isEnabled = isEnabled;
        }

        private async Task<BlobContainerClient> GetContainerClientAsync()
        {
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                throw new InvalidOperationException("Azure Storage Connection String is not configured.");
            }

            BlobServiceClient blobServiceClient = new BlobServiceClient(_connectionString);
            BlobContainerClient blobContainerClient = blobServiceClient.GetBlobContainerClient(BackupBlobContainerName);

            await blobContainerClient.CreateIfNotExistsAsync();
            await blobContainerClient.SetAccessPolicyAsync(PublicAccessType.Blob);

            return blobContainerClient;
        }

        public async Task PushAsync(string filePath)
        {
            if (!_isEnabled) return;

            Console.WriteLine($"Pushing {filePath} to azure blob storage...");

            var containerClient = await GetContainerClientAsync();
            var fileName = Path.GetFileName(filePath);
            string blobName = $"{BackupBlobContainerFolderName}/{fileName}";

            BlobClient blobClient = containerClient.GetBlobClient(blobName);

            await blobClient.UploadAsync(filePath, new BlobHttpHeaders()
            {
                ContentType = fileName.GetContentType()
            });
        }

        public async Task DeleteAsync(string filePath)
        {
            if (!_isEnabled) return;

            var containerClient = await GetContainerClientAsync();
            var fileName = Path.GetFileName(filePath);
            string blobName = $"{BackupBlobContainerFolderName}/{fileName}";

            BlobClient blobClient = containerClient.GetBlobClient(blobName);
            await blobClient.DeleteIfExistsAsync();
        }
    }
}
