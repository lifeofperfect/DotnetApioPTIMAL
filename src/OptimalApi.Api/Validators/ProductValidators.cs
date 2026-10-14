using FluentValidation;
using OptimalApi.Api.Contracts;
using OptimalApi.Api.Domain;

namespace OptimalApi.Api.Validators;

public sealed class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(x => x.Name).ProductName();
        RuleFor(x => x.Price).ProductPrice();
    }
}

public sealed class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(x => x.Name).ProductName();
        RuleFor(x => x.Price).ProductPrice();
        RuleFor(x => x.Version)
            .NotEmpty().WithMessage("Version is required. Use the value from the latest GET of this product.");
    }
}

public sealed class PaginationQueryValidator : AbstractValidator<PaginationQuery>
{
    public PaginationQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, PaginationQuery.MaxPageSize);
    }
}

/// <summary>Rules shared by create and update, so the two can never drift apart.</summary>
internal static class ProductRules
{
    public static IRuleBuilderOptions<T, string> ProductName<T>(this IRuleBuilder<T, string> rule) =>
        rule
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("Name is required.")
            .Must(name => name is null || name.Trim().Length <= Product.NameMaxLength)
            .WithMessage($"Name must be at most {Product.NameMaxLength} characters.");

    // Matches the numeric(18,2) column, so anything that passes validation can be stored as-is.
    public static IRuleBuilderOptions<T, decimal> ProductPrice<T>(this IRuleBuilder<T, decimal> rule) =>
        rule
            .GreaterThanOrEqualTo(0)
            .PrecisionScale(18, 2, ignoreTrailingZeros: true);
}
