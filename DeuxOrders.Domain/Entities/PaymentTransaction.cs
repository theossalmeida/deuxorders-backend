using DeuxOrders.Domain.Enums;

namespace DeuxOrders.Domain.Entities
{
    public class PaymentTransaction
    {
        public Guid Id { get; private set; }
        public Guid OrderId { get; private set; }
        public string AbacateBillingId { get; private set; } = null!;
        public string? AbacateCustomerId { get; private set; }
        public string PaymentMethod { get; private set; } = null!;
        public PaymentStatus Status { get; private set; }
        public long AmountCents { get; private set; }
        public long? PaidAmountCents { get; private set; }
        public long? PlatformFeeCents { get; private set; }
        public string? CheckoutUrl { get; private set; }
        public string? ReceiptUrl { get; private set; }
        public string? PayerName { get; private set; }
        public string? PayerTaxIdMasked { get; private set; }
        public string? CardLastFour { get; private set; }
        public string? CardBrand { get; private set; }
        public DateTime? WebhookReceivedAt { get; private set; }
        public string? WebhookEventType { get; private set; }
        public string IdempotencyKey { get; private set; } = null!;
        public string? FailureReason { get; private set; }
        public bool DevMode { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? UpdatedAt { get; private set; }

        public PaymentTransaction(
            Guid orderId,
            string abacateBillingId,
            string paymentMethod,
            long amountCents,
            string? checkoutUrl,
            string idempotencyKey,
            bool devMode)
        {
            Id = Guid.CreateVersion7();
            OrderId = orderId;
            AbacateBillingId = abacateBillingId;
            PaymentMethod = paymentMethod;
            Status = PaymentStatus.Pending;
            AmountCents = amountCents;
            CheckoutUrl = checkoutUrl;
            IdempotencyKey = idempotencyKey;
            DevMode = devMode;
            CreatedAt = DateTime.UtcNow;
        }

        private PaymentTransaction() { }

        // Pending → Paid. Se já Paid, ignora (idempotência).
        public void ConfirmPayment(
            long paidAmount, long? platformFee,
            string? payerName, string? payerTaxId,
            string? cardLastFour, string? cardBrand,
            string? receiptUrl, string? eventType)
        {
            if (Status == PaymentStatus.Paid) return;
            if (Status != PaymentStatus.Pending)
                throw new InvalidOperationException("Só é possível confirmar pagamentos Pending.");
            if (paidAmount <= 0)
                throw new InvalidOperationException("paidAmount deve ser maior que zero.");

            Status = PaymentStatus.Paid;
            PaidAmountCents = paidAmount;
            PlatformFeeCents = platformFee;
            PayerName = payerName;
            PayerTaxIdMasked = payerTaxId;
            CardLastFour = cardLastFour;
            CardBrand = cardBrand;
            ReceiptUrl = receiptUrl;
            WebhookReceivedAt = DateTime.UtcNow;
            WebhookEventType = eventType;
            UpdatedAt = DateTime.UtcNow;
        }

        // Pending → Failed
        public void MarkAsFailed(string reason)
        {
            if (Status != PaymentStatus.Pending)
                throw new InvalidOperationException("Só é possível marcar como Failed a partir de Pending.");
            Status = PaymentStatus.Failed;
            FailureReason = reason;
            UpdatedAt = DateTime.UtcNow;
        }

        // Paid → Refunded
        public void MarkAsRefunded(string reason, string? eventType)
        {
            if (Status != PaymentStatus.Paid)
                throw new InvalidOperationException("Só é possível estornar pagamentos Paid.");
            Status = PaymentStatus.Refunded;
            FailureReason = reason;
            WebhookReceivedAt = DateTime.UtcNow;
            WebhookEventType = eventType;
            UpdatedAt = DateTime.UtcNow;
        }

        // Paid → Disputed
        public void MarkAsDisputed(string reason, string? eventType)
        {
            if (Status != PaymentStatus.Paid)
                throw new InvalidOperationException("Só é possível disputar pagamentos Paid.");
            Status = PaymentStatus.Disputed;
            FailureReason = reason;
            WebhookReceivedAt = DateTime.UtcNow;
            WebhookEventType = eventType;
            UpdatedAt = DateTime.UtcNow;
        }

        // Pending → Expired (billing link closed before payment)
        public void MarkAsExpired(string? eventType)
        {
            if (Status != PaymentStatus.Pending)
                throw new InvalidOperationException("Só é possível expirar pagamentos Pending.");
            Status = PaymentStatus.Expired;
            WebhookReceivedAt = DateTime.UtcNow;
            WebhookEventType = eventType;
            UpdatedAt = DateTime.UtcNow;
        }

        // Pending → Cancelled (customer or merchant explicitly cancelled)
        public void MarkAsCancelled(string reason, string? eventType)
        {
            if (Status != PaymentStatus.Pending)
                throw new InvalidOperationException("Só é possível cancelar pagamentos Pending.");
            Status = PaymentStatus.Cancelled;
            FailureReason = reason;
            WebhookReceivedAt = DateTime.UtcNow;
            WebhookEventType = eventType;
            UpdatedAt = DateTime.UtcNow;
        }

        public void SetAbacateCustomerId(string customerId)
        {
            AbacateCustomerId = customerId;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
