namespace DeuxOrders.API.Services
{
    public interface IStorageService
    {
        string GeneratePresignedUploadUrl(string objectKey, string contentType);
        List<string>? GetSignedReadUrls(List<string>? objectKeys);
        Task DeleteObjectAsync(string objectKey);
        Task<string> UploadFileAsync(Stream stream, string objectKey, string contentType);
        string GetPublicUrl(string objectKey);
    }
}
