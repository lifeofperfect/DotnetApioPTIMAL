using OptimalApi.Api.Contracts;

namespace OptimalApi.Api.Services;

/// <summary>
/// Product use cases, independent of HTTP. Controllers translate the results into status codes.
/// Callers are expected to pass requests that have already been validated.
/// </summary>
public interface IProductService
{
    Task<PagedResponse<ProductResponse>> GetPagedAsync(PaginationQuery query, CancellationToken cancellationToken);

    Task<ProductResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken);

    Task<UpdateProductResult> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken);

    /// <returns><c>true</c> if a product was deleted, <c>false</c> if it did not exist.</returns>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken);
}

public enum UpdateProductStatus
{
    Updated,
    NotFound,
    Conflict,
}

/// <summary>Outcome of an update. <see cref="Product"/> is set only when the update succeeded.</summary>
public sealed record UpdateProductResult(UpdateProductStatus Status, ProductResponse? Product = null)
{
    public static UpdateProductResult NotFound { get; } = new(UpdateProductStatus.NotFound);

    public static UpdateProductResult Conflict { get; } = new(UpdateProductStatus.Conflict);

    public static UpdateProductResult Updated(ProductResponse product) => new(UpdateProductStatus.Updated, product);
}
