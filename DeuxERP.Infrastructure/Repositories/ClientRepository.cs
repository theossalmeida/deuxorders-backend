using DeuxERP.Application.DTOs;
using DeuxERP.Domain.Sales;
using DeuxERP.Domain.Interfaces;
using DeuxERP.Domain.Models;
using DeuxERP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DeuxERP.Infrastructure.Repositories
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
        public async Task<PagedResult<Client>> GetAll(string? search, bool? status, int page = 1, int size = 20)
        {
            var query = _context.Clients.AsNoTracking();

            if (status.HasValue)
                query = query.Where(c => c.Status == status.Value);

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(c => c.Name.ToLower().Contains(search.ToLower()));

            var totalCount = await query.CountAsync();
            var items = await query
                .OrderBy(c => c.Name)
                .Skip((page - 1) * size)
                .Take(size)
                .ToListAsync();

            return new PagedResult<Client>(items, totalCount, page, size);
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