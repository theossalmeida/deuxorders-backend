using DeuxOrders.Domain.Cash.Enums;

namespace DeuxOrders.Application.DTOs;

public record CreateCashEntryRequest(
    DateTime BillingDate,
    CashFlowType Type,
    CashFlowCategory Category,
    string Counterparty,
    long AmountCents,
    string? Notes,
    Guid? SourceId = null);

public record UpdateCashEntryRequest(
    DateTime BillingDate,
    CashFlowType Type,
    CashFlowCategory Category,
    string Counterparty,
    long AmountCents,
    string? Notes);

public record CashEntryResponse(
    Guid Id,
    DateTime CreatedAt,
    DateTime BillingDate,
    string Type,
    string Category,
    string Counterparty,
    long AmountCents,
    string? Notes,
    string Source,
    Guid? SourceId,
    Guid AuthorUserId,
    string AuthorUserName,
    DateTime? UpdatedAt,
    DateTime? DeletedAt);

public record CashDailyPoint(
    DateTime Date,
    long InflowCents,
    long OutflowCents);
