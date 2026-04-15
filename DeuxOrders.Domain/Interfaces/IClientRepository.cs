using DeuxOrders.Domain.Models;
using DeuxOrders.Domain.Sales;

namespace DeuxOrders.Domain.Interfaces
{
    public interface IClientRepository
    {
        Task<Client?> GetByIdAsync(Guid Id);
        Task<PagedResult<Client>> GetAll(string? search, bool? status, int page = 1, int size = 20);
        void Add(Client client);
        void Update(Client client);
        Task<bool> DeleteAsync(Guid id);
        Task<IEnumerable<DropdownItemModel>> GetForDropdownAsync(bool? status);
    }
}