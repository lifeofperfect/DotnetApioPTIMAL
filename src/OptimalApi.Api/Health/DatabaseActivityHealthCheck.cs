using Microsoft.Extensions.Diagnostics.HealthChecks;
using OptimalApi.Api.Infrastructure;

namespace OptimalApi.Api.Health;

/// <summary>
/// Finds queries that have been running too long and sessions stuck waiting on a lock.
/// Both are a common hidden cause of "the API is slow": one stuck statement can block every request behind it.
/// Degraded if any query runs longer than <see cref="LongRunningThreshold"/> or any session is blocked by a lock.
/// </summary>
public sealed class DatabaseActivityHealthCheck(AppDbContext db) : IHealthCheck
{
    public static readonly TimeSpan LongRunningThreshold = TimeSpan.FromSeconds(30);

    // "active" = executing right now (including waiting on a lock). Idle-in-transaction sessions are counted
    // separately as stale transactions, so one session is never reported twice.
    private const string Sql = """
        SELECT
            count(*) FILTER (WHERE state = 'active' AND now() - query_start > make_interval(secs => @threshold)) AS long_running,
            coalesce(max(extract(epoch FROM now() - query_start)) FILTER (WHERE state = 'active'), 0)         AS longest_seconds,
            count(*) FILTER (WHERE cardinality(pg_blocking_pids(pid)) > 0)                                     AS blocked,
            count(*) FILTER (WHERE state LIKE 'idle in transaction%'
                             AND now() - state_change > make_interval(secs => @threshold))                     AS stale_transactions
        FROM pg_stat_activity
        WHERE backend_type = 'client backend'
          AND pid <> pg_backend_pid()
          AND datname = current_database()
        """;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var row = await db.WithConnectionAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText = Sql;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "threshold";
            parameter.Value = LongRunningThreshold.TotalSeconds;
            command.Parameters.Add(parameter);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            await reader.ReadAsync(cancellationToken);
            return (
                LongRunning: reader.GetInt64(0),
                LongestSeconds: Math.Round(Convert.ToDouble(reader.GetValue(1), System.Globalization.CultureInfo.InvariantCulture), 1),
                Blocked: reader.GetInt64(2),
                StaleTransactions: reader.GetInt64(3));
        }, cancellationToken);

        var data = new Dictionary<string, object>
        {
            ["longRunningQueries"] = row.LongRunning,
            ["longestQuerySeconds"] = row.LongestSeconds,
            ["blockedByLocks"] = row.Blocked,
            ["staleTransactions"] = row.StaleTransactions,
            ["thresholdSeconds"] = LongRunningThreshold.TotalSeconds,
        };

        var problems = new List<string>();
        if (row.LongRunning > 0)
        {
            problems.Add($"{row.LongRunning} query(ies) running longer than {LongRunningThreshold.TotalSeconds:F0}s (longest {row.LongestSeconds:F0}s)");
        }

        if (row.Blocked > 0)
        {
            problems.Add($"{row.Blocked} session(s) waiting on a lock");
        }

        if (row.StaleTransactions > 0)
        {
            problems.Add($"{row.StaleTransactions} transaction(s) left open and idle");
        }

        return problems.Count > 0
            ? HealthCheckResult.Degraded(string.Join("; ", problems) + ".", data: data)
            : HealthCheckResult.Healthy("No long-running queries or lock waits.", data);
    }
}
