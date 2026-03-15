using DeuxOrders.Application.DTOs;
using FluentValidation;

namespace DeuxOrders.API.Validations
{
    public class CreateOrderRequestValidator : AbstractValidator<CreateOrderRequest>
    {
        public CreateOrderRequestValidator()
        {
            RuleFor(x => x.ClientId)
                .NotEmpty()
                .WithMessage("O cliente é obrigatório.");

            RuleFor(x => x.DeliveryDate)
                .NotEmpty()
                .WithMessage("A data de entrega é obrigatória.");

            RuleFor(x => x.Items)
                .NotEmpty()
                .WithMessage("O pedido deve conter ao menos um item.");

            RuleForEach(x => x.Items).SetValidator(new CreateOrderItemValidator());

            RuleFor(x => x.References)
                .Must(r => r == null || r.Count <= 3)
                .WithMessage("Um pedido pode ter no máximo 3 referências.")
                .Must(r => r == null || r.All(key => !string.IsNullOrWhiteSpace(key)))
                .WithMessage("Chave de referência inválida.");
        }
    }

    public class CreateOrderItemValidator : AbstractValidator<CreateOrderItemRequest>
    {
        public CreateOrderItemValidator()
        {
            RuleFor(x => x.ProductId)
                .NotEmpty()
                .WithMessage("O produto é obrigatório.");

            RuleFor(x => x.Quantity)
                .GreaterThanOrEqualTo(0)
                .WithMessage("A quantidade não pode ser negativa.");

            RuleFor(x => x.UnitPrice)
                .GreaterThanOrEqualTo(0)
                .WithMessage("O preço unitário não pode ser negativo.");

            RuleFor(x => x.Observation)
                .MaximumLength(500)
                .WithMessage("A observação não pode exceder 500 caracteres.");
        }
    }
}