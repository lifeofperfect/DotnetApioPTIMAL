using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OptimalApi.Api.Health;

/// <summary>
/// All health check wiring in one place.
/// <list type="table">
/// <item><term>/health/live</term><description>Process is up. Runs no checks. For Docker/Kubernetes liveness probes.</description></item>
/// <item><term>/health/ready</term><description>Database is usable. For readiness probes and load balancers.</description></item>
/// <item><term>/health</term><description>Every check with details (latency, versions, memory) as JSON. Development only.</description></item>
/// <item><term>/health-ui</term><description>Dashboard that polls /health and keeps a short history. Development only.</description></item>
/// </list>
/// </summary>
public static class HealthCheckSetup
{
    public const string ReadyTag = "ready";
    public const string DatabaseTag = "database";
    public const string RuntimeTag = "runtime";
    public const string InfoTag = "info";

    private const string DetailsPath = "/health";
    private const string DashboardPath = "/health-ui";

    public static IServiceCollection AddAppHealthChecks(this IServiceCollection services, IConfiguration configuration)
    {
        // Only "database" carries the ready tag. Everything else is information for the dashboard and /health:
        // a warning there (slow GC, low disk, a long query) must never take the app out of the load balancer.
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: [ReadyTag, DatabaseTag])
            .AddCheck<DatabaseConnectionsHealthCheck>("database-connections", tags: [DatabaseTag])
            .AddCheck<DatabaseActivityHealthCheck>("database-activity", tags: [DatabaseTag])
            .AddCheck<DatabaseSizeHealthCheck>("database-size", tags: [DatabaseTag])
            .AddCheck<ThreadPoolHealthCheck>("thread-pool", tags: [RuntimeTag])
            .AddCheck<GarbageCollectionHealthCheck>("garbage-collection", tags: [RuntimeTag])
            .AddCheck<MemoryHealthCheck>("memory", tags: [RuntimeTag])
            .AddCheck<DiskSpaceHealthCheck>("disk-space", tags: [RuntimeTag])
            .AddCheck<AppInfoHealthCheck>("app-info", tags: [InfoTag]);

        // Retries in the EF Core execution strategy would otherwise keep a probe hanging for ~30s when Postgres is down.
        // Probes must answer fast, so cap every check.
        services.PostConfigure<HealthCheckServiceOptions>(options =>
        {
            foreach (var registration in options.Registrations)
            {
                registration.Timeout = TimeSpan.FromSeconds(5);
            }
        });

        if (IsDashboardEnabled(configuration))
        {
            // Where the dashboard polls. A relative path works with "dotnet run"; in a container the app listens on
            // 0.0.0.0, which can't be called, so docker-compose sets HealthChecksUI__EndpointUrl to an absolute URL.
            var endpointUrl = configuration.GetValue("HealthChecksUI:EndpointUrl", DetailsPath)!;

            services
                .AddHealthChecksUI(settings => settings
                    .AddHealthCheckEndpoint("OptimalApi", endpointUrl)
                    .SetEvaluationTimeInSeconds(15)
                    .MaximumHistoryEntriesPerEndpoint(60))
                .AddInMemoryStorage();
        }

        return services;
    }

    public static WebApplication MapAppHealthChecks(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains(ReadyTag) });

        // Details reveal internals (server version, memory), so they are not exposed outside Development.
        // To use them in production, put them behind authentication: .RequireAuthorization(...).
        if (app.Environment.IsDevelopment())
        {
            app.MapHealthChecks(DetailsPath, new HealthCheckOptions
            {
                ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse,
            });
        }

        if (IsDashboardEnabled(app.Configuration))
        {
            app.MapHealthChecksUI(options => options.UIPath = DashboardPath);
        }

        return app;
    }

    // The dashboard polls /health over HTTP, so it is only switched on where /health exists (Development),
    // and can be turned off (e.g. in tests) with HealthChecksUI:Enabled=false.
    private static bool IsDashboardEnabled(IConfiguration configuration) =>
        configuration.GetValue("HealthChecksUI:Enabled", defaultValue: false);
}
