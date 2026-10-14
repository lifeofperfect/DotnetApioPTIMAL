using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace OptimalApi.Api.Health;

/// <summary>
/// Detects thread pool starvation, the classic .NET failure where blocking calls (.Result, .Wait(), Thread.Sleep)
/// tie up threads, so work piles up in the queue and every request slows down although CPU looks idle.
/// Degraded when more than <see cref="DegradedQueueLength"/> work items are waiting for a thread.
/// </summary>
public sealed class ThreadPoolHealthCheck : IHealthCheck
{
    public const long DegradedQueueLength = 50;

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        ThreadPool.GetMinThreads(out var minWorkers, out _);
        ThreadPool.GetMaxThreads(out var maxWorkers, out _);
        ThreadPool.GetAvailableThreads(out var availableWorkers, out _);

        var queued = ThreadPool.PendingWorkItemCount;
        var data = new Dictionary<string, object>
        {
            ["threads"] = ThreadPool.ThreadCount,
            ["queuedWorkItems"] = queued,
            ["busyWorkerThreads"] = maxWorkers - availableWorkers,
            ["minWorkerThreads"] = minWorkers,
            ["completedWorkItems"] = ThreadPool.CompletedWorkItemCount,
            ["processorCount"] = Environment.ProcessorCount,
            ["thresholdQueued"] = DegradedQueueLength,
        };

        var result = queued > DegradedQueueLength
            ? HealthCheckResult.Degraded(
                $"{queued} work items waiting for a thread: possible thread pool starvation (look for blocking calls).", data: data)
            : HealthCheckResult.Healthy($"{ThreadPool.ThreadCount} threads, {queued} work item(s) queued.", data);

        return Task.FromResult(result);
    }
}
