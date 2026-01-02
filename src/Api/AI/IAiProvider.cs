namespace Metrics.Api.AI;

/// <summary>
/// Interface for AI providers that generate DSL and output schemas.
/// Matches the contract defined in specs/backend/08-ai-assist/ai-provider-contract.md
/// </summary>
public interface IAiProvider
{
    /// <summary>
    /// Generates DSL and output schema from natural language description and sample input.
    /// </summary>
    /// <param name="request">The DSL generation request</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Generated DSL and output schema</returns>
    /// <exception cref="AiProviderException">When provider fails or returns invalid output</exception>
    Task<DslGenerateResult> GenerateDslAsync(DslGenerateRequest request, CancellationToken ct);

    /// <summary>
    /// Gets the provider name for logging/diagnostics
    /// </summary>
    string ProviderName { get; }
}

/// <summary>
/// Exception thrown by AI providers when an error occurs
/// </summary>
public class AiProviderException : Exception
{
    public string ErrorCode { get; }
    public List<AiErrorDetail>? Details { get; }

    public AiProviderException(string errorCode, string message, List<AiErrorDetail>? details = null)
        : base(message)
    {
        ErrorCode = errorCode;
        Details = details;
    }

    public AiProviderException(string errorCode, string message, Exception innerException)
        : base(message, innerException)
    {
        ErrorCode = errorCode;
    }
}
