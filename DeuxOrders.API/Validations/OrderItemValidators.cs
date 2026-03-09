using DeuxOrders.Application.DTOs;
using FluentValidation;

namespace DeuxOrders.API.Validations
{
    public class CreateOrderItemValidator : AbstractValidator<CreateOrderItemRequest>
    {
        public CreateOrderItemValidator()
        {
            RuleFor(x => x.Observation)
                .MaximumLength(500)
                .WithMessage("A observação não pode exceder 500 caracteres.");
        }
    }
}