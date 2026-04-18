using FluentValidation;

namespace DeuxERP.API.Validations
{
    public class PresignedUploadRequestValidator : AbstractValidator<PresignedUploadRequest>
    {
        public PresignedUploadRequestValidator()
        {
            RuleFor(x => x.FileName)
                .NotEmpty()
                .WithMessage("O nome do arquivo é obrigatório.")
                .MaximumLength(255)
                .WithMessage("O nome do arquivo não pode exceder 255 caracteres.");

            RuleFor(x => x.ContentType)
                .NotEmpty()
                .WithMessage("O tipo do conteúdo é obrigatório.");
        }
    }

    public class RemoveReferenceRequestValidator : AbstractValidator<RemoveReferenceRequest>
    {
        public RemoveReferenceRequestValidator()
        {
            RuleFor(x => x.ObjectKey)
                .NotEmpty()
                .WithMessage("A chave do objeto é obrigatória.")
                .MaximumLength(500)
                .WithMessage("A chave do objeto não pode exceder 500 caracteres.");
        }
    }
}
