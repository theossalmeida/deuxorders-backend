using System.ComponentModel.DataAnnotations;

namespace DeuxOrders.API.DTOs;

public record RegisterRequest(
    [Required(ErrorMessage = "Nome é obrigatório")] string Name,
    [Required] string Username,
    [Required, EmailAddress(ErrorMessage = "E-mail inválido")] string Email,
    [Required, MinLength(6, ErrorMessage = "A senha deve ter no mínimo 6 caracteres")] string Password
);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password
);