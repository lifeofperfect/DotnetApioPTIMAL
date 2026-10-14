using System.Net;
using System.Net.Http.Json;
using OptimalApi.Api.Contracts;
using OptimalApi.IntegrationTests.Infrastructure;

namespace OptimalApi.IntegrationTests.Products;

/// <summary>End-to-end tests through HTTP: routing, validation, the service, EF Core and PostgreSQL together.</summary>
public sealed class ProductsApiTests : IClassFixture<PostgresApiFactory>, IAsyncLifetime
{
    private const string Products = "/api/products";

    private readonly PostgresApiFactory _factory;
    private readonly HttpClient _client;

    public ProductsApiTests(PostgresApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    public Task InitializeAsync() => _factory.ResetDatabaseAsync();

    public Task DisposeAsync()
    {
        _client.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CreateReturns201WithLocationAndTheStoredProduct()
    {
        using var response = await _client.PostAsJsonAsync(Products, new CreateProductRequest("  Keyboard  ", 49.99m));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await ReadAsync<ProductResponse>(response);
        Assert.True(body.Success);
        Assert.Equal("Keyboard", body.Data!.Name);
        Assert.Equal(49.99m, body.Data.Price);
        Assert.NotEqual(0u, body.Data.Version);
        Assert.Equal($"/api/products/{body.Data.Id}", response.Headers.Location!.AbsolutePath);
    }

    [Fact]
    public async Task GetByIdReturnsWhatWasCreated()
    {
        var created = await CreateAsync("Mouse", 25m);

        using var response = await _client.GetAsync(new Uri($"{Products}/{created.Id}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(created, (await ReadAsync<ProductResponse>(response)).Data);
    }

    [Fact]
    public async Task ListIsPagedOldestFirst()
    {
        for (var i = 1; i <= 5; i++)
        {
            await CreateAsync($"Item {i}", i);
        }

        using var response = await _client.GetAsync(new Uri($"{Products}?page=2&pageSize=2", UriKind.Relative));

        var page = (await ReadAsync<PagedResponse<ProductResponse>>(response)).Data!;
        Assert.Equal(5, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(["Item 3", "Item 4"], page.Items.Select(p => p.Name));
        Assert.True(page.HasNextPage);
        Assert.True(page.HasPreviousPage);
    }

    [Fact]
    public async Task UpdateWithCurrentVersionSucceedsAndReturnsNewVersion()
    {
        var created = await CreateAsync("Keyboard", 49.99m);

        using var response = await _client.PutAsJsonAsync(
            $"{Products}/{created.Id}", new UpdateProductRequest("Keyboard RGB", 59.99m, created.Version));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var updated = (await ReadAsync<ProductResponse>(response)).Data!;
        Assert.Equal("Keyboard RGB", updated.Name);
        Assert.NotEqual(created.Version, updated.Version);
        Assert.NotNull(updated.UpdatedAt);
    }

    [Fact]
    public async Task UpdateWithStaleVersionReturns409AndKeepsTheFirstEdit()
    {
        var created = await CreateAsync("Keyboard", 49.99m);

        // Alice saves first...
        using var first = await _client.PutAsJsonAsync(
            $"{Products}/{created.Id}", new UpdateProductRequest("Alice's edit", 45m, created.Version));
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // ...Bob still holds the version he loaded before Alice saved.
        using var second = await _client.PutAsJsonAsync(
            $"{Products}/{created.Id}", new UpdateProductRequest("Bob's edit", 1m, created.Version));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.False((await ReadAsync<object>(second)).Success);
        Assert.Equal("Alice's edit", (await GetAsync(created.Id)).Name);
    }

    [Fact]
    public async Task InvalidCreateReturns400WithFieldErrors()
    {
        using var response = await _client.PostAsJsonAsync(Products, new CreateProductRequest("", -1m));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await ReadAsync<object>(response);
        Assert.False(body.Success);
        Assert.Equal("Validation failed.", body.Message);
        Assert.Contains("name", body.Errors!.Keys);
        Assert.Contains("price", body.Errors.Keys);
        Assert.False(string.IsNullOrEmpty(body.TraceId));
    }

    [Fact]
    public async Task UnknownProductReturns404InTheStandardEnvelope()
    {
        using var response = await _client.GetAsync(new Uri($"{Products}/{Guid.NewGuid()}", UriKind.Relative));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await ReadAsync<object>(response);
        Assert.False(body.Success);
        Assert.Null(body.Data);
    }

    [Fact]
    public async Task DeleteRemovesTheProductAndSecondDeleteIs404()
    {
        var created = await CreateAsync("Temporary", 1m);
        var uri = new Uri($"{Products}/{created.Id}", UriKind.Relative);

        using var deleted = await _client.DeleteAsync(uri);
        using var again = await _client.DeleteAsync(uri);
        using var get = await _client.GetAsync(uri);

        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task ReadinessIsHealthyWhenTheDatabaseIsUp()
    {
        using var response = await _client.GetAsync(new Uri("/health/ready", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<ProductResponse> CreateAsync(string name, decimal price)
    {
        using var response = await _client.PostAsJsonAsync(Products, new CreateProductRequest(name, price));
        response.EnsureSuccessStatusCode();
        return (await ReadAsync<ProductResponse>(response)).Data!;
    }

    private async Task<ProductResponse> GetAsync(Guid id)
    {
        using var response = await _client.GetAsync(new Uri($"{Products}/{id}", UriKind.Relative));
        response.EnsureSuccessStatusCode();
        return (await ReadAsync<ProductResponse>(response)).Data!;
    }

    private static async Task<ApiResponse<T>> ReadAsync<T>(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<ApiResponse<T>>()
        ?? throw new InvalidOperationException("Response body was empty.");
}
