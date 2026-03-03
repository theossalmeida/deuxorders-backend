using DeuxOrders.API.DTOs;
using FluentValidation;

namespace DeuxOrders.API.Validations
{
    public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
    {
        public RegisterRequestValidator()
        {
            RuleFor(x => x.Name).NotEmpty().WithMessage("O nome é obrigatório.");
            RuleFor(x => x.Username).NotEmpty().WithMessage("O username é obrigatório.");
            RuleFor(x => x.Password).MinimumLength(6).WithMessage("A senha deve ter no mínimo 6 caracteres.");
            RuleFor(x => x.Email).NotEmpty().EmailAddress().WithMessage("E-mail inválido.");
        }
    }
}