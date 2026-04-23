using DeuxERP.Domain.Inventory;

namespace DeuxERP.API.Models;

public record CreateMaterialRequest(string Name, int Quantity, long TotalCost, MeasureUnit MeasureUnit);

public record UpdateMaterialRequest(string Name, MeasureUnit MeasureUnit);

public record RestockRequest(int Quantity, long TotalCost);
