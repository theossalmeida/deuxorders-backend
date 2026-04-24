namespace DeuxERP.Domain.Storage;

public static class OrderReferenceObjectKey
{
    public const string Prefix = "order-references/";

    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    public static bool IsValid(string objectKey)
    {
        if (string.IsNullOrWhiteSpace(objectKey) || !objectKey.StartsWith(Prefix, StringComparison.Ordinal))
            return false;

        var fileName = objectKey[Prefix.Length..];
        if (fileName.Contains('/') || fileName.Contains('\\'))
            return false;

        var extension = Path.GetExtension(fileName);
        if (!AllowedExtensions.Contains(extension))
            return false;

        var idPart = Path.GetFileNameWithoutExtension(fileName);
        return Guid.TryParse(idPart, out _);
    }
}
