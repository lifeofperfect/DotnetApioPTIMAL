using OptimalApi.Api.Contracts;
using OptimalApi.Api.Domain;
using OptimalApi.Api.Validators;

namespace OptimalApi.UnitTests.Validators;

public sealed class CreateProductRequestValidatorTests
{
    private readonly CreateProductRequestValidator _validator = new();

    [Fact]
    public void ValidRequestPasses() =>
        Assert.True(_validator.Validate(new CreateProductRequest("Keyboard", 49.99m)).IsValid);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankNameFails(string name) =>
        AssertFailsOn(_validator.Validate(new CreateProductRequest(name, 10m)), nameof(CreateProductRequest.Name));

    [Fact]
    public void NameAtMaxLengthPasses() =>
        Assert.True(_validator.Validate(new CreateProductRequest(new string('a', Product.NameMaxLength), 1m)).IsValid);

    [Fact]
    public void NameOverMaxLengthFails() =>
        AssertFailsOn(
            _validator.Validate(new CreateProductRequest(new string('a', Product.NameMaxLength + 1), 1m)),
            nameof(CreateProductRequest.Name));

    [Fact]
    public void ZeroPricePasses() =>
        Assert.True(_validator.Validate(new CreateProductRequest("Free sample", 0m)).IsValid);

    [Fact]
    public void NegativePriceFails() =>
        AssertFailsOn(_validator.Validate(new CreateProductRequest("Keyboard", -0.01m)), nameof(CreateProductRequest.Price));

    [Fact]
    public void PriceWithMoreThanTwoDecimalsFails() =>
        AssertFailsOn(_validator.Validate(new CreateProductRequest("Keyboard", 1.999m)), nameof(CreateProductRequest.Price));

    internal static void AssertFailsOn(FluentValidation.Results.ValidationResult result, string property)
    {
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == property);
    }
}

public sealed class UpdateProductRequestValidatorTests
{
    private readonly UpdateProductRequestValidator _validator = new();

    [Fact]
    public void ValidRequestPasses() =>
        Assert.True(_validator.Validate(new UpdateProductRequest("Keyboard", 49.99m, Version: 1234)).IsValid);

    [Fact]
    public void MissingVersionFails() =>
        CreateProductRequestValidatorTests.AssertFailsOn(
            _validator.Validate(new UpdateProductRequest("Keyboard", 49.99m, Version: 0)),
            nameof(UpdateProductRequest.Version));

    [Fact]
    public void SharesNameAndPriceRulesWithCreate()
    {
        var result = _validator.Validate(new UpdateProductRequest("", -1m, Version: 1));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateProductRequest.Name));
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(UpdateProductRequest.Price));
    }
}

public sealed class PaginationQueryValidatorTests
{
    private readonly PaginationQueryValidator _validator = new();

    [Fact]
    public void DefaultsPass() => Assert.True(_validator.Validate(new PaginationQuery()).IsValid);

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, PaginationQuery.MaxPageSize + 1)]
    public void OutOfRangeValuesFail(int page, int pageSize) =>
        Assert.False(_validator.Validate(new PaginationQuery { Page = page, PageSize = pageSize }).IsValid);
}
