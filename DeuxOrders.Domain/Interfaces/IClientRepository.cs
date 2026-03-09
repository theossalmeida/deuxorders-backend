using DeuxOrders.Domain.Models;
using DeuxOrders.Domain.Entities;

namespace DeuxOrders.Domain.Interfaces
{
    public interface IClientRepository
    {
        Task<Client?> GetByIdAsync(Guid Id);
        Task<IEnumerable<Client>> GetAll();
        void Add(Client client);
        void Update(Client client);
        Task<bool> DeleteAsync(Guid id);
        Task<IEnumerable<DropdownItemModel>> GetForDropdownAsync(bool? status);
    }
}