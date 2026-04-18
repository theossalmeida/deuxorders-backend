using DeuxERP.Domain.Cash.Enums;

namespace DeuxERP.Domain.Cash;

public class CashFlowAuditLog
{
    public Guid Id { get; private set; }
    public Guid EntryId { get; private set; }
    public DateTime OccurredAt { get; private set; }
    public AuditAction Action { get; private set; }
    public Guid UserId { get; private set; }
    public string UserName { get; private set; } = null!;
    public string SnapshotJson { get; private set; } = null!;
    public string? PreviousSnapshotJson { get; private set; }

    public CashFlowAuditLog(
        Guid entryId, AuditAction action, Guid userId, string userName,
        string snapshotJson, string? previousSnapshotJson)
    {
        Id = Guid.CreateVersion7();
        EntryId = entryId;
        OccurredAt = DateTime.UtcNow;
        Action = action;
        UserId = userId;
        UserName = userName;
        SnapshotJson = snapshotJson;
        PreviousSnapshotJson = previousSnapshotJson;
    }

    private CashFlowAuditLog() { }
}
