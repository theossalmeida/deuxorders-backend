using DeuxERP.API.DTOs;
using DeuxERP.Application.Common;
using DeuxERP.Domain.Identity;
using DeuxERP.Domain.Interfaces;
using DeuxERP.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Data;

[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(ApplicationDbContext db, ITokenService tokenService, ILogger<AuthController> logger)
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
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        var usersExist = await _db.Users.AnyAsync();
        if (!_isDevMode && usersExist)
        {
            if (!(User.Identity?.IsAuthenticated ?? false))
                return Unauthorized("Token de autenticação necessário.");

            if (!User.IsInRole(UserRole.Administrator.ToString()))
                return Forbid();
        }

        var existingUser = await _db.Users.FirstOrDefaultAsync(user =>
            user.Email == request.Email || user.Username == request.Username);
        if (existingUser != null)
            return Conflict("Usuário com este e-mail ou username já existe.");

        var hash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var role = usersExist && !_isDevMode ? UserRole.User : UserRole.Administrator;
        var user = new User(request.Name, request.Username, hash, request.Email, role);

        _db.Users.Add(user);

        try
        {
            if (await _db.SaveChangesAsync() == 0)
                return BadRequest("Falha ao registrar o usuário no banco de dados.");

            await transaction.CommitAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("unique") == true
                                        || ex.InnerException?.Message.Contains("duplicate") == true)
        {
            return Conflict("Usuário com este e-mail ou username já existe.");
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message.Contains("40001") == true
                                        || ex.InnerException?.Message.Contains("serialization", StringComparison.OrdinalIgnoreCase) == true)
        {
            return Conflict("Registro simultâneo detectado. Recarregue e tente novamente.");
        }
        catch (Exception ex) when (ex.Message.Contains("40001", StringComparison.OrdinalIgnoreCase)
                                || ex.Message.Contains("serialization", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict("Registro simultâneo detectado. Recarregue e tente novamente.");
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
