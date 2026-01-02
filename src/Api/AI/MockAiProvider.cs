using System.Text.Json;

namespace Metrics.Api.AI;

/// <summary>
/// Mock AI provider for testing purposes.
/// Can be configured to return predetermined responses or simulate errors.
/// </summary>
public class MockAiProvider : IAiProvider
{
    public string ProviderName => "MockProvider";

    private readonly MockProviderConfig _config;

    public MockAiProvider(MockProviderConfig? config = null)
    {
        _config = config ?? MockProviderConfig.Default;
    }

    public Task<DslGenerateResult> GenerateDslAsync(DslGenerateRequest request, CancellationToken ct)
    {
        // Check if we should simulate an error
        if (_config.SimulateError != null)
        {
            throw new AiProviderException(_config.SimulateError.Code, _config.SimulateError.Message);
        }

        // Check if we should return invalid output
        if (_config.ReturnInvalidOutput)
        {
            throw new AiProviderException(AiErrorCodes.AiOutputInvalid,
                "Mock provider configured to return invalid output");
        }

        // Check for simulated timeout
        if (_config.SimulateTimeoutMs > 0)
        {
            ct.ThrowIfCancellationRequested();
            Thread.Sleep(_config.SimulateTimeoutMs);
            if (ct.IsCancellationRequested)
            {
                throw new TaskCanceledException();
            }
        }

        // Return a valid mock response
        var result = _config.CustomResult ?? CreateDefaultResult(request);
        return Task.FromResult(result);
    }

    private static DslGenerateResult CreateDefaultResult(DslGenerateRequest request)
    {
        // Create a simple JSONata expression that works with any input
        // This produces an array with a single dummy object
        var dslText = request.DslProfile == "jsonata"
            ? "[ { \"id\": \"mock-1\", \"name\": \"mock-item\", \"value\": 100 } ]"
            : "[*].{id: id, name: name, value: value}";

        // Create a simple output schema
        var outputSchemaJson = """
            {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "id": { "type": "string" },
                        "name": { "type": "string" },
                        "value": { "type": "number" }
                    },
                    "required": ["id", "name", "value"],
                    "additionalProperties": true
                }
            }
            """;

        using var schemaDoc = JsonDocument.Parse(outputSchemaJson);

        return new DslGenerateResult
        {
            Dsl = new DslOutput
            {
                Profile = request.DslProfile,
                Text = dslText
            },
            OutputSchema = schemaDoc.RootElement.Clone(),
            Rationale = "Mock response generated for testing purposes.",
            Warnings = new List<string>(),
            ModelInfo = new ModelInfo
            {
                Provider = "MockProvider",
                Model = "mock-model-v1",
                PromptVersion = "1.0.0-mock"
            }
        };
    }
}

/// <summary>
/// Configuration for MockAiProvider behavior
/// </summary>
public class MockProviderConfig
{
    /// <summary>
    /// Default configuration returning valid responses
    /// </summary>
    public static MockProviderConfig Default => new();

    /// <summary>
    /// If set, provider will throw an exception with this error
    /// </summary>
    public MockError? SimulateError { get; init; }

    /// <summary>
    /// If true, provider will throw AI_OUTPUT_INVALID error
    /// </summary>
    public bool ReturnInvalidOutput { get; init; }

    /// <summary>
    /// If > 0, provider will sleep for this duration before responding
    /// </summary>
    public int SimulateTimeoutMs { get; init; }

    /// <summary>
    /// If set, provider will return this exact result
    /// </summary>
    public DslGenerateResult? CustomResult { get; init; }

    /// <summary>
    /// Creates a config that returns a specific result
    /// </summary>
    public static MockProviderConfig WithResult(DslGenerateResult result) =>
        new() { CustomResult = result };

    /// <summary>
    /// Creates a config that simulates an error
    /// </summary>
    public static MockProviderConfig WithError(string code, string message) =>
        new() { SimulateError = new MockError(code, message) };

    /// <summary>
    /// Creates a config that returns invalid output
    /// </summary>
    public static MockProviderConfig WithInvalidOutput() =>
        new() { ReturnInvalidOutput = true };

    /// <summary>
    /// Creates a config that simulates a timeout
    /// </summary>
    public static MockProviderConfig WithTimeout(int timeoutMs) =>
        new() { SimulateTimeoutMs = timeoutMs };
}

/// <summary>
/// Error configuration for mock provider
/// </summary>
public record MockError(string Code, string Message);
