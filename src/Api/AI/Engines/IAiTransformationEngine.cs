namespace Metrics.Api.AI.Engines;

/// <summary>
/// Interface for DSL transformation engines.
/// Implementations handle different strategies for generating DSL from user goals.
/// </summary>
public interface IAiTransformationEngine
{
    /// <summary>
    /// The engine type identifier (e.g., "legacy", "plan_v1")
    /// </summary>
    string EngineType { get; }

    /// <summary>
    /// Generate DSL based on the request.
    /// </summary>
    /// <param name="request">The DSL generation request</param>
    /// <param name="correlationId">Request correlation ID for logging</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The generation result</returns>
    Task<AiEngineResult> GenerateAsync(
        DslGenerateRequest request, 
        string correlationId,
        CancellationToken ct);
}

/// <summary>
/// Result from an AI transformation engine
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
    public static AiEngineResult Ok(DslGenerateResult result, string engineUsed) =>
        new()
        {
            Success = true,
            Result = result with { EngineUsed = engineUsed },
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
