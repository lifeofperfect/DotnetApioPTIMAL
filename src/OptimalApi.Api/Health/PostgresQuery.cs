using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using OptimalApi.Api.Infrastructure;

namespace OptimalApi.Api.Health;

/// <summary>Runs read-only diagnostic SQL against PostgreSQL for the health checks.</summary>
internal static class PostgresQuery
{
    /// <summary>Opens the context's connection, runs <paramref name="work"/>, and always closes it again.</summary>
    public static async Task<T> WithConnectionAsync<T>(
        this AppDbContext db, Func<DbConnection, Task<T>> work, CancellationToken cancellationToken)
    {
        await db.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            return await work(db.Database.GetDbConnection());
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    /// <summary>Runs a query and returns the first row's columns by name. Only ever called with constant SQL.</summary>
    public static async Task<IReadOnlyDictionary<string, object?>> QueryRowAsync(
        this DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var row = new Dictionary<string, object?>();
        if (await reader.ReadAsync(cancellationToken))
        {
            for (var i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
        }

        return row;
    }

    public static long Long(this IReadOnlyDictionary<string, object?> row, string column) =>
        row.TryGetValue(column, out var value) && value is not null ? Convert.ToInt64(value, System.Globalization.CultureInfo.InvariantCulture) : 0;

    public static double Double(this IReadOnlyDictionary<string, object?> row, string column) =>
        row.TryGetValue(column, out var value) && value is not null ? Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture) : 0;
}
