public record UpdateOrderRequest(
    DateTime? DeliveryDate,
    int? Status,
    List<UpdateOrderItemRequest>? Items,
    List<string>? References
);

public record UpdateOrderItemRequest(
    Guid ProductId,
    int? Quantity,
    int? PaidUnitPrice,
    string? Observation
);

public record UpdateItemQuantityRequest(int Increment);