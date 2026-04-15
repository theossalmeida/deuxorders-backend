using DeuxOrders.Domain.Identity;

namespace DeuxOrders.Domain.Interfaces
{
    public interface ITokenService
    {
        string GenerateToken(User user);
    }
}