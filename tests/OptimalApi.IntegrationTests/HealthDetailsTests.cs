using System.Net;
using System.Text.Json;
using OptimalApi.IntegrationTests.Infrastructure;

namespace OptimalApi.IntegrationTests;

/// <summary>The detailed /health endpoint against a real PostgreSQL.</summary>
public sealed class HealthDetailsTests(PostgresApiFactory factory) : IClassFixture<PostgresApiFactory>
{
    public static TheoryData<string> AllChecks =>
    [
        "database", "database-connections", "database-activity", "database-size",
        "thread-pool", "garbage-collection", "memory", "disk-space", "app-info",
    ];

    [Fact]
    public async Task DatabaseCheckReportsVersionAndMigrations()
    {
        var database = (await GetEntriesAsync()).GetProperty("database");

        Assert.Equal("Healthy", database.GetProperty("status").GetString());
        var data = database.GetProperty("data");
        Assert.StartsWith("17", data.GetProperty("serverVersion").GetString(), StringComparison.Ordinal);
        Assert.Equal(0, data.GetProperty("pendingMigrations").GetInt32());
        Assert.True(data.GetProperty("appliedMigrations").GetInt32() >= 1);
    }

    [Theory]
    [MemberData(nameof(AllChecks))]
    public async Task EveryCheckIsReportedWithDataAndADescription(string check)
    {
        var entry = (await GetEntriesAsync()).GetProperty(check);

        Assert.False(string.IsNullOrWhiteSpace(entry.GetProperty("description").GetString()));
        Assert.NotEqual(JsonValueKind.Undefined, entry.GetProperty("data").ValueKind);
    }

    [Theory]
    [InlineData("database-connections")]
    [InlineData("database-activity")]
    [InlineData("database-size")]
    [InlineData("app-info")]
    public async Task ChecksThatDoNotDependOnTheMachineAreHealthyOnAFreshDatabase(string check)
    {
        // Disk, memory, GC and thread pool depend on the machine running the tests, so they are not asserted here.
        var entry = (await GetEntriesAsync()).GetProperty(check);

        Assert.Equal("Healthy", entry.GetProperty("status").GetString());
    }

    [Fact]
    public async Task ConnectionsCheckSeesTheServerLimit()
    {
        var data = (await GetEntriesAsync()).GetProperty("database-connections").GetProperty("data");

        Assert.True(data.GetProperty("max").GetInt32() > 0);
        Assert.True(data.GetProperty("used").GetInt32() >= 1);
    }

    private async Task<JsonElement> GetEntriesAsync()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(new Uri("/health", UriKind.Relative));

        // 503 is valid here too (an Unhealthy machine-dependent check); the body is what's being tested.
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.ServiceUnavailable);

        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("entries").Clone();
    }
}
