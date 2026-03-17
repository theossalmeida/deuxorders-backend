namespace DeuxOrders.Domain.Entities
{
    public class WebhookEventLog
    {
        public Guid Id { get; private set; }
        public DateTime ReceivedAt { get; private set; }
        public string? EventType { get; private set; }
        public string RawPayload { get; private set; } = null!;
        public string? SignatureHeader { get; private set; }
        public bool SignatureValid { get; private set; }
        public bool SecretValid { get; private set; }
        public string? ProcessingResult { get; private set; }
        public string? ErrorMessage { get; private set; }
        public string? AbacateBillingId { get; private set; }
        public int HttpStatusReturned { get; private set; }

        public WebhookEventLog(
            Guid id,
            DateTime receivedAt,
            string? eventType,
            string rawPayload,
            string? signatureHeader,
            bool signatureValid,
            bool secretValid,
            string? processingResult,
            string? errorMessage,
            string? abacateBillingId,
            int httpStatusReturned)
        {
            Id = id;
            ReceivedAt = receivedAt;
            EventType = eventType;
            RawPayload = rawPayload;
            SignatureHeader = signatureHeader;
            SignatureValid = signatureValid;
            SecretValid = secretValid;
            ProcessingResult = processingResult;
            ErrorMessage = errorMessage;
            AbacateBillingId = abacateBillingId;
            HttpStatusReturned = httpStatusReturned;
        }

        private WebhookEventLog() { }
    }
}
