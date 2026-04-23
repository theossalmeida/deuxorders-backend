namespace DeuxERP.Application.DTOs;

public record InventoryMaterialResponse(
    Guid Id,
    string Name,
    int Quantity,
    long UnitCost,
    string MeasureUnit,
    bool Status,
    DateTime CreatedAt,
    DateTime UpdatedAt);
