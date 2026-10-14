using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OptimalApi.Api.Health;

/// <summary>
/// Not a pass/fail check: shows what is running (version, commit, environment, uptime, runtime).
/// The first thing you want to know during an incident is "which build is this, and when did it start?".
/// </summary>
public sealed class AppInfoHealthCheck(IHostEnvironment environment) : IHealthCheck
{
    private static readonly DateTime StartedAtUtc = Process.GetCurrentProcess().StartTime.ToUniversalTime();

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        // The SDK stamps "1.0.0+<git commit>" into the informational version when the build can see the git repository.
        var informationalVersion = typeof(AppInfoHealthCheck).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        var parts = informationalVersion.Split('+', 2);
        var commit = parts.Length > 1 ? parts[1][..Math.Min(7, parts[1].Length)] : "unknown";

        var uptime = DateTime.UtcNow - StartedAtUtc;

        var data = new Dictionary<string, object>
        {
            ["version"] = parts[0],
            ["commit"] = commit,
            ["environment"] = environment.EnvironmentName,
            ["startedAtUtc"] = StartedAtUtc.ToString("u", System.Globalization.CultureInfo.InvariantCulture),
            ["uptime"] = FormatUptime(uptime),
            ["runtime"] = RuntimeInformation.FrameworkDescription,
            ["os"] = RuntimeInformation.OSDescription,
            ["host"] = Environment.MachineName,
            ["processId"] = Environment.ProcessId,
        };

        return Task.FromResult(HealthCheckResult.Healthy(
            $"v{parts[0]} ({commit}) in {environment.EnvironmentName}, up {FormatUptime(uptime)}.", data));
    }

    private static string FormatUptime(TimeSpan uptime) =>
        uptime.TotalDays >= 1 ? $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m"
        : uptime.TotalHours >= 1 ? $"{uptime.Hours}h {uptime.Minutes}m"
        : $"{uptime.Minutes}m {uptime.Seconds}s";
}
