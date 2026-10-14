using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using OptimalApi.Api.Contracts;
using OptimalApi.Api.Domain;
using OptimalApi.Api.Infrastructure;

namespace OptimalApi.Api.Services;

public sealed class ProductService(AppDbContext db) : IProductService
{
    // Translated to SQL, so list/get queries select only the columns the response needs.
    private static readonly Expression<Func<Product, ProductResponse>> ToResponse = p =>
        new ProductResponse(p.Id, p.Name, p.Price, p.CreatedAt, p.UpdatedAt, p.Version);

    private static readonly Func<Product, ProductResponse> ToResponseCompiled = ToResponse.Compile();

    public async Task<PagedResponse<ProductResponse>> GetPagedAsync(
        PaginationQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var products = db.Products.AsNoTracking();

        var totalCount = await products.CountAsync(cancellationToken);

        // UUIDv7 ids are time-ordered, so ordering by id gives a stable "oldest first" order.
        var items = await products
            .OrderBy(p => p.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(ToResponse)
            .ToListAsync(cancellationToken);

        return new PagedResponse<ProductResponse>(items, query.Page, query.PageSize, totalCount);
    }

    public Task<ProductResponse?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Products
            .AsNoTracking()
            .Where(p => p.Id == id)
            .Select(ToResponse)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<ProductResponse> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var product = new Product(request.Name, request.Price);

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        return ToResponseCompiled(product);
    }

    public async Task<UpdateProductResult> UpdateAsync(
        Guid id, UpdateProductRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var product = await db.Products.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
        {
            return UpdateProductResult.NotFound;
        }

        // Compare against the version the client last saw, not the one just loaded.
        // EF then issues "UPDATE ... WHERE id = @id AND xmin = @clientVersion".
        db.Entry(product).Property(p => p.Version).OriginalValue = request.Version;

        product.Update(request.Name, request.Price);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return UpdateProductResult.Conflict;
        }

        return UpdateProductResult.Updated(ToResponseCompiled(product));
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        // Single DELETE statement; no need to load the row first.
        await db.Products.Where(p => p.Id == id).ExecuteDeleteAsync(cancellationToken) > 0;
}
