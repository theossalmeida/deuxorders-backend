using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace DeuxOrders.API.Services
{
    public class StorageService : IStorageService
    {
        private readonly IAmazonS3 _s3Client;
        private readonly string _bucketName;

        public StorageService(IConfiguration configuration)
        {
            var accountId = configuration["R2:AccountId"]!;
            var accessKeyId = configuration["R2:AccessKeyId"]!;
            var secretAccessKey = configuration["R2:SecretAccessKey"]!;
            _bucketName = configuration["R2:BucketName"]!;

            var credentials = new BasicAWSCredentials(accessKeyId, secretAccessKey);
            var config = new AmazonS3Config
            {
                ServiceURL = $"https://{accountId}.r2.cloudflarestorage.com",
                ForcePathStyle = true
            };

            _s3Client = new AmazonS3Client(credentials, config);
        }

        public string GeneratePresignedUploadUrl(string objectKey, string contentType)
        {
            var request = new GetPreSignedUrlRequest
            {
                BucketName = _bucketName,
                Key = objectKey,
                Verb = HttpVerb.PUT,
                Expires = DateTime.UtcNow.AddMinutes(15),
                ContentType = contentType
            };

            return _s3Client.GetPreSignedURL(request);
        }

        public List<string>? GetSignedReadUrls(List<string>? objectKeys)
        {
            if (objectKeys == null || objectKeys.Count == 0) return null;

            return objectKeys.Select(key =>
            {
                var request = new GetPreSignedUrlRequest
                {
                    BucketName = _bucketName,
                    Key = key,
                    Verb = HttpVerb.GET,
                    Expires = DateTime.UtcNow.AddHours(1)
                };
                return _s3Client.GetPreSignedURL(request);
            }).ToList();
        }
    }
}
