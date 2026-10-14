using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OptimalApi.Api.Health;

/// <summary>
/// Reports how much memory the process uses. Degraded (not Unhealthy) above the threshold:
/// high memory is a warning to investigate, not a reason to take the instance out of service.
/// </summary>
public sealed class MemoryHealthCheck : IHealthCheck
{
    public const long DegradedThresholdBytes = 1024L * 1024 * 1024; // 1 GB

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var workingSet = Environment.WorkingSet;
        var gcInfo = GC.GetGCMemoryInfo();

        var data = new Dictionary<string, object>
        {
            ["workingSetMb"] = ToMegabytes(workingSet),
            ["managedHeapMb"] = ToMegabytes(GC.GetTotalMemory(forceFullCollection: false)),
            ["gcCollections"] = GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2),
            ["memoryLimitMb"] = ToMegabytes(gcInfo.TotalAvailableMemoryBytes),
            ["thresholdMb"] = ToMegabytes(DegradedThresholdBytes),
        };

        var result = workingSet > DegradedThresholdBytes
            ? HealthCheckResult.Degraded($"Process is using {ToMegabytes(workingSet)} MB (threshold {ToMegabytes(DegradedThresholdBytes)} MB).", data: data)
            : HealthCheckResult.Healthy($"Process is using {ToMegabytes(workingSet)} MB.", data);

        return Task.FromResult(result);
    }

    private static double ToMegabytes(long bytes) => Math.Round(bytes / 1024d / 1024d, 1);
}
