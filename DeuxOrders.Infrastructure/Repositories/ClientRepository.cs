using DeuxOrders.Application.DTOs;
using DeuxOrders.Domain.Entities;
using DeuxOrders.Domain.Interfaces;
using DeuxOrders.Domain.Models;
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
        public async Task<IEnumerable<Client>> GetAll()
        {
            return await _context.Clients
                .AsNoTracking()
                .ToListAsync();
        }

        public void Add(Client client)
        {
            _context.Clients.Add(client);
        }

        public void Update(Client client)
        {
            _context.Clients.Update(client);
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var rowsAffected = await _context.Clients
                .Where(c => c.Id == id)
                .ExecuteDeleteAsync();

            return rowsAffected > 0;
        }
        public async Task<IEnumerable<DropdownItemModel>> GetForDropdownAsync(bool? status)
        {
            var query = _context.Clients.AsNoTracking();

            if (status.HasValue)
            {
                query = query.Where(c => c.Status == status.Value);
            }

            return await query
                .Select(c => new DropdownItemModel
                {
                    Id = c.Id,
                    Name = c.Name
                })
                .ToListAsync();
        }

    }
}