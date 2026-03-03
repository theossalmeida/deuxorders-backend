using DeuxOrders.Domain.Entities;

namespace DeuxOrders.Domain.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(Guid Id);
        Task<User?> GetByEmail(string email);
        Task<User?> GetByUsername(string username);
        Task AddAsync(User user);
        Task UpdateAsync(User user);
    }
}
