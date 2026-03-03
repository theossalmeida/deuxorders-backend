using DeuxOrders.Domain.Entities;

namespace DeuxOrders.Domain.Interfaces
{
    public interface ITokenService
    {
        string GenerateToken(User user);
    }
}