using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OptimalApi.Api.Infrastructure;

/// <summary>
/// Used only by EF Core tooling (dotnet ef, migration bundles) instead of starting the whole app.
/// Without it, tooling would hit Program.cs's "connection string is missing" guard, e.g. when the
/// Docker build creates the migration bundle, where no database exists.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    // Never connected to: only used by commands that don't touch a database (migrations add, migrations bundle).
    private const string OfflinePlaceholder = "Host=localhost;Database=optimalapi";

    public AppDbContext CreateDbContext(string[] args)
    {
        // Same sources the app uses locally: user-secrets, then environment variables (Docker/CI).
        var configuration = new ConfigurationBuilder()
            .AddUserSecrets<AppDbContext>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var options = new DbContextOptionsBuilder<AppDbContext>();
        AppDbContext.Configure(options, configuration.GetConnectionString("Default") ?? OfflinePlaceholder);

        return new AppDbContext(options.Options);
    }
}
