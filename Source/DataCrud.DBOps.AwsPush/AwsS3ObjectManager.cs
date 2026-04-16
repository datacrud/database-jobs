using System;
using System.IO;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using System.Threading.Tasks;
using DataCrud.DBOps.Shared.Extensions;
using Serilog;

namespace DataCrud.DBOps.AwsPush
{
    public class AwsS3ObjectManager
    {
        private const string FolderPath = "backups/databases/";
        private readonly string _bucketName;
        private readonly RegionEndpoint _bucketRegion;
        private readonly string _accessKey;
        private readonly string _secretKey;
        private readonly bool _isEnabled;

        public AwsS3ObjectManager(string accessKey, string secretKey, string bucketName, string region, bool isEnabled)
        {
            _accessKey = accessKey;
            _secretKey = secretKey;
            _bucketName = bucketName;
            _bucketRegion = string.IsNullOrEmpty(region) ? RegionEndpoint.USEast1 : RegionEndpoint.GetBySystemName(region);
            _isEnabled = isEnabled;
        }

        private IAmazonS3 GetS3Client()
        {
            if (string.IsNullOrEmpty(_accessKey) || string.IsNullOrEmpty(_secretKey))
            {
                throw new InvalidOperationException("AWS credentials are not configured.");
            }
            return new AmazonS3Client(new BasicAWSCredentials(_accessKey, _secretKey), _bucketRegion);
        }

        public async Task PushAsync(string filePath)
        {
            if (!_isEnabled) return;

            Console.WriteLine($"Pushing {filePath} to aws s3...");

            using (var s3Client = GetS3Client())
            {
                var keyName = $"{FolderPath}{Path.GetFileName(filePath)}";

                try
                {
                    var request = new PutObjectRequest
                    {
                        BucketName = _bucketName,
                        Key = keyName,
                        FilePath = filePath,
                        ContentType = keyName.GetContentType()
                    };

                    request.Metadata.Add("x-amz-meta-title", "database backup");

                    if (!await AmazonS3Util.DoesS3BucketExistV2Async(s3Client, _bucketName))
                    {
                        await s3Client.PutBucketAsync(_bucketName);
                    }

                    await s3Client.PutObjectAsync(request);
                }
                catch (AmazonS3Exception e)
                {
                    Console.WriteLine($"Error encountered on AWS S3 server. Message:'{e.Message}' when writing an object");
                    Log.Error(e, "AWS S3 Error during Push");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Unknown error encountered on server. Message:'{e.Message}' when writing an object");
                    Log.Error(e, "Unknown error during AWS Push");
                }
            }
        }

        public async Task DeleteAsync(string filePath)
        {
            if (!_isEnabled) return;

            try
            {
                using (var s3Client = GetS3Client())
                {
                    var keyName = $"{FolderPath}{Path.GetFileName(filePath)}";

                    var deleteObjectRequest = new DeleteObjectRequest
                    {
                        BucketName = _bucketName,
                        Key = keyName
                    };

                    Console.WriteLine($"Deleting {keyName} from AWS S3...");

                    if (await AmazonS3Util.DoesS3BucketExistV2Async(s3Client, _bucketName))
                    {
                        await s3Client.DeleteObjectAsync(deleteObjectRequest);
                    }
                }
            }
            catch (AmazonS3Exception e)
            {
                Console.WriteLine($"Error encountered on server during AWS delete. Message:'{e.Message}'");
            }
            catch (Exception e)
            {
                Console.WriteLine($"Unknown error encountered on server during AWS delete. Message:'{e.Message}'");
            }
        }
    }
}
