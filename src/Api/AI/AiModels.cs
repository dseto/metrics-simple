using System.Text.Json;
using System.Text.Json.Serialization;

namespace Metrics.Api.AI;

/// <summary>
/// Configuration for AI provider - loaded from appsettings.json
/// </summary>
public record AiConfiguration
{
    public bool Enabled { get; init; }
    /// <summary>
    /// AI Provider: "HttpOpenAICompatible" (OpenRouter/OpenAI), "Gemini" (Google), or "MockProvider"
    /// </summary>
    public string Provider { get; init; } = "HttpOpenAICompatible";
    /// <summary>
    /// Endpoint URL.
    /// For OpenRouter: https://openrouter.ai/api/v1/chat/completions
    /// For Gemini: https://generativelanguage.googleapis.com/v1beta/models (without model name or key)
    /// </summary>
    public string EndpointUrl { get; init; } = "https://openrouter.ai/api/v1/chat/completions";
    public string? ApiKey { get; set; }
    /// <summary>
    /// Model name. 
    /// For OpenRouter: "openai/gpt-4-turbo", "nousresearch/hermes-3-llama-3.1-405b", etc.
    /// For Gemini: "gemini-2.5-flash", "gemini-1.5-pro", "gemini-1.5-flash", etc. (with or without "models/" prefix)
    /// </summary>
    public string Model { get; init; } = "openai/gpt-oss-120b";
    public string PromptVersion { get; init; } = "1.0.0";
    public int TimeoutSeconds { get; init; } = 30;
    public int MaxRetries { get; init; } = 1;
    public double Temperature { get; init; } = 0.0;
    public int MaxTokens { get; init; } = 4096;
    public double TopP { get; init; } = 0.9;
    
    /// <summary>
    /// Enable OpenRouter structured outputs (response_format with json_schema)
    /// Forces LLM to return valid JSON matching the schema.
    /// </summary>
    public bool EnableStructuredOutputs { get; init; } = true;
    
    /// <summary>
    /// Enable OpenRouter response-healing plugin.
    /// Attempts to repair malformed JSON responses automatically.
    /// </summary>
    public bool EnableResponseHealing { get; init; } = true;
    
    /// <summary>
    /// Default engine when request doesn't specify one: "legacy" | "plan_v1" | "auto"
    /// </summary>
    public string DefaultEngine { get; init; } = EngineType.Legacy;
}

/// <summary>
/// Engine selection mode for DSL generation
/// </summary>
public static class EngineType
{
    /// <summary>Use legacy LLM-based engine (Jsonata)</summary>
    public const string Legacy = "legacy";
    
    /// <summary>Use plan_v1 deterministic IR engine</summary>
    public const string PlanV1 = "plan_v1";
    
    /// <summary>Auto-select based on goal heuristics</summary>
    public const string Auto = "auto";
    
    public static bool IsValid(string? engine) =>
        string.IsNullOrEmpty(engine) ||
        engine == Legacy ||
        engine == PlanV1 ||
        engine == Auto;
}

/// <summary>
/// DSL generation request - matches dslGenerateRequest.schema.json
/// </summary>
public record DslGenerateRequest
{
    [JsonPropertyName("goalText")]
    public required string GoalText { get; init; }

    [JsonPropertyName("sampleInput")]
    public required JsonElement SampleInput { get; init; }

    [JsonPropertyName("dslProfile")]
    public string DslProfile { get; init; } = "jsonata";

    [JsonPropertyName("constraints")]
    public required DslConstraints Constraints { get; init; }

    [JsonPropertyName("hints")]
    public Dictionary<string, string>? Hints { get; init; }

    [JsonPropertyName("existingDsl")]
    public string? ExistingDsl { get; init; }

    [JsonPropertyName("existingOutputSchema")]
    public JsonElement? ExistingOutputSchema { get; init; }

    /// <summary>
    /// Engine selection: "legacy" (LLM/Jsonata), "plan_v1" (IR deterministic), "auto" (heuristic).
    /// Defaults to configuration if not specified.
    /// </summary>
    [JsonPropertyName("engine")]
    public string? Engine { get; init; }

    /// <summary>
    /// When true and engine=plan_v1, include the execution plan in the response.
    /// </summary>
    [JsonPropertyName("includePlan")]
    public bool IncludePlan { get; init; } = false;
}

/// <summary>
/// Constraints for DSL generation
/// </summary>
public record DslConstraints
{
    [JsonPropertyName("maxColumns")]
    public int MaxColumns { get; init; } = 50;

    [JsonPropertyName("allowTransforms")]
    public bool AllowTransforms { get; init; } = true;

    [JsonPropertyName("forbidNetworkCalls")]
    public bool ForbidNetworkCalls { get; init; } = true;

    [JsonPropertyName("forbidCodeExecution")]
    public bool ForbidCodeExecution { get; init; } = true;
}

/// <summary>
/// DSL generation result - matches dslGenerateResult.schema.json
/// </summary>
public record DslGenerateResult
{
    [JsonPropertyName("dsl")]
    public required DslOutput Dsl { get; init; }

    [JsonPropertyName("outputSchema")]
    public required JsonElement OutputSchema { get; init; }

    [JsonPropertyName("exampleRows")]
    public JsonElement? ExampleRows { get; init; }

    [JsonPropertyName("rationale")]
    public required string Rationale { get; init; }

    [JsonPropertyName("warnings")]
    public required List<string> Warnings { get; init; }

    [JsonPropertyName("modelInfo")]
    public ModelInfo? ModelInfo { get; init; }

    /// <summary>
    /// Execution plan (only populated when engine=plan_v1 and includePlan=true)
    /// </summary>
    [JsonPropertyName("plan")]
    public JsonElement? Plan { get; init; }

    /// <summary>
    /// Which engine was used for generation
    /// </summary>
    [JsonPropertyName("engineUsed")]
    public string? EngineUsed { get; init; }
}

/// <summary>
/// DSL output details
/// </summary>
public record DslOutput
{
    [JsonPropertyName("profile")]
    public required string Profile { get; init; }

    [JsonPropertyName("text")]
    public required string Text { get; init; }
}

/// <summary>
/// Model information (optional)
/// </summary>
public record ModelInfo
{
    [JsonPropertyName("provider")]
    public string? Provider { get; init; }

    [JsonPropertyName("model")]
    public string? Model { get; init; }

    [JsonPropertyName("promptVersion")]
    public string? PromptVersion { get; init; }
}

/// <summary>
/// AI error response - matches aiError.schema.json
/// </summary>
public record AiError
{
    [JsonPropertyName("code")]
    public required string Code { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }

    [JsonPropertyName("details")]
    public List<AiErrorDetail>? Details { get; init; }

    [JsonPropertyName("correlationId")]
    public required string CorrelationId { get; init; }

    [JsonPropertyName("executionId")]
    public string? ExecutionId { get; init; }
}

/// <summary>
/// AI error detail entry
/// </summary>
public record AiErrorDetail
{
    [JsonPropertyName("path")]
    public required string Path { get; init; }

    [JsonPropertyName("message")]
    public required string Message { get; init; }
}

/// <summary>
/// Known AI error codes (from aiError.schema.json)
/// </summary>
public static class AiErrorCodes
{
    public const string AiDisabled = "AI_DISABLED";
    public const string AiProviderUnavailable = "AI_PROVIDER_UNAVAILABLE";
    public const string AiTimeout = "AI_TIMEOUT";
    public const string AiOutputInvalid = "AI_OUTPUT_INVALID";
    public const string AiRateLimited = "AI_RATE_LIMITED";
}
