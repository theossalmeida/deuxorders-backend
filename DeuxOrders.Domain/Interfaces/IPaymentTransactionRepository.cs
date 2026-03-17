using DeuxOrders.Domain.Entities;

namespace DeuxOrders.Domain.Interfaces
{
    public interface IPaymentTransactionRepository
    {
        Task<PaymentTransaction?> GetByAbacateBillingIdAsync(string billingId);
        Task<PaymentTransaction?> GetByIdempotencyKeyAsync(string idempotencyKey);
        Task<PaymentTransaction?> GetByOrderIdAsync(Guid orderId);
        void Add(PaymentTransaction transaction);
    }
}
