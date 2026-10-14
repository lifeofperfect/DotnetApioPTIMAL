using Microsoft.AspNetCore.Mvc;
using OptimalApi.Api.Contracts;
using OptimalApi.Api.Http;
using OptimalApi.Api.Services;

namespace OptimalApi.Api.Controllers;

/// <summary>
/// HTTP layer for products. It only maps requests and results to status codes;
/// validation runs in <see cref="Filters.FluentValidationFilter"/> and the logic lives in <see cref="IProductService"/>.
/// Every response is wrapped in <see cref="ApiResponse{T}"/>.
/// </summary>
[ApiController]
[Route("api/products")]
[Produces("application/json")]
[ProducesResponseType<ApiResponse<object>>(StatusCodes.Status500InternalServerError)]
public sealed class ProductsController(IProductService products) : ControllerBase
{
    /// <summary>Lists products, oldest first, one page at a time.</summary>
    [HttpGet]
    [ProducesResponseType<ApiResponse<PagedResponse<ProductResponse>>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAll([FromQuery] PaginationQuery query, CancellationToken cancellationToken)
    {
        var page = await products.GetPagedAsync(query, cancellationToken);

        return Ok(ApiResponse.Ok(page, "Products retrieved."));
    }

    /// <summary>Gets one product. The returned <c>version</c> is needed to update it.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ApiResponse<ProductResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        await products.GetByIdAsync(id, cancellationToken) is { } product
            ? Ok(ApiResponse.Ok(product, "Product retrieved."))
            : ProductNotFound(id);

    /// <summary>Creates a product and returns it with its Location header.</summary>
    [HttpPost]
    [ProducesResponseType<ApiResponse<ProductResponse>>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var created = await products.CreateAsync(request, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, ApiResponse.Ok(created, "Product created."));
    }

    /// <summary>
    /// Updates a product. Send the <c>version</c> from your last read; if the product changed
    /// since then, you get 409 Conflict instead of overwriting someone else's change.
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<ApiResponse<ProductResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(Guid id, UpdateProductRequest request, CancellationToken cancellationToken)
    {
        var result = await products.UpdateAsync(id, request, cancellationToken);

        // Returns the updated product (with its new version) so the client can keep editing without another GET.
        return result.Status switch
        {
            UpdateProductStatus.Updated => Ok(ApiResponse.Ok(result.Product, "Product updated.")),
            UpdateProductStatus.NotFound => ProductNotFound(id),
            UpdateProductStatus.Conflict => Conflict(ApiErrors.Create(
                HttpContext,
                "The product was changed by someone else. Reload it to get the latest version, then apply your change again.")),
            _ => throw new InvalidOperationException($"Unhandled update status '{result.Status}'."),
        };
    }

    /// <summary>Deletes a product.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiResponse<object>>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken) =>
        await products.DeleteAsync(id, cancellationToken)
            ? Ok(ApiResponse.Ok("Product deleted."))
            : ProductNotFound(id);

    private NotFoundObjectResult ProductNotFound(Guid id) =>
        NotFound(ApiErrors.Create(HttpContext, $"Product '{id}' was not found."));
}
