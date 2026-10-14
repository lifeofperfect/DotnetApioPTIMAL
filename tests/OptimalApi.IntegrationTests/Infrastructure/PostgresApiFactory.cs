using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OptimalApi.Api.Infrastructure;
using Testcontainers.PostgreSql;

namespace OptimalApi.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the real API in memory against a real, throwaway PostgreSQL container.
/// One container per test class (via IClassFixture); the database is migrated with the same
/// migrations production uses, so tests fail if a migration is wrong.
/// </summary>
public sealed class PostgresApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    // Same image as docker-compose.yml, so tests run against the version production uses.
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder
            .UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString())
            // The dashboard polls over real HTTP, which the in-memory test server doesn't have.
            .UseSetting("HealthChecksUI:Enabled", "false");

    /// <summary>Empties the products table so each test starts from a known state.</summary>
    public async Task ResetDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Products.ExecuteDeleteAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }
}
