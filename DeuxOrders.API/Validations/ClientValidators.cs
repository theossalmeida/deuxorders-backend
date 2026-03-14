using FluentValidation;

namespace DeuxOrders.API.Validations
{
    public class CreateClientValidator : AbstractValidator<CreateClient>
    {
        public CreateClientValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("O nome do cliente é obrigatório.")
                .MaximumLength(150).WithMessage("O nome não pode exceder 150 caracteres.");

            RuleFor(x => x.Mobile)
                .MaximumLength(20).WithMessage("O telefone não pode exceder 20 caracteres.")
                .When(x => !string.IsNullOrWhiteSpace(x.Mobile));
        }
    }

    public class UpdateClientValidator : AbstractValidator<UpdateClient>
    {
        public UpdateClientValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("O nome do cliente é obrigatório.")
                .MaximumLength(150).WithMessage("O nome não pode exceder 150 caracteres.");

            RuleFor(x => x.Mobile)
                .MaximumLength(20).WithMessage("O telefone não pode exceder 20 caracteres.")
                .When(x => !string.IsNullOrWhiteSpace(x.Mobile));
        }
    }
}