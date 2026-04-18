using DeuxERP.Application.DTOs;
using DeuxERP.Domain.Cash.Enums;
using FluentValidation;

namespace DeuxERP.API.Validations
{
    public class UnpayRequestValidator : AbstractValidator<UnpayRequest>
    {
        public UnpayRequestValidator()
        {
            RuleFor(x => x.Reason)
                .NotEmpty().WithMessage("O motivo da reversão é obrigatório.")
                .MinimumLength(5).WithMessage("O motivo deve ter no mínimo 5 caracteres.")
                .MaximumLength(500).WithMessage("O motivo não pode exceder 500 caracteres.");
        }
    }

    public class CreateCashEntryRequestValidator : AbstractValidator<CreateCashEntryRequest>
    {
        public CreateCashEntryRequestValidator()
        {
            RuleFor(x => x.BillingDate)
                .NotEmpty().WithMessage("A data de competência é obrigatória.");

            RuleFor(x => x.Type)
                .IsInEnum().WithMessage("Tipo inválido.");

            RuleFor(x => x.Category)
                .IsInEnum().WithMessage("Categoria inválida.");

            RuleFor(x => x.Counterparty)
                .NotEmpty().WithMessage("A contraparte é obrigatória.")
                .MaximumLength(200).WithMessage("A contraparte não pode exceder 200 caracteres.");

            RuleFor(x => x.AmountCents)
                .GreaterThan(0).WithMessage("O valor deve ser maior que zero.");

            RuleFor(x => x.Notes)
                .MaximumLength(2000).WithMessage("As notas não podem exceder 2000 caracteres.")
                .When(x => x.Notes != null);
        }
    }

    public class UpdateCashEntryRequestValidator : AbstractValidator<UpdateCashEntryRequest>
    {
        public UpdateCashEntryRequestValidator()
        {
            RuleFor(x => x.BillingDate)
                .NotEmpty().WithMessage("A data de competência é obrigatória.");

            RuleFor(x => x.Type)
                .IsInEnum().WithMessage("Tipo inválido.");

            RuleFor(x => x.Category)
                .IsInEnum().WithMessage("Categoria inválida.");

            RuleFor(x => x.Counterparty)
                .NotEmpty().WithMessage("A contraparte é obrigatória.")
                .MaximumLength(200).WithMessage("A contraparte não pode exceder 200 caracteres.");

            RuleFor(x => x.AmountCents)
                .GreaterThan(0).WithMessage("O valor deve ser maior que zero.");

            RuleFor(x => x.Notes)
                .MaximumLength(2000).WithMessage("As notas não podem exceder 2000 caracteres.")
                .When(x => x.Notes != null);
        }
    }

    public class DeleteCashEntryRequestValidator : AbstractValidator<DeleteCashEntryRequest>
    {
        public DeleteCashEntryRequestValidator()
        {
            RuleFor(x => x.Reason)
                .NotEmpty().WithMessage("O motivo da exclusão é obrigatório.")
                .MinimumLength(5).WithMessage("O motivo deve ter no mínimo 5 caracteres.")
                .MaximumLength(500).WithMessage("O motivo não pode exceder 500 caracteres.");
        }
    }
}
