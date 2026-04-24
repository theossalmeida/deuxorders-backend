using DeuxERP.Application.Common;
using Microsoft.AspNetCore.Http;

namespace DeuxERP.API.Services;

public sealed class CurrentUserAccessor : ICurrentUserAccessor
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserAccessor(IHttpContextAccessor httpContextAccessor)
        => _httpContextAccessor = httpContextAccessor;

    public Guid UserId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User.FindFirst("id")?.Value;
            if (claim != null && Guid.TryParse(claim, out var id))
                return id;

            throw new UnauthorizedAccessException("Token autenticado sem claim de usuário válida.");
        }
    }

    public string UserName
    {
        get
        {
            var email = _httpContextAccessor.HttpContext?.User.FindFirst("email")?.Value;
            if (!string.IsNullOrWhiteSpace(email))
                return email;

            throw new UnauthorizedAccessException("Token autenticado sem claim de e-mail válida.");
        }
    }
}
