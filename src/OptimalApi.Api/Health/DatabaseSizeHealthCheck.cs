using Microsoft.Extensions.Diagnostics.HealthChecks;
using OptimalApi.Api.Infrastructure;

namespace OptimalApi.Api.Health;

/// <summary>
/// How big the database is and how many products it holds (PostgreSQL's estimate, so it costs nothing to read,
/// unlike COUNT(*) on a large table). Degraded above <see cref="DegradedSizeMb"/> as an early warning to plan capacity.
/// </summary>
public sealed class DatabaseSizeHealthCheck(AppDbContext db) : IHealthCheck
{
    public const double DegradedSizeMb = 10 * 1024; // 10 GB

    private const string Sql = """
        SELECT
            pg_database_size(current_database())                                         AS database_bytes,
            coalesce((SELECT n_live_tup FROM pg_stat_user_tables WHERE relname = 'products'), 0) AS product_rows,
            coalesce((SELECT pg_total_relation_size('products')), 0)                     AS products_bytes
        """;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var row = await db.WithConnectionAsync(c => c.QueryRowAsync(Sql, cancellationToken), cancellationToken);

        var sizeMb = ToMegabytes(row.Long("database_bytes"));
        var data = new Dictionary<string, object>
        {
            ["databaseSizeMb"] = sizeMb,
            ["productsTableMb"] = ToMegabytes(row.Long("products_bytes")),
            ["productRowsEstimate"] = row.Long("product_rows"),
            ["thresholdMb"] = DegradedSizeMb,
        };

        var summary = $"Database is {sizeMb} MB with about {row.Long("product_rows")} product(s).";

        return sizeMb > DegradedSizeMb
            ? HealthCheckResult.Degraded($"{summary} Above the {DegradedSizeMb} MB threshold: plan for growth.", data: data)
            : HealthCheckResult.Healthy(summary, data);
    }

    private static double ToMegabytes(long bytes) => Math.Round(bytes / 1024d / 1024d, 2);
}
