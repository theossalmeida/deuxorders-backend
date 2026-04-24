namespace DeuxERP.Domain.Storage;

public class OrderReferenceUpload
{
    public Guid Id { get; private set; }
    public string ObjectKey { get; private set; } = null!;
    public Guid UserId { get; private set; }
    public Guid? OrderId { get; private set; }
    public string ContentType { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? ConsumedAt { get; private set; }

    public bool IsConsumed => ConsumedAt.HasValue;

    public OrderReferenceUpload(string objectKey, Guid userId, Guid? orderId, string contentType, DateTime expiresAt)
    {
        Id = Guid.CreateVersion7();
        ObjectKey = objectKey;
        UserId = userId;
        OrderId = orderId;
        ContentType = contentType;
        CreatedAt = DateTime.UtcNow;
        ExpiresAt = expiresAt;
    }

    private OrderReferenceUpload()
    {
    }

    public void Consume(Guid orderId, DateTime consumedAt)
    {
        if (IsConsumed)
            throw new InvalidOperationException("Upload de referência já consumido.");

        if (ExpiresAt <= consumedAt)
            throw new InvalidOperationException("Upload de referência expirado.");

        if (OrderId.HasValue && OrderId.Value != orderId)
            throw new InvalidOperationException("Upload de referência não pertence a este pedido.");

        OrderId = orderId;
        ConsumedAt = consumedAt;
    }
}
