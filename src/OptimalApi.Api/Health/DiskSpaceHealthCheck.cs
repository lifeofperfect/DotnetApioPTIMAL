using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OptimalApi.Api.Health;

/// <summary>
/// Free space on the disk the app runs from. A full disk breaks logging, temp files and uploads in confusing ways.
/// Uses absolute amounts as well as percentages, so a large disk with plenty of GB free doesn't raise false alarms:
/// Degraded below <see cref="DegradedFreeGb"/> GB or <see cref="DegradedFreePercent"/>% free,
/// Unhealthy below <see cref="UnhealthyFreeGb"/> GB.
/// </summary>
public sealed class DiskSpaceHealthCheck : IHealthCheck
{
    public const double DegradedFreeGb = 5;
    public const double DegradedFreePercent = 5;
    public const double UnhealthyFreeGb = 1;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var drive = new DriveInfo(Path.GetPathRoot(AppContext.BaseDirectory)!);

        var totalGb = ToGigabytes(drive.TotalSize);
        var freeGb = ToGigabytes(drive.AvailableFreeSpace);
        var freePercent = drive.TotalSize == 0 ? 0 : Math.Round(drive.AvailableFreeSpace * 100d / drive.TotalSize, 1);

        var data = new Dictionary<string, object>
        {
            ["path"] = drive.RootDirectory.FullName,
            ["freeGb"] = freeGb,
            ["totalGb"] = totalGb,
            ["freePercent"] = freePercent,
            ["degradedBelowGb"] = DegradedFreeGb,
            ["unhealthyBelowGb"] = UnhealthyFreeGb,
        };

        var summary = $"{freeGb} GB free of {totalGb} GB ({freePercent}%).";

        HealthCheckResult result;
        if (freeGb < UnhealthyFreeGb)
        {
            result = HealthCheckResult.Unhealthy($"Disk almost full: {summary}", data: data);
        }
        else if (freeGb < DegradedFreeGb || freePercent < DegradedFreePercent)
        {
            result = HealthCheckResult.Degraded($"Disk space low: {summary}", data: data);
        }
        else
        {
            result = HealthCheckResult.Healthy(summary, data);
        }

        return Task.FromResult(result);
    }

    private static double ToGigabytes(long bytes) => Math.Round(bytes / 1024d / 1024d / 1024d, 1);
}
