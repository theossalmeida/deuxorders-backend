using DeuxOrders.Domain.Entities;
using DeuxOrders.Domain.Interfaces;
using DeuxOrders.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DeuxOrders.Infrastructure.Repositories
{
    public class UserRepository : IUserRepository
    {
        private readonly ApplicationDbContext _context;

        public UserRepository(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<User?> GetByIdAsync(Guid id)
        {
            return await _context.Users
                .FirstOrDefaultAsync(o => o.Id == id);
        }

        public async Task<User?> GetByEmail(string email)
        {
            return await _context.Users
                .FirstOrDefaultAsync(o => o.Email == email);
        }
        public async Task<User?> GetByUsername(string username)
        {
            return await _context.Users
                .FirstOrDefaultAsync(o => o.Username == username);
        }

        public void Add(User user)
        {
            _context.Users.Add(user);
        }

        public void Update(User user)
        {
            _context.Users.Update(user);
        }
    }
}