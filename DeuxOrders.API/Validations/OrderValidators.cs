using DeuxOrders.Application.DTOs;
using DeuxOrders.Domain.Enums;
using FluentValidation;

namespace DeuxOrders.API.Validations
{
    public class UpdateOrderRequestValidator : AbstractValidator<UpdateOrderRequest>
    {
        public UpdateOrderRequestValidator()
        {
            RuleFor(x => x.DeliveryDate)
                .GreaterThanOrEqualTo(DateTime.UtcNow.Date)
                .WithMessage("A data de entrega não pode ser no passado.")
                .When(x => x.DeliveryDate.HasValue);

            RuleFor(x => x.Status)
                .Must(s => !s.HasValue || Enum.IsDefined(typeof(OrderStatus), s.Value))
                .WithMessage("Status inválido.");

            RuleFor(x => x.References)
                .Must(r => r == null || r.Count <= 3)
                .WithMessage("Um pedido pode ter no máximo 3 referências.")
                .Must(r => r == null || r.All(key => !string.IsNullOrWhiteSpace(key)))
                .WithMessage("Chave de referência inválida.");

            RuleForEach(x => x.Items).SetValidator(new UpdateOrderItemRequestValidator())
                .When(x => x.Items != null && x.Items.Count > 0);
        }
    }

    public class UpdateOrderItemRequestValidator : AbstractValidator<UpdateOrderItemRequest>
    {
        public UpdateOrderItemRequestValidator()
        {
            RuleFor(x => x.ProductId)
                .NotEmpty()
                .WithMessage("O produto é obrigatório.");

            RuleFor(x => x.Quantity)
                .GreaterThan(0)
                .WithMessage("A quantidade deve ser maior que zero.")
                .When(x => x.Quantity.HasValue);

            RuleFor(x => x.PaidUnitPrice)
                .GreaterThanOrEqualTo(0)
                .WithMessage("O preço unitário não pode ser negativo.")
                .When(x => x.PaidUnitPrice.HasValue);

            RuleFor(x => x.Observation)
                .MaximumLength(500)
                .WithMessage("A observação não pode exceder 500 caracteres.");
        }
    }

    public class UpdateItemQuantityRequestValidator : AbstractValidator<UpdateItemQuantityRequest>
    {
        public UpdateItemQuantityRequestValidator()
        {
            RuleFor(x => x.Increment)
                .NotEqual(0)
                .WithMessage("O incremento não pode ser zero.");
        }
    }
}
