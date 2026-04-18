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
            return claim != null && Guid.TryParse(claim, out var id) ? id : Guid.Empty;
        }
    }

    public string UserName
        => _httpContextAccessor.HttpContext?.User.FindFirst("email")?.Value ?? "system";
}
