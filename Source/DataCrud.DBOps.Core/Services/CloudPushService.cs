using System;
using System.IO;
using System.Threading.Tasks;
using DataCrud.DBOps.AzurePush;
using DataCrud.DBOps.AwsPush;
using DataCrud.DBOps.Zipper;
using DataCrud.DBOps.Zipper.Extensions;
using Serilog;

namespace DataCrud.DBOps.Core.Services
{
    public class CloudPushService : ICloudPushService
    {
        private readonly DBOpsConfiguration _config;

        public CloudPushService(DBOpsConfiguration config)
        {
            _config = config;
        }

        public async Task PushAsync(string filePath, string providerPrefix)
        {
            if (!_config.PushToAzure && !_config.PushToAws)
            {
                return;
            }

            try
            {
                // Ensure the file exists
                if (!File.Exists(filePath))
                {
                    Log.Error($"Backup file not found for cloud push: {filePath}");
                    return;
                }

                // Create zip if not already zipped
                string zipPath = filePath;
                bool isTemporaryZip = false;

                if (!filePath.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    Log.Information($"Zipping backup file: {filePath}");
                    zipPath = filePath.ToZip(); // Extension method from Zipper
                    ZipBuilder.Zip(zipPath, new System.Collections.Generic.List<string> { filePath });
                    isTemporaryZip = true;
                }

                // Push to Azure
                if (_config.PushToAzure)
                {
                    Log.Information($"Pushing {zipPath} to Azure Blob Storage...");
                    var azureManager = new AzureBlobManager(_config.AzureStorageConnectionString, _config.PushToAzure);
                    await azureManager.PushAsync(zipPath);
                }

                // Push to AWS
                if (_config.PushToAws)
                {
                    Log.Information($"Pushing {zipPath} to AWS S3...");
                    var awsManager = new AwsS3ObjectManager(
                        _config.AwsAccessKey, 
                        _config.AwsSecretKey, 
                        _config.AwsBucketName, 
                        _config.AwsRegion, 
                        _config.PushToAws);
                    await awsManager.PushAsync(zipPath);
                }

                // Cleanup temporary zip if we created one
                if (isTemporaryZip && File.Exists(zipPath))
                {
                    // File.Delete(zipPath); // Optional: depends on user preference for local retention
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error pushing backup to cloud storage.");
                throw;
            }
        }
    }
}
