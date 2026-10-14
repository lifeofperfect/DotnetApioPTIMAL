using Microsoft.Extensions.Diagnostics.HealthChecks;
using OptimalApi.Api.Infrastructure;

namespace OptimalApi.Api.Health;

/// <summary>
/// How much of PostgreSQL's connection limit is in use (by every client, not just this app).
/// When it runs out, new requests fail with "too many clients".
/// Degraded above 80%, Unhealthy above 95%.
/// </summary>
public sealed class DatabaseConnectionsHealthCheck(AppDbContext db) : IHealthCheck
{
    public const double DegradedPercent = 80;
    public const double UnhealthyPercent = 95;

    private const string Sql = """
        SELECT
            current_setting('max_connections')::int                                   AS max_connections,
            count(*)                                                                  AS total,
            count(*) FILTER (WHERE state = 'active')                                  AS active,
            count(*) FILTER (WHERE state = 'idle')                                    AS idle,
            count(*) FILTER (WHERE state LIKE 'idle in transaction%')                 AS idle_in_transaction,
            count(*) FILTER (WHERE datname = current_database())                      AS this_database
        FROM pg_stat_activity
        WHERE backend_type = 'client backend'
        """;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var row = await db.WithConnectionAsync(c => c.QueryRowAsync(Sql, cancellationToken), cancellationToken);

        var max = row.Long("max_connections");
        var total = row.Long("total");
        var usedPercent = max == 0 ? 0 : Math.Round(total * 100d / max, 1);

        var data = new Dictionary<string, object>
        {
            ["used"] = total,
            ["max"] = max,
            ["usedPercent"] = usedPercent,
            ["active"] = row.Long("active"),
            ["idle"] = row.Long("idle"),
            ["idleInTransaction"] = row.Long("idle_in_transaction"),
            ["thisDatabase"] = row.Long("this_database"),
        };

        var summary = $"{total} of {max} connections in use ({usedPercent}%).";

        return usedPercent switch
        {
            >= UnhealthyPercent => HealthCheckResult.Unhealthy($"Almost out of connections: {summary}", data: data),
            >= DegradedPercent => HealthCheckResult.Degraded($"Connections running low: {summary}", data: data),
            _ => HealthCheckResult.Healthy(summary, data),
        };
    }
}
