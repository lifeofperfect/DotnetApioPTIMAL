using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OptimalApi.Api.Health;

/// <summary>
/// How much time the app spends paused for garbage collection. Rising GC time is an early sign of a memory
/// problem (too many allocations, a leak) before memory actually runs out.
/// Degraded when GC pauses take more than <see cref="DegradedPausePercent"/>% of the time.
/// </summary>
public sealed class GarbageCollectionHealthCheck : IHealthCheck
{
    public const double DegradedPausePercent = 10;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var info = GC.GetGCMemoryInfo();
        var pausePercent = Math.Round(info.PauseTimePercentage, 2);

        var data = new Dictionary<string, object>
        {
            ["pauseTimePercent"] = pausePercent,
            ["totalPauseMs"] = Math.Round(GC.GetTotalPauseDuration().TotalMilliseconds, 1),
            ["gen0Collections"] = GC.CollectionCount(0),
            ["gen1Collections"] = GC.CollectionCount(1),
            ["gen2Collections"] = GC.CollectionCount(2),
            ["heapSizeMb"] = ToMegabytes(info.HeapSizeBytes),
            ["fragmentedMb"] = ToMegabytes(info.FragmentedBytes),
            ["allocatedSinceStartMb"] = ToMegabytes(GC.GetTotalAllocatedBytes()),
            ["serverGc"] = System.Runtime.GCSettings.IsServerGC,
        };

        var result = pausePercent > DegradedPausePercent
            ? HealthCheckResult.Degraded(
                $"GC pauses take {pausePercent}% of the time (threshold {DegradedPausePercent}%): check for heavy allocations.", data: data)
            : HealthCheckResult.Healthy($"GC pauses take {pausePercent}% of the time.", data);

        return Task.FromResult(result);
    }

    private static double ToMegabytes(long bytes) => Math.Round(bytes / 1024d / 1024d, 1);
}
