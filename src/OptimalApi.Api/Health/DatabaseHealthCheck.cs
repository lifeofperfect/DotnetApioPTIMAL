using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OptimalApi.Api.Infrastructure;

namespace OptimalApi.Api.Health;

/// <summary>
/// Checks PostgreSQL in more depth than "can I connect":
/// <list type="bullet">
/// <item><b>Unhealthy</b>: the database cannot be reached or queried.</item>
/// <item><b>Degraded</b>: it works, but a query took longer than <see cref="SlowQueryThreshold"/>,
/// or migrations are pending (the schema is behind the code).</item>
/// <item><b>Healthy</b>: otherwise.</item>
/// </list>
/// Latency, server version and migration counts are reported as data for the dashboard.
/// </summary>
public sealed class DatabaseHealthCheck(AppDbContext db) : IHealthCheck
{
    public static readonly TimeSpan SlowQueryThreshold = TimeSpan.FromMilliseconds(500);

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = db.Database.GetDbConnection();
            await db.Database.OpenConnectionAsync(cancellationToken);

            try
            {
                // Round trip with a trivial query: measures real query latency, not just the TCP handshake.
                var stopwatch = Stopwatch.StartNew();
                await using (var command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT 1";
                    await command.ExecuteScalarAsync(cancellationToken);
                }

                stopwatch.Stop();

                var applied = (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).Count();
                var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

                var data = new Dictionary<string, object>
                {
                    ["latencyMs"] = Math.Round(stopwatch.Elapsed.TotalMilliseconds, 1),
                    ["serverVersion"] = connection.ServerVersion,
                    ["database"] = connection.Database,
                    ["appliedMigrations"] = applied,
                    ["pendingMigrations"] = pending.Count,
                };

                if (pending.Count > 0)
                {
                    return HealthCheckResult.Degraded(
                        $"{pending.Count} pending migration(s): {string.Join(", ", pending)}. Run the migrations.", data: data);
                }

                if (stopwatch.Elapsed > SlowQueryThreshold)
                {
                    return HealthCheckResult.Degraded(
                        $"Database is slow: {stopwatch.Elapsed.TotalMilliseconds:F0} ms for SELECT 1 " +
                        $"(threshold {SlowQueryThreshold.TotalMilliseconds:F0} ms).", data: data);
                }

                return HealthCheckResult.Healthy(
                    $"PostgreSQL {connection.ServerVersion} responded in {stopwatch.Elapsed.TotalMilliseconds:F0} ms.", data);
            }
            finally
            {
                await db.Database.CloseConnectionAsync();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("Cannot reach the database.", ex);
        }
    }
}
