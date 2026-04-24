using DeuxERP.Domain.Storage;

namespace DeuxERP.API.Services
{
    public static class FileValidation
    {
        private static readonly HashSet<string> AllowedExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

        private static readonly HashSet<string> AllowedContentTypes =
            new(StringComparer.OrdinalIgnoreCase) { "image/jpeg", "image/png", "image/webp" };

        public static bool IsAllowedImage(string fileName, string contentType)
        {
            var ext = Path.GetExtension(fileName);
            return AllowedExtensions.Contains(ext) && AllowedContentTypes.Contains(contentType);
        }

        public static bool IsAllowedImage(IFormFile file)
            => IsAllowedImage(file.FileName, file.ContentType);

        public static bool IsValidOrderReferenceKey(string objectKey)
            => OrderReferenceObjectKey.IsValid(objectKey);
    }
}
