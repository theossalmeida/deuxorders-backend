using FluentValidation;

namespace DeuxOrders.API.Validations
{
    public class CreateProductValidator : AbstractValidator<CreateProduct>
    {
        public CreateProductValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("O nome do produto é obrigatório.")
                .MaximumLength(100).WithMessage("O nome não pode exceder 100 caracteres.");

            RuleFor(x => x.Price)
                .GreaterThan(0).WithMessage("O preço do produto deve ser maior que zero.");
        }
    }
}