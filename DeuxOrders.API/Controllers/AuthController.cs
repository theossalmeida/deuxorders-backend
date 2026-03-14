using DeuxOrders.API.DTOs;
using DeuxOrders.Domain.Entities;
using DeuxOrders.Domain.Enums;
using DeuxOrders.Domain.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;
    private readonly IUnitOfWork _unitOfWork;

    public AuthController(IUserRepository userRepository, ITokenService tokenService, IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
        _unitOfWork = unitOfWork;
    }

    [Authorize]
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var existingUser = await _userRepository.GetByEmail(request.Email) ?? await _userRepository.GetByUsername(request.Username);
        if (existingUser != null)
            return BadRequest("Usuário (Email ou Username) já cadastrado.");

        var hash = await Task.Run(() => BCrypt.Net.BCrypt.HashPassword(request.Password));

        var user = new User(request.Name, request.Username, hash, request.Email, UserRole.User);

        _userRepository.Add(user);

        var success = await _unitOfWork.CommitAsync();
        if (!success)
            return BadRequest("Falha ao registrar o usuário no banco de dados.");

        return Ok("Usuário registrado com sucesso!");
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _userRepository.GetByEmail(request.Email);

        if (user == null)
            return Unauthorized("Credenciais inválidas.");

        var isValid = await Task.Run(() => BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash));
        if (!isValid)
            return Unauthorized("Credenciais inválidas.");

        var token = _tokenService.GenerateToken(user);

        return Ok(new { token });
    }
}