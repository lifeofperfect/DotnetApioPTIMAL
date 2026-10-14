using Microsoft.AspNetCore.Diagnostics;
using OptimalApi.Api.Http;

namespace OptimalApi.Api.Middleware;

/// <summary>
/// Last line of defence: turns any unhandled exception into a 500 in the standard
/// <see cref="Contracts.ApiResponse{T}"/> shape. The full exception is logged; the client only
/// gets a generic message and a traceId to quote in bug reports.
/// </summary>
public sealed partial class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        // The client went away; there is nobody to send a response to.
        if (exception is OperationCanceledException && httpContext.RequestAborted.IsCancellationRequested)
        {
            return true;
        }

        LogUnhandledException(
            logger, exception, httpContext.Request.Method, httpContext.Request.Path, ApiErrors.TraceId(httpContext));

        await ApiErrors.WriteAsync(httpContext, StatusCodes.Status500InternalServerError);
        return true;
    }

    // Source-generated logging: no allocations when the level is disabled.
    // TraceId is logged so a client's traceId can be searched for in the logs.
    [LoggerMessage(Level = LogLevel.Error, Message = "Unhandled exception for {Method} {Path} (traceId {TraceId})")]
    private static partial void LogUnhandledException(
        ILogger logger, Exception exception, string method, string path, string traceId);
}
