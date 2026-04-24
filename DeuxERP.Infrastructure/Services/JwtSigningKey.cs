using System.Text;

namespace DeuxERP.Infrastructure.Services;

public static class JwtSigningKey
{
    private const int MinimumKeyBytes = 32;

    public static byte[] FromSecret(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
            throw new InvalidOperationException("JWT Secret não configurada.");

        byte[] key;
        if (secret.StartsWith("base64:", StringComparison.OrdinalIgnoreCase))
        {
            var encoded = secret["base64:".Length..];
            try
            {
                key = Convert.FromBase64String(encoded);
            }
            catch (FormatException ex)
            {
                throw new InvalidOperationException("JWT Secret base64 inválida.", ex);
            }
        }
        else
        {
            key = Encoding.UTF8.GetBytes(secret);
        }

        if (key.Length < MinimumKeyBytes)
            throw new InvalidOperationException("JWT Secret deve ter pelo menos 32 bytes de entropia.");

        return key;
    }
}
