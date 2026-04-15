using DeuxOrders.API.DTOs;
using DeuxOrders.Domain.Identity;
using DeuxOrders.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IUserRepository userRepository, ITokenService tokenService, IUnitOfWork unitOfWork, ILogger<AuthController> logger)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    [Authorize]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var existingUser = await _userRepository.GetByEmail(request.Email) ?? await _userRepository.GetByUsername(request.Username);
        if (existingUser != null)
            return Conflict("Usuário com este e-mail ou username já existe.");

        var hash = await Task.Run(() => BCrypt.Net.BCrypt.HashPassword(request.Password));

        var user = new User(request.Name, request.Username, hash, request.Email, UserRole.Administrator);

        _userRepository.Add(user);

        try
        {
            var success = await _unitOfWork.CommitAsync();
            if (!success)
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
        var user = await _userRepository.GetByEmail(request.Email);

        if (user == null)
        {
            _logger.LogWarning("Failed login: e-mail not found ({Email}) from {IP}", request.Email, ip);
            return Unauthorized("Credenciais inválidas.");
        }

        var isValid = await Task.Run(() => BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash));
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