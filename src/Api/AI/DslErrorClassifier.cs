namespace Metrics.Api.AI;

/// <summary>
/// Error classification for DSL generation retry logic.
/// Categorizes errors to determine if retry makes sense.
/// </summary>
public static class DslErrorClassifier
{
    public enum ErrorCategory
    {
        /// <summary>LLM response is not valid JSON</summary>
        LlmResponseNotJson,
        
        /// <summary>JSON structure doesn't match DSL contract</summary>
        LlmContractInvalid,
        
        /// <summary>Jsonata syntax error (compilation)</summary>
        JsonataSyntaxInvalid,
        
        /// <summary>Jsonata evaluation error (runtime)</summary>
        JsonataEvalFailed,
        
        /// <summary>Output schema is invalid JSON</summary>
        OutputSchemaInvalid,
        
        /// <summary>Other error</summary>
        Unknown
    }

    /// <summary>
    /// Classify error from LLM provider exception or parsing.
    /// </summary>
    public static ErrorCategory Classify(string? errorMessage, string? errorCode)
    {
        if (errorMessage == null && errorCode == null)
            return ErrorCategory.Unknown;

        var combined = $"{errorCode} {errorMessage}".ToLowerInvariant();

        if (combined.Contains("json"))
            return ErrorCategory.LlmResponseNotJson;

        if (combined.Contains("contract") || combined.Contains("field") || combined.Contains("required"))
            return ErrorCategory.LlmContractInvalid;

        if (combined.Contains("jsonata") || combined.Contains("syntax") || combined.Contains("parse") || 
            combined.Contains("compile") || combined.Contains("failed to parse"))
            return ErrorCategory.JsonataSyntaxInvalid;

        if (combined.Contains("evaluation") || combined.Contains("eval failed") || combined.Contains("runtime"))
            return ErrorCategory.JsonataEvalFailed;

        if (combined.Contains("outputschema") || combined.Contains("schema"))
            return ErrorCategory.OutputSchemaInvalid;

        return ErrorCategory.Unknown;
    }

    /// <summary>
    /// Determine if retry is worthwhile for this error category.
    /// Some errors are not transient and won't be fixed by retry.
    /// </summary>
    public static bool IsRetryable(ErrorCategory category)
    {
        return category switch
        {
            ErrorCategory.LlmResponseNotJson => true,      // Network glitch or transient parse issue
            ErrorCategory.LlmContractInvalid => true,      // Retry with repair might help
            ErrorCategory.JsonataSyntaxInvalid => true,    // Try repair with syntax tips
            ErrorCategory.JsonataEvalFailed => true,       // Data issue, might be fixed with different DSL
            ErrorCategory.OutputSchemaInvalid => true,     // Backend should fix, but retry first
            ErrorCategory.Unknown => true,                  // Try once more
            _ => true,                                       // Default: try retry for unknown cases
        };
    }
}
