using DeuxOrders.Domain.Entities;
using DeuxOrders.Domain.Enums;
using DeuxOrders.Domain.Interfaces;
using Microsoft.AspNetCore.Mvc;
using DeuxOrders.API.DTOs;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly IUserRepository _userRepository;
    private readonly ITokenService _tokenService;

    public AuthController(IUserRepository userRepository, ITokenService tokenService)
    {
        _userRepository = userRepository;
        _tokenService = tokenService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (await _userRepository.GetByEmail(request.Email) != null)
            return BadRequest("Este e-mail já está em uso.");

        var hash = BCrypt.Net.BCrypt.HashPassword(request.Password);

        var user = new User(request.Name, request.Username, hash, request.Email, UserRole.User);

        await _userRepository.AddAsync(user);
        return Ok("Usuário registrado com sucesso!");
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _userRepository.GetByEmail(request.Email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized("E-mail ou senha inválidos.");

        var token = _tokenService.GenerateToken(user);

        return Ok(new { token });
    }
}