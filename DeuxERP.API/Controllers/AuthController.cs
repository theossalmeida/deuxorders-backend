using DeuxERP.API.DTOs;
using DeuxERP.Application.Common;
using DeuxERP.Domain.Identity;
using DeuxERP.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly IAppDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAppDbContext db, ITokenService tokenService, ILogger<AuthController> logger)
    {
        _db = db;
        _tokenService = tokenService;
        _logger = logger;
    }

    private static readonly bool _isDevMode =
        (Environment.GetEnvironmentVariable("RUN_MODE") ?? "PROD") == "DEV";

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!_isDevMode && !(User.Identity?.IsAuthenticated ?? false))
            return Unauthorized("Token de autenticação necessário.");

        var existingUser = await _db.Users.FirstOrDefaultAsync(user =>
            user.Email == request.Email || user.Username == request.Username);
        if (existingUser != null)
            return Conflict("Usuário com este e-mail ou username já existe.");

        var hash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var user = new User(request.Name, request.Username, hash, request.Email, UserRole.Administrator);

        _db.Users.Add(user);

        try
        {
            if (await _db.SaveChangesAsync() == 0)
                return BadRequest("Falha ao registrar o usuário no banco de dados.");
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("unique") == true
                                        || ex.InnerException?.Message.Contains("duplicate") == true)
        {
            return Conflict("Usuário com este e-mail ou username já existe.");
        }

        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        _logger.LogInformation("New user registered: {Username} ({Email}) from {IP}", user.Username, user.Email, ip);

        return Ok("Usuário registrado com sucesso!");
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var user = await _db.Users.FirstOrDefaultAsync(user => user.Email == request.Email);

        if (user == null)
        {
            _logger.LogWarning("Failed login: e-mail not found ({Email}) from {IP}", request.Email, ip);
            return Unauthorized("Credenciais inválidas.");
        }

        var isValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        if (!isValid)
        {
            _logger.LogWarning("Failed login: wrong password for {Email} from {IP}", request.Email, ip);
            return Unauthorized("Credenciais inválidas.");
        }

        _logger.LogInformation("Login: {Email} from {IP}", request.Email, ip);
        var token = _tokenService.GenerateToken(user);

        return Ok(new { token });
    }
}
