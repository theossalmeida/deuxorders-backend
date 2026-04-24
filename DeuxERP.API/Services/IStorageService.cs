namespace DeuxERP.API.Services
{
    public interface IStorageService
    {
        string GeneratePresignedUploadUrl(string objectKey, string contentType);
        List<string>? GetSignedReadUrls(List<string>? objectKeys);
        Task DeleteObjectAsync(string objectKey, CancellationToken ct = default);
        Task<string> UploadFileAsync(Stream stream, string objectKey, string contentType, CancellationToken ct = default);
        string GetPublicUrl(string objectKey);
    }
}
