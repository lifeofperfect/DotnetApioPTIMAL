namespace OptimalApi.Api.Contracts;

/// <summary>Body of POST /api/products.</summary>
public sealed record CreateProductRequest(string Name, decimal Price);

/// <summary>
/// Body of PUT /api/products/{id}. <see cref="Version"/> must be the value from the last read;
/// if someone else changed the product since, the update is rejected with 409 Conflict.
/// </summary>
public sealed record UpdateProductRequest(string Name, decimal Price, uint Version);

/// <summary>A product as returned by the API. Entities are never exposed directly.</summary>
public sealed record ProductResponse(
    Guid Id,
    string Name,
    decimal Price,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    uint Version);
