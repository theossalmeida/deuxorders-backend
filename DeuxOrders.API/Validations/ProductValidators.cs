using DeuxOrders.API.Models;
using FluentValidation;

namespace DeuxOrders.API.Validations
{
    public class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
    {
        private static readonly string[] AllowedImageTypes = ["image/jpeg", "image/png", "image/webp"];

        public CreateProductRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("O nome do produto é obrigatório.")
                .MaximumLength(100).WithMessage("O nome não pode exceder 100 caracteres.");

            RuleFor(x => x.Price)
                .GreaterThan(0).WithMessage("O preço do produto deve ser maior que zero.");

            When(x => x.Image != null, () =>
            {
                RuleFor(x => x.Image!.ContentType)
                    .Must(ct => AllowedImageTypes.Contains(ct))
                    .WithMessage("A imagem deve ser JPEG, PNG ou WebP.");

                RuleFor(x => x.Image!.Length)
                    .LessThanOrEqualTo(5 * 1024 * 1024)
                    .WithMessage("A imagem não pode exceder 5 MB.");
            });
        }
    }

    public class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
    {
        private static readonly string[] AllowedImageTypes = ["image/jpeg", "image/png", "image/webp"];

        public UpdateProductRequestValidator()
        {
            RuleFor(x => x.Name)
                .NotEmpty().WithMessage("O nome do produto é obrigatório.")
                .MaximumLength(100).WithMessage("O nome não pode exceder 100 caracteres.");

            RuleFor(x => x.Price)
                .GreaterThan(0).WithMessage("O preço do produto deve ser maior que zero.");

            When(x => x.Image != null, () =>
            {
                RuleFor(x => x.Image!.ContentType)
                    .Must(ct => AllowedImageTypes.Contains(ct))
                    .WithMessage("A imagem deve ser JPEG, PNG ou WebP.");

                RuleFor(x => x.Image!.Length)
                    .LessThanOrEqualTo(5 * 1024 * 1024)
                    .WithMessage("A imagem não pode exceder 5 MB.");
            });
        }
    }
}
