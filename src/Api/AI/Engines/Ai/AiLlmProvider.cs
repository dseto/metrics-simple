using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace Metrics.Api.AI.Engines.Ai;

/// <summary>
/// LLM provider for AI engine.
/// Generates TransformPlan JSON using structured outputs.
/// </summary>
public class AiLlmProvider
{
    private readonly HttpClient _httpClient;
    private readonly AiConfiguration _config;
    private readonly ILogger<AiLlmProvider> _logger;
    private readonly string _apiKey;
    private readonly PlanSchemaValidator _schemaValidator;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };

    /// <summary>
    /// Simplified JSON Schema for structured outputs (OpenAI compatible).
    /// Note: Full schema with $defs is not supported by all providers, so we use a simplified version.
    /// Backend validation uses the full schema.
    /// </summary>
    private static readonly object PlanJsonSchema = new
    {
        type = "object",
        properties = new
        {
            planVersion = new { type = "string", @enum = new[] { "1.0" } },
            source = new
            {
                type = "object",
                properties = new
                {
                    recordPath = new { type = "string" }
                },
                required = new[] { "recordPath" },
                additionalProperties = false
            },
            steps = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        op = new { type = "string" },
                        fields = new { type = "array" },
                        where = new { type = "object" },
                        compute = new { type = "array" },
                        map = new { type = "array" },
                        keys = new { type = "array" },
                        metrics = new { type = "array" },
                        by = new { type = "string" },
                        dir = new { type = "string" },
                        n = new { type = "integer" }
                    },
                    required = new[] { "op" },
                    additionalProperties = false
                }
            }
        },
        required = new[] { "planVersion", "source", "steps" },
        additionalProperties = false
    };

    public AiLlmProvider(
        HttpClient httpClient,
        AiConfiguration config,
        ILogger<AiLlmProvider> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;
        _schemaValidator = new PlanSchemaValidator();

        _apiKey = Environment.GetEnvironmentVariable("METRICS_OPENROUTER_API_KEY")
                  ?? Environment.GetEnvironmentVariable("OPENROUTER_API_KEY")
                  ?? throw new InvalidOperationException("API key not configured");

        _httpClient.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);
    }

    /// <summary>
    /// Result of LLM plan generation
    /// </summary>
    public record LlmPlanResult(
        bool Success,
        TransformPlan? Plan,
        string? RawJson,
        string? Error,
        PlanErrorCategory ErrorCategory,
        long LatencyMs
    );

    /// <summary>
    /// Error categories for plan generation
    /// </summary>
    public enum PlanErrorCategory
    {
        None,
        LlmTimeout,
        LlmUnavailable,
        LlmRateLimited,
        ResponseNotJson,
        PlanSchemaInvalid,
        RecordPathNotFound,
        PathInvalid,
        WrongShape,
        UnexpectedError
    }

    /// <summary>
    /// Generates a transformation plan using LLM.
    /// </summary>
    public async Task<LlmPlanResult> GeneratePlanAsync(
        string goalText,
        JsonElement sampleInput,
        IReadOnlyList<string>? candidateRecordPaths,
        string? structureAnalysis,
        CancellationToken ct)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var requestId = Guid.NewGuid().ToString("N")[..12];

        try
        {
            _logger.LogInformation(
                "PlanV1 LLM request: RequestId={RequestId}, Model={Model}, GoalLength={GoalLength}",
                requestId, _config.Model, goalText.Length);

            var systemPrompt = AiSystemPrompt.Build(50); // Default max columns
            var sampleInputJson = JsonSerializer.Serialize(sampleInput, new JsonSerializerOptions { WriteIndented = true });
            var userPrompt = AiSystemPrompt.BuildUserPrompt(goalText, sampleInputJson, structureAnalysis, candidateRecordPaths);

            var chatRequest = BuildChatRequest(systemPrompt, userPrompt);
            var json = JsonSerializer.Serialize(chatRequest, JsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _config.EndpointUrl);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            httpRequest.Content = content;

            HttpResponseMessage response;
            try
            {
                response = await _httpClient.SendAsync(httpRequest, ct);
            }
            catch (TaskCanceledException) when (!ct.IsCancellationRequested)
            {
                stopwatch.Stop();
                _logger.LogWarning("PlanV1 LLM timeout: RequestId={RequestId}, LatencyMs={Latency}", requestId, stopwatch.ElapsedMilliseconds);
                return new LlmPlanResult(false, null, null, "LLM request timed out", PlanErrorCategory.LlmTimeout, stopwatch.ElapsedMilliseconds);
            }
            catch (HttpRequestException ex)
            {
                stopwatch.Stop();
                _logger.LogWarning(ex, "PlanV1 LLM unavailable: RequestId={RequestId}", requestId);
                return new LlmPlanResult(false, null, null, $"LLM unavailable: {ex.Message}", PlanErrorCategory.LlmUnavailable, stopwatch.ElapsedMilliseconds);
            }

            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                stopwatch.Stop();
                return new LlmPlanResult(false, null, null, "Rate limited", PlanErrorCategory.LlmRateLimited, stopwatch.ElapsedMilliseconds);
            }

            if (!response.IsSuccessStatusCode)
            {
                stopwatch.Stop();
                var errorBody = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("PlanV1 LLM HTTP error: Status={Status}, Body={Body}", response.StatusCode, errorBody);
                return new LlmPlanResult(false, null, null, $"HTTP {response.StatusCode}", PlanErrorCategory.LlmUnavailable, stopwatch.ElapsedMilliseconds);
            }

            var responseBody = await response.Content.ReadAsStringAsync(ct);
            return ParseLlmResponse(responseBody, requestId, stopwatch);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "PlanV1 LLM unexpected error: RequestId={RequestId}", requestId);
            return new LlmPlanResult(false, null, null, ex.Message, PlanErrorCategory.UnexpectedError, stopwatch.ElapsedMilliseconds);
        }
    }

    private LlmPlanResult ParseLlmResponse(string responseBody, string requestId, System.Diagnostics.Stopwatch stopwatch)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            if (!root.TryGetProperty("choices", out var choices) || choices.GetArrayLength() == 0)
            {
                stopwatch.Stop();
                return new LlmPlanResult(false, null, null, "Missing choices in response", PlanErrorCategory.ResponseNotJson, stopwatch.ElapsedMilliseconds);
            }

            var firstChoice = choices[0];
            if (!firstChoice.TryGetProperty("message", out var message) ||
                !message.TryGetProperty("content", out var contentElement))
            {
                stopwatch.Stop();
                return new LlmPlanResult(false, null, null, "Missing message.content", PlanErrorCategory.ResponseNotJson, stopwatch.ElapsedMilliseconds);
            }

            var contentString = contentElement.GetString();
            if (string.IsNullOrWhiteSpace(contentString))
            {
                stopwatch.Stop();
                return new LlmPlanResult(false, null, null, "Empty content", PlanErrorCategory.ResponseNotJson, stopwatch.ElapsedMilliseconds);
            }

            // Robust parse: remove markdown, extract JSON
            var (parseSuccess, parsedContent, errorCategory, errorDetails) = LlmResponseParser.TryParseJsonResponse(contentString);
            if (!parseSuccess)
            {
                stopwatch.Stop();
                _logger.LogWarning("PlanV1 LLM response not JSON: RequestId={RequestId}, Error={Error}", requestId, errorDetails);
                return new LlmPlanResult(false, null, contentString, $"Not valid JSON: {errorDetails}", PlanErrorCategory.ResponseNotJson, stopwatch.ElapsedMilliseconds);
            }

            var planJson = parsedContent!.Value.GetRawText();

            // Validate against plan schema
            var validation = _schemaValidator.ValidateJson(planJson);
            if (!validation.IsValid)
            {
                stopwatch.Stop();
                var errorMsg = string.Join("; ", validation.Errors);
                _logger.LogWarning("PlanV1 LLM plan schema invalid: RequestId={RequestId}, Errors={Errors}", requestId, errorMsg);
                return new LlmPlanResult(false, null, planJson, $"Schema validation failed: {errorMsg}", PlanErrorCategory.PlanSchemaInvalid, stopwatch.ElapsedMilliseconds);
            }

            // Deserialize to TransformPlan
            var plan = JsonSerializer.Deserialize<TransformPlan>(planJson, JsonOptions);
            if (plan == null)
            {
                stopwatch.Stop();
                return new LlmPlanResult(false, null, planJson, "Failed to deserialize plan", PlanErrorCategory.PlanSchemaInvalid, stopwatch.ElapsedMilliseconds);
            }

            stopwatch.Stop();
            _logger.LogInformation(
                "PlanV1 LLM success: RequestId={RequestId}, LatencyMs={Latency}, Steps={StepCount}",
                requestId, stopwatch.ElapsedMilliseconds, plan.Steps.Count);

            return new LlmPlanResult(true, plan, planJson, null, PlanErrorCategory.None, stopwatch.ElapsedMilliseconds);
        }
        catch (JsonException ex)
        {
            stopwatch.Stop();
            _logger.LogWarning(ex, "PlanV1 LLM JSON parse error: RequestId={RequestId}", requestId);
            return new LlmPlanResult(false, null, null, ex.Message, PlanErrorCategory.ResponseNotJson, stopwatch.ElapsedMilliseconds);
        }
    }

    private object BuildChatRequest(string systemPrompt, string userPrompt)
    {
        var messages = new[]
        {
            new { role = "system", content = systemPrompt },
            new { role = "user", content = userPrompt }
        };

        var requestObj = new JsonObject
        {
            ["model"] = _config.Model,
            ["messages"] = JsonSerializer.SerializeToNode(messages),
            ["max_tokens"] = _config.MaxTokens,
            ["temperature"] = 0.2, // Lower temperature for more deterministic output
            ["top_p"] = _config.TopP,
            ["stream"] = false
        };

        // Add structured outputs if enabled
        if (_config.EnableStructuredOutputs)
        {
            requestObj["response_format"] = new JsonObject
            {
                ["type"] = "json_schema",
                ["json_schema"] = new JsonObject
                {
                    ["name"] = "transform_plan_v1",
                    ["strict"] = true,
                    ["schema"] = JsonSerializer.SerializeToNode(PlanJsonSchema)
                }
            };
        }

        return requestObj;
    }
}
