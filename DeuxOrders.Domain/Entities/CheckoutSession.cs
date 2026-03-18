namespace DeuxOrders.Domain.Entities
{
    // Stores a pending ecommerce checkout between the billing creation
    // and the webhook confirmation. Consumed exactly once on billing.paid.
    public class CheckoutSession
    {
        public Guid Id { get; private set; }
        public string AbacateBillingId { get; private set; } = null!;
        public string? AbacateCustomerId { get; private set; }
        public string ClientName { get; private set; } = null!;
        public string ClientMobile { get; private set; } = null!;
        public string Email { get; private set; } = null!;
        public string TaxId { get; private set; } = null!;
        public DateTime DeliveryDate { get; private set; }
        // JSON array: [{productId, name, quantity, paidPrice, basePrice, observation}]
        public string ItemsJson { get; private set; } = null!;
        // JSON array of R2 object keys already uploaded
        public string? ReferencesJson { get; private set; }
        public long AmountCents { get; private set; }
        public string? CheckoutUrl { get; private set; }
        public bool DevMode { get; private set; }
        public DateTime CreatedAt { get; private set; }
        // Set when the order is successfully created after billing.paid
        public DateTime? UsedAt { get; private set; }

        public CheckoutSession(
            string abacateBillingId,
            string? abacateCustomerId,
            string clientName,
            string clientMobile,
            string email,
            string taxId,
            DateTime deliveryDate,
            string itemsJson,
            string? referencesJson,
            long amountCents,
            string? checkoutUrl,
            bool devMode)
        {
            Id = Guid.CreateVersion7();
            AbacateBillingId = abacateBillingId;
            AbacateCustomerId = abacateCustomerId;
            ClientName = clientName;
            ClientMobile = clientMobile;
            Email = email;
            TaxId = taxId;
            DeliveryDate = deliveryDate;
            ItemsJson = itemsJson;
            ReferencesJson = referencesJson;
            AmountCents = amountCents;
            CheckoutUrl = checkoutUrl;
            DevMode = devMode;
            CreatedAt = DateTime.UtcNow;
        }

        private CheckoutSession() { }

        public void MarkAsUsed()
        {
            UsedAt = DateTime.UtcNow;
        }
    }
}
