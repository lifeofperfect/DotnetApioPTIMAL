namespace OptimalApi.Api.Domain;

/// <summary>
/// A product in the catalogue. State changes only through constructors and methods,
/// so the entity can never be persisted in an invalid state.
/// </summary>
public sealed class Product
{
    public const int NameMaxLength = 200;

    // Required by EF Core to materialise entities from the database.
    private Product()
    {
    }

    public Product(string name, decimal price)
    {
        Id = Guid.CreateVersion7(); // time-ordered: index-friendly, unlike random v4 GUIDs
        CreatedAt = UtcNowForDatabase();
        SetDetails(name, price);
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public decimal Price { get; private set; }

    public DateTime CreatedAt { get; private set; }

    public DateTime? UpdatedAt { get; private set; }

    /// <summary>Optimistic concurrency token, mapped to PostgreSQL's xmin system column.</summary>
    public uint Version { get; private set; }

    public void Update(string name, decimal price)
    {
        SetDetails(name, price);
        UpdatedAt = UtcNowForDatabase();
    }

    private void SetDetails(string name, decimal price)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(name.Trim().Length, NameMaxLength, nameof(name));
        ArgumentOutOfRangeException.ThrowIfNegative(price);

        Name = name.Trim();
        Price = price;
    }

    // PostgreSQL stores timestamps to the microsecond; .NET has 10x finer ticks.
    // Rounding here means a value reads back exactly as it was returned from create/update.
    private static DateTime UtcNowForDatabase()
    {
        var now = DateTime.UtcNow;
        return now.AddTicks(-(now.Ticks % (TimeSpan.TicksPerMillisecond / 1000)));
    }
}
