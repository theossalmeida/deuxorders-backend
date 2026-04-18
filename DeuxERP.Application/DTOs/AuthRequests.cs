using System.ComponentModel.DataAnnotations;

namespace DeuxERP.API.DTOs;

public record RegisterRequest(
    [Required(ErrorMessage = "Nome é obrigatório")] string Name,
    [Required] string Username,
    [Required, EmailAddress(ErrorMessage = "E-mail inválido")] string Email,
    [Required, MinLength(8, ErrorMessage = "A senha deve ter no mínimo 8 caracteres")] string Password
);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password
);