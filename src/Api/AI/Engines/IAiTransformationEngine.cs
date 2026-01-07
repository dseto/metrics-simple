namespace Metrics.Api.AI.Engines;

/// <summary>
/// Result from AI engine
/// </summary>
public record AiEngineResult
{
    /// <summary>
    /// Whether the engine execution was successful
    /// </summary>
    public required bool Success { get; init; }

    /// <summary>
    /// The DSL generation result (when Success=true)
    /// </summary>
    public DslGenerateResult? Result { get; init; }

    /// <summary>
    /// Error information (when Success=false)
    /// </summary>
    public AiError? Error { get; init; }

    /// <summary>
    /// HTTP status code to return
    /// </summary>
    public int StatusCode { get; init; } = 200;

    /// <summary>
    /// Create a successful result
    /// </summary>
    public static AiEngineResult Ok(DslGenerateResult result, string _) =>
        new()
        {
            Success = true,
            Result = result,
            StatusCode = 200
        };

    /// <summary>
    /// Create an error result
    /// </summary>
    public static AiEngineResult Fail(AiError error, int statusCode = 502) =>
        new()
        {
            Success = false,
            Error = error,
            StatusCode = statusCode
        };
}
