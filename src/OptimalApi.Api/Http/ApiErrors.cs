using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.WebUtilities;
using OptimalApi.Api.Contracts;

namespace OptimalApi.Api.Http;

/// <summary>
/// Builds every error response in one place, so a 400 from validation, a 404 from an unknown URL
/// and a 500 from a crash all look exactly the same to clients.
/// </summary>
public static class ApiErrors
{
    public const string ValidationFailedMessage = "Validation failed.";

    /// <summary>The id that ties a response to its log entries (W3C trace id when tracing is on).</summary>
    public static string TraceId(HttpContext httpContext)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        return Activity.Current?.Id ?? httpContext.TraceIdentifier;
    }

    public static ApiResponse<object> Create(
        HttpContext httpContext, string message, IReadOnlyDictionary<string, string[]>? errors = null) =>
        ApiResponse.Fail(message, TraceId(httpContext), errors);

    public static ApiResponse<object> Validation(HttpContext httpContext, ModelStateDictionary modelState) =>
        Create(httpContext, ValidationFailedMessage, ToErrors(modelState));

    /// <summary>Writes an error envelope directly to the response (for middleware, where there is no controller).</summary>
    public static Task WriteAsync(HttpContext httpContext, int statusCode, string? message = null)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        httpContext.Response.StatusCode = statusCode;
        return httpContext.Response.WriteAsJsonAsync(
            Create(httpContext, message ?? DefaultMessage(statusCode)),
            httpContext.RequestAborted);
    }

    /// <summary>Friendly text for errors that have no specific message of their own.</summary>
    public static string DefaultMessage(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "The request was invalid.",
        StatusCodes.Status404NotFound => "The requested resource was not found.",
        StatusCodes.Status405MethodNotAllowed => "This HTTP method is not allowed on this endpoint.",
        StatusCodes.Status415UnsupportedMediaType => "Unsupported content type. Send JSON with 'Content-Type: application/json'.",
        StatusCodes.Status500InternalServerError => "An unexpected error occurred.",
        _ => ReasonPhrases.GetReasonPhrase(statusCode) is { Length: > 0 } phrase ? phrase : "The request failed.",
    };

    /// <summary>ModelState to <c>{ "fieldName": ["message", ...] }</c> with camelCase keys, matching the JSON clients send.</summary>
    public static IReadOnlyDictionary<string, string[]> ToErrors(ModelStateDictionary modelState)
    {
        ArgumentNullException.ThrowIfNull(modelState);

        return modelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .ToDictionary(
                entry => JsonNamingPolicy.CamelCase.ConvertName(entry.Key),
                entry => entry.Value!.Errors
                    .Select(error => string.IsNullOrEmpty(error.ErrorMessage) ? "The value is invalid." : error.ErrorMessage)
                    .ToArray());
    }
}
