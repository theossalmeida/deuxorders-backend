using DeuxOrders.Domain.Entities;

namespace DeuxOrders.Domain.Interfaces
{
    public interface IClientRepository
    {
        Task<Client?> GetByIdAsync(Guid Id);
        Task AddAsync(Client client);
        Task UpdateAsync(Client client);
    }
}
