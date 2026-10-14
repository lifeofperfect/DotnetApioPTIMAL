using System.Text.Json.Serialization;

namespace OptimalApi.Api.Contracts;

/// <summary>
/// The single shape every API response uses, success or failure:
/// <code>{ "success": bool, "message": string, "data": T | null, "errors": { field: [messages] } | null, "traceId"?: string }</code>
/// </summary>
/// <param name="Success">True for 2xx responses, false otherwise.</param>
/// <param name="Message">Human-readable summary of what happened.</param>
/// <param name="Data">The payload on success; null on failure.</param>
/// <param name="Errors">Field-level validation errors (field name to messages); null when there are none.</param>
/// <param name="TraceId">Present on failures only. Matches the server logs, so quote it when reporting a problem.</param>
public sealed record ApiResponse<T>(
    bool Success,
    string Message,
    T? Data,
    IReadOnlyDictionary<string, string[]>? Errors = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? TraceId = null);

/// <summary>Factory methods, so call sites read as <c>ApiResponse.Ok(product, "...")</c>.</summary>
public static class ApiResponse
{
    public static ApiResponse<T> Ok<T>(T data, string message) => new(true, message, data);

    /// <summary>Success with nothing to return (for example after a delete).</summary>
    public static ApiResponse<object> Ok(string message) => new(true, message, null);

    public static ApiResponse<object> Fail(
        string message, string traceId, IReadOnlyDictionary<string, string[]>? errors = null) =>
        new(false, message, null, errors, traceId);
}
