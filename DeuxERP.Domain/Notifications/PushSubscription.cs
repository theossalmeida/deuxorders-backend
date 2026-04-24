using DeuxERP.Domain.Common;

namespace DeuxERP.Domain.Notifications;

public class PushSubscription : Entity
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Endpoint { get; private set; } = null!;
    public string P256dh { get; private set; } = null!;
    public string Auth { get; private set; } = null!;
    public string? DeviceLabel { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastUsedAt { get; private set; }
    public DateTime? DeactivatedAt { get; private set; }
    public bool IsActive { get; private set; }

    public static PushSubscription Create(
        Guid userId,
        string endpoint,
        string p256dh,
        string auth,
        string? deviceLabel)
    {
        return new PushSubscription
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            Endpoint = endpoint.Trim(),
            P256dh = p256dh.Trim(),
            Auth = auth.Trim(),
            DeviceLabel = string.IsNullOrWhiteSpace(deviceLabel) ? null : deviceLabel.Trim(),
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
    }

    public void Refresh(Guid userId, string p256dh, string auth, string? deviceLabel)
    {
        UserId = userId;
        P256dh = p256dh.Trim();
        Auth = auth.Trim();
        DeviceLabel = string.IsNullOrWhiteSpace(deviceLabel) ? DeviceLabel : deviceLabel.Trim();
        IsActive = true;
        DeactivatedAt = null;
    }

    public void MarkUsed() => LastUsedAt = DateTime.UtcNow;

    public void Deactivate()
    {
        IsActive = false;
        DeactivatedAt = DateTime.UtcNow;
    }

    private PushSubscription() { }
}
