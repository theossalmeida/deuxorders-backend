using DeuxOrders.Domain.Cash.Enums;
using DeuxOrders.Domain.Common;
using DeuxOrders.Domain.Sales.Events;

namespace DeuxOrders.Domain.Cash;

public class CashFlowEntry : Entity
{
    public Guid Id { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime BillingDate { get; private set; }
    public CashFlowType Type { get; private set; }
    public CashFlowCategory Category { get; private set; }
    public string Counterparty { get; private set; } = null!;
    public long AmountCents { get; private set; }
    public string? Notes { get; private set; }
    public CashFlowSource Source { get; private set; }
    public Guid? SourceId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public string AuthorUserName { get; private set; } = null!;
    public DateTime? UpdatedAt { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }
    public string? UpdatedByUserName { get; private set; }
    public DateTime? DeletedAt { get; private set; }
    public Guid? DeletedByUserId { get; private set; }
    public string? DeletedByUserName { get; private set; }
    public string? DeletionReason { get; private set; }

    public static CashFlowEntry CreateManual(
        DateTime billingDate, CashFlowType type, CashFlowCategory category,
        string counterparty, long amountCents, string? notes,
        Guid authorUserId, string authorUserName, Guid? sourceId = null)
    {
        Validate(amountCents, counterparty);
        return new CashFlowEntry
        {
            Id = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            BillingDate = DateTime.SpecifyKind(billingDate.Date.AddHours(12), DateTimeKind.Utc),
            Type = type,
            Category = category,
            Counterparty = counterparty.Trim(),
            AmountCents = amountCents,
            Notes = notes?.Trim(),
            Source = CashFlowSource.Manual,
            SourceId = sourceId,
            AuthorUserId = authorUserId,
            AuthorUserName = authorUserName
        };
    }

    public static CashFlowEntry FromOrderPayment(OrderPaidEvent ev) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            BillingDate = ev.PaidAt,
            Type = CashFlowType.Inflow,
            Category = CashFlowCategory.Order,
            Counterparty = ev.ClientName,
            AmountCents = ev.AmountCents,
            Source = CashFlowSource.OrderPayment,
            SourceId = ev.OrderId,
            AuthorUserId = ev.UserId,
            AuthorUserName = ev.UserName
        };

    public static CashFlowEntry FromOrderReversal(OrderPaymentReversedEvent ev) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            CreatedAt = DateTime.UtcNow,
            BillingDate = DateTime.UtcNow,
            Type = CashFlowType.Outflow,
            Category = CashFlowCategory.OrderReversal,
            Counterparty = ev.ClientName,
            AmountCents = ev.AmountCents,
            Notes = $"Reversal of order {ev.OrderId}: {ev.Reason}",
            Source = CashFlowSource.OrderReversal,
            SourceId = ev.OrderId,
            AuthorUserId = ev.UserId,
            AuthorUserName = ev.UserName
        };

    public void Update(
        DateTime billingDate, CashFlowType type, CashFlowCategory category,
        string counterparty, long amountCents, string? notes,
        Guid editorId, string editorName)
    {
        if (Source != CashFlowSource.Manual)
            throw new InvalidOperationException("Entradas automáticas não podem ser editadas.");
        if (DeletedAt.HasValue)
            throw new InvalidOperationException("Entradas excluídas não podem ser editadas.");
        Validate(amountCents, counterparty);

        BillingDate = DateTime.SpecifyKind(billingDate.Date.AddHours(12), DateTimeKind.Utc);
        Type = type;
        Category = category;
        Counterparty = counterparty.Trim();
        AmountCents = amountCents;
        Notes = notes?.Trim();
        UpdatedAt = DateTime.UtcNow;
        UpdatedByUserId = editorId;
        UpdatedByUserName = editorName;
    }

    public void SoftDelete(Guid userId, string userName, string reason)
    {
        if (Source != CashFlowSource.Manual)
            throw new InvalidOperationException(
                "Entradas automáticas não podem ser excluídas. Reverta via pedido de origem.");
        if (DeletedAt.HasValue)
            throw new InvalidOperationException("Entrada já excluída.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Length < 5)
            throw new ArgumentException("Motivo da exclusão é obrigatório (mínimo 5 caracteres).");

        DeletedAt = DateTime.UtcNow;
        DeletedByUserId = userId;
        DeletedByUserName = userName;
        DeletionReason = reason.Trim();
    }

    private static void Validate(long amountCents, string counterparty)
    {
        if (amountCents <= 0)
            throw new ArgumentException("O valor deve ser maior que zero.");
        if (string.IsNullOrWhiteSpace(counterparty))
            throw new ArgumentException("A contraparte é obrigatória.");
        if (counterparty.Length > 200)
            throw new ArgumentException("Contraparte: máximo 200 caracteres.");
    }

    private CashFlowEntry() { }
}
