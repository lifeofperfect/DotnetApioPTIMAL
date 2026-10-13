using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace OptimalApi.IntegrationTests;

public sealed class HealthCheckTests : IClassFixture<HealthCheckTests.ApiFactory>
{
    private readonly ApiFactory _factory;

    public HealthCheckTests(ApiFactory factory) => _factory = factory;

    [Fact]
    public async Task LivenessEndpointReturnsOkWithoutTouchingTheDatabase()
    {
        using var client = _factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/health/live", UriKind.Relative));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public sealed class ApiFactory : WebApplicationFactory<Program>
    {
        // Liveness never opens a connection, so an unreachable placeholder is enough here.
        // Step 5 replaces this with a real Postgres from Testcontainers.
        protected override void ConfigureWebHost(IWebHostBuilder builder) =>
            builder.UseSetting("ConnectionStrings:Default", "Host=127.0.0.1;Port=1;Database=unused;Username=unused");
    }
}
