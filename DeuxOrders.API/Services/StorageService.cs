using System.Security.Cryptography;
using System.Text;

namespace DeuxOrders.API.Services
{
    public class StorageService : IStorageService
    {
        private readonly string _accessKeyId;
        private readonly string _secretAccessKey;
        private readonly string _bucketName;
        private readonly string _endpoint;
        private const string Region = "auto";
        private const string Service = "s3";

        public StorageService(IConfiguration configuration)
        {
            _accessKeyId = configuration["R2:AccessKeyId"]!;
            _secretAccessKey = configuration["R2:SecretAccessKey"]!;
            _bucketName = configuration["R2:BucketName"]!;
            var accountId = configuration["R2:AccountId"]!;
            _endpoint = $"https://{accountId}.r2.cloudflarestorage.com";
        }

        public string GeneratePresignedUploadUrl(string objectKey, string contentType)
            => BuildPresignedUrl("PUT", objectKey, expiresInSeconds: 900);

        public List<string>? GetSignedReadUrls(List<string>? objectKeys)
        {
            if (objectKeys == null || objectKeys.Count == 0) return null;
            return objectKeys.Select(k => BuildPresignedUrl("GET", k, expiresInSeconds: 3600)).ToList();
        }

        private string BuildPresignedUrl(string method, string objectKey, int expiresInSeconds)
        {
            var now = DateTime.UtcNow;
            var dateStamp = now.ToString("yyyyMMdd");
            var amzDate = now.ToString("yyyyMMddTHHmmssZ");
            var credentialScope = $"{dateStamp}/{Region}/{Service}/aws4_request";
            var host = new Uri(_endpoint).Host;

            var queryParams = new SortedDictionary<string, string>
            {
                ["X-Amz-Algorithm"] = "AWS4-HMAC-SHA256",
                ["X-Amz-Credential"] = $"{_accessKeyId}/{credentialScope}",
                ["X-Amz-Date"] = amzDate,
                ["X-Amz-Expires"] = expiresInSeconds.ToString(),
                ["X-Amz-SignedHeaders"] = "host"
            };

            var canonicalQueryString = string.Join("&", queryParams.Select(kvp =>
                $"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(kvp.Value)}"));

            var canonicalUri = "/" + string.Join("/", objectKey.Split('/').Select(Uri.EscapeDataString));

            var canonicalRequest = string.Join("\n",
                method,
                $"/{_bucketName}{canonicalUri}",
                canonicalQueryString,
                $"host:{host}\n",
                "host",
                "UNSIGNED-PAYLOAD");

            var stringToSign = string.Join("\n",
                "AWS4-HMAC-SHA256",
                amzDate,
                credentialScope,
                Sha256Hex(canonicalRequest));

            var signature = HmacHex(DeriveSigningKey(dateStamp), stringToSign);

            return $"{_endpoint}/{_bucketName}/{objectKey}?{canonicalQueryString}&X-Amz-Signature={signature}";
        }

        private byte[] DeriveSigningKey(string dateStamp)
        {
            var kDate = Hmac(Encoding.UTF8.GetBytes($"AWS4{_secretAccessKey}"), dateStamp);
            var kRegion = Hmac(kDate, Region);
            var kService = Hmac(kRegion, Service);
            return Hmac(kService, "aws4_request");
        }

        private static byte[] Hmac(byte[] key, string data)
            => new HMACSHA256(key).ComputeHash(Encoding.UTF8.GetBytes(data));

        private static string HmacHex(byte[] key, string data)
            => Convert.ToHexString(Hmac(key, data)).ToLower();

        private static string Sha256Hex(string data)
            => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(data))).ToLower();
    }
}
