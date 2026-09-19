namespace AgrismartAPI.Application.Common;

/// <summary>
/// A Monime API call failed. <see cref="StatusCode"/> is null when Monime was never reached
/// (network/timeout failure) — callers map a 4xx status to this API's 400 (bad input) and anything
/// else, including null, to a 502 (the payment provider, not the caller, was the problem).
/// </summary>
public class MonimeApiException(string message, int? statusCode, string? requestJson = null, string? responseJson = null) : Exception(message)
{
    public int? StatusCode { get; } = statusCode;

    /// <summary>The exact JSON body sent to Monime — kept so callers can still write an audit-log row on failure.</summary>
    public string? RequestJson { get; } = requestJson;

    /// <summary>The raw response body Monime returned, if any was received.</summary>
    public string? ResponseJson { get; } = responseJson;
}
