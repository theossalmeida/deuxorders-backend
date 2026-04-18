using DeuxERP.Domain.Identity;

namespace DeuxERP.Domain.Interfaces
{
    public interface ITokenService
    {
        string GenerateToken(User user);
    }
}