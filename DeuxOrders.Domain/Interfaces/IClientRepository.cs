using DeuxOrders.Domain.Entities;

namespace DeuxOrders.Domain.Interfaces
{
    public interface IClientRepository
    {
        Task<Client?> GetByIdAsync(Guid Id);
        void Add(Client client);
        void Update(Client client);
    }
}
