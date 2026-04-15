using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using DataCrud.DBOps.Shared;
using DataCrud.DBOps.Shared.Enums;
using DataCrud.DBOps.Shared.Extensions;
using Serilog;

namespace DataCrud.DBOps.AwsPush
{
    
    public static class AwsS3ObjectManager
    {
        private static readonly string BucketName = AppSettings.S3BucketName;
        private static readonly RegionEndpoint BucketRegion = RegionEndpoint.GetBySystemName(AppSettings.S3BucketRegion);
        private const string FolderPath = "backups/databases/";

        private static IAmazonS3 _amazonS3;

        public static void Push(string filePath)
        {
            if (!AppSettings.PushToAwsS3Bucket) return;

            Console.WriteLine($"Pushing {filePath} to aws s3...");

            _amazonS3 = new AmazonS3Client(new BasicAWSCredentials(AppSettings.AwsAccessKey, AppSettings.AwsSecretKey), BucketRegion);

            var keyName = $"{FolderPath}{Path.GetFileName(filePath)}";

            try
            {
                var request = new PutObjectRequest
                {
                    BucketName = BucketName,
                    Key = keyName,
                    FilePath = filePath,
                    ContentType = keyName.GetContentType()
                };

                request.Metadata.Add("x-amz-meta-title", "database backup");


                _amazonS3.EnsureBucketExists(BucketName);

                if (_amazonS3.DoesS3BucketExist(BucketName))
                {
                    PutObjectResponse response = _amazonS3.PutObject(request);
                }

            }
            catch (AmazonS3Exception e)
            {
                Console.WriteLine(
                    "Error encountered ***. Message:'{0}' when writing an object"
                    , e.Message);

                Log.Error(e.ToString());
            }
            catch (Exception e)
            {
                Console.WriteLine(
                    "Unknown encountered on server. Message:'{0}' when writing an object"
                    , e.Message);

                Log.Error(e.ToString());
            }

        }



        public static void Delete(string filePath)
        {
            try
            {
                var keyName = $"{FolderPath}{Path.GetFileName(filePath)}";

                _amazonS3 = new AmazonS3Client(new BasicAWSCredentials(AppSettings.AwsAccessKey, AppSettings.AwsSecretKey), BucketRegion);

                var deleteObjectRequest = new DeleteObjectRequest
                {
                    BucketName = BucketName,
                    Key = keyName
                };

                Console.WriteLine($"Deleting {keyName}...");

                if (_amazonS3.DoesS3BucketExist(BucketName))
                {
                    _amazonS3.DeleteObject(deleteObjectRequest);
                }
            }
            catch (AmazonS3Exception e)
            {
                Console.WriteLine("Error encountered on server. Message:'{0}' when deleting an object", e.Message);
            }
            catch (Exception e)
            {
                Console.WriteLine("Unknown encountered on server. Message:'{0}' when deleting an object", e.Message);
            }
        }
    }
}

