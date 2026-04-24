namespace DeuxERP.Infrastructure.Notifications;

public sealed class VapidSettings
{
    public string Subject { get; set; } = null!;
    public string PublicKey { get; set; } = null!;
    public string PrivateKey { get; set; } = null!;
}
