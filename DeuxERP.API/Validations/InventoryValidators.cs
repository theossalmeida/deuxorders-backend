using DeuxERP.API.Models;
using FluentValidation;

namespace DeuxERP.API.Validations;

public class CreateMaterialValidator : AbstractValidator<CreateMaterialRequest>
{
    public CreateMaterialValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.Quantity)
            .GreaterThan(0);

        RuleFor(x => x.TotalCost)
            .GreaterThan(0);

        RuleFor(x => x.MeasureUnit)
            .IsInEnum();
    }
}

public class UpdateMaterialValidator : AbstractValidator<UpdateMaterialRequest>
{
    public UpdateMaterialValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.MeasureUnit)
            .IsInEnum();
    }
}

public class RestockValidator : AbstractValidator<RestockRequest>
{
    public RestockValidator()
    {
        RuleFor(x => x.Quantity)
            .GreaterThan(0);

        RuleFor(x => x.TotalCost)
            .GreaterThan(0);
    }
}

public class SetRecipeValidator : AbstractValidator<SetRecipeRequest>
{
    public SetRecipeValidator()
    {
        RuleFor(x => x.Items)
            .NotNull();

        RuleFor(x => x.Items)
            .Must(items => items == null || items.Select(item => item.MaterialId).Distinct().Count() == items.Count)
            .WithMessage("Não é permitido repetir materiais na receita.");

        RuleForEach(x => x.Items)
            .SetValidator(new RecipeItemRequestValidator())
            .When(x => x.Items != null);
    }
}

public class SetRecipeOptionValidator : AbstractValidator<SetRecipeOptionRequest>
{
    public SetRecipeOptionValidator()
    {
        RuleFor(x => x.Type)
            .IsInEnum();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(x => x.Items)
            .NotNull();

        RuleFor(x => x.Items)
            .Must(items => items == null || items.Select(item => item.MaterialId).Distinct().Count() == items.Count)
            .WithMessage("Não é permitido repetir materiais na receita.");

        RuleForEach(x => x.Items)
            .SetValidator(new RecipeItemRequestValidator())
            .When(x => x.Items != null);
    }
}

public class RecipeItemRequestValidator : AbstractValidator<RecipeItemRequest>
{
    public RecipeItemRequestValidator()
    {
        RuleFor(x => x.MaterialId)
            .NotEmpty();

        RuleFor(x => x.Quantity)
            .GreaterThan(0);
    }
}
