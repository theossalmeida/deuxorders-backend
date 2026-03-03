using DeuxOrders.Domain.Entities;
using DeuxOrders.Domain.Interfaces;
using DeuxOrders.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DeuxOrders.Infrastructure.Repositories
{
    public class ClientRepository : IClientRepository
    {
        private readonly ApplicationDbContext _context;

        public ClientRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Client?> GetByIdAsync(Guid id)
        {
            return await _context.Clients
                .FirstOrDefaultAsync(o => o.Id == id);
        }

        public void Add(Client client)
        {
            _context.Clients.Add(client);
        }

        public void Update(Client client)
        {
            _context.Clients.Update(client);
        }
    }
}