using DeuxOrders.Domain.Entities;
using DeuxOrders.Domain.Interfaces;
using DeuxOrders.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DeuxOrders.Infrastructure.Repositories
{
    public class PaymentTransactionRepository : IPaymentTransactionRepository
    {
        private readonly ApplicationDbContext _context;

        public PaymentTransactionRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<PaymentTransaction?> GetByAbacateBillingIdAsync(string billingId)
            => await _context.PaymentTransactions
                .FirstOrDefaultAsync(t => t.AbacateBillingId == billingId);

        public async Task<PaymentTransaction?> GetByIdempotencyKeyAsync(string idempotencyKey)
            => await _context.PaymentTransactions
                .FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey);

        public async Task<PaymentTransaction?> GetByOrderIdAsync(Guid orderId)
            => await _context.PaymentTransactions
                .Where(t => t.OrderId == orderId)
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();

        public void Add(PaymentTransaction transaction)
            => _context.PaymentTransactions.Add(transaction);
    }
}
