using DeuxERP.Domain.Identity;

namespace DeuxERP.Domain.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetByIdAsync(Guid Id);
        Task<User?> GetByEmail(string email);
        Task<User?> GetByUsername(string username);
        void Add(User user);
        void Update(User user);
    }
}
