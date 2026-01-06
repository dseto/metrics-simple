using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace Metrics.Api.AI;

/// <summary>
/// HTTP provider for Google Gemini API
/// Generates DSL using Google's Generative AI models
/// 
/// Features:
/// - Direct integration with Google Generative Language API
/// - JSON response format validation
/// - Comprehensive error handling and retry logic
/// </summary>
public class GeminiProvider : IAiProvider
{
    private readonly HttpClient _httpClient;
    private readonly AiConfiguration _config;
    private readonly ILogger<GeminiProvider> _logger;
    private readonly string _apiKey;

    public string ProviderName => "Gemini";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        PropertyNameCaseInsensitive = true
    };

    public GeminiProvider(
        HttpClient httpClient,
        AiConfiguration config,
        ILogger<GeminiProvider> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;

        // Get API key from environment variables (priority: METRICS_GEMINI_API_KEY > GEMINI_API_KEY)
        _apiKey = Environment.GetEnvironmentVariable("METRICS_GEMINI_API_KEY")
                  ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY")
                  ?? throw new AiProviderException(
                      AiErrorCodes.AiProviderUnavailable,
                      "Gemini API key not configured. Set METRICS_GEMINI_API_KEY or GEMINI_API_KEY environment variable.");

        _httpClient.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);
    }

    public async Task<DslGenerateResult> GenerateDslAsync(DslGenerateRequest request, CancellationToken ct)
    {
        var retryCount = 0;
        var maxRetries = _config.MaxRetries;
        var requestId = Guid.NewGuid().ToString("N")[..12];

        while (true)
        {
            try
            {
                _logger.LogInformation(
                    "Gemini request: RequestId={RequestId}, Model={Model}, GoalLength={GoalLength}, Attempt={Attempt}",
                    requestId, _config.Model, request.GoalText.Length, retryCount + 1);

                var systemPrompt = BuildSystemPrompt(request);
                var userPrompt = BuildUserPrompt(request);

                var chatRequest = BuildChatRequest(systemPrompt, userPrompt);
                var json = JsonSerializer.Serialize(chatRequest, JsonOptions);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                // Construct Gemini API URL with API key
                var endpoint = BuildEndpoint();
                
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint);
                httpRequest.Content = content;

                HttpResponseMessage response;
                try
                {
                    response = await _httpClient.SendAsync(httpRequest, ct);
                }
                catch (TaskCanceledException) when (!ct.IsCancellationRequested)
                {
                    _logger.LogWarning("Gemini request timeout: RequestId={RequestId}", requestId);
                    
                    if (retryCount < maxRetries)
                    {
                        retryCount++;
                        await Task.Delay(TimeSpan.FromMilliseconds(100 * retryCount), ct);
                        continue;
                    }

                    throw new AiProviderException(
                        AiErrorCodes.AiTimeout,
                        $"Gemini request timed out after {_config.TimeoutSeconds} seconds");
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning(ex, "Gemini HTTP error: RequestId={RequestId}", requestId);
                    throw new AiProviderException(
                        AiErrorCodes.AiProviderUnavailable,
                        $"Gemini API unavailable: {ex.Message}");
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    _logger.LogWarning("Gemini rate limited: RequestId={RequestId}", requestId);

                    if (retryCount < maxRetries)
                    {
                        retryCount++;
                        await Task.Delay(TimeSpan.FromSeconds(1 + retryCount), ct);
                        continue;
                    }

                    throw new AiProviderException(
                        AiErrorCodes.AiRateLimited,
                        "Gemini API rate limit exceeded");
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogWarning(
                        "Gemini HTTP error: RequestId={RequestId}, Status={Status}, Body={Body}",
                        requestId, response.StatusCode, errorBody);

                    throw new AiProviderException(
                        AiErrorCodes.AiProviderUnavailable,
                        $"Gemini API returned HTTP {response.StatusCode}: {errorBody}");
                }

                var responseBody = await response.Content.ReadAsStringAsync(ct);
                return ParseGeminiResponse(responseBody, requestId);
            }
            catch (AiProviderException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gemini unexpected error: RequestId={RequestId}", requestId);
                throw new AiProviderException(
                    AiErrorCodes.AiOutputInvalid,
                    $"Gemini provider error: {ex.Message}");
            }
        }
    }

    private string BuildEndpoint()
    {
        // Gemini API format: https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={key}
        // The model name should be without "models/" prefix if coming from config
        var modelName = _config.Model;
        if (!modelName.StartsWith("models/"))
        {
            modelName = $"models/{modelName}";
        }

        var baseUrl = _config.EndpointUrl.TrimEnd('/');
        return $"{baseUrl}/{modelName}:generateContent?key={_apiKey}";
    }

    private DslGenerateResult ParseGeminiResponse(string responseBody, string requestId)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            // Gemini response structure: { candidates: [{ content: { parts: [{ text: "..." }] } }] }
            if (!root.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            {
                _logger.LogWarning("Gemini response missing candidates: RequestId={RequestId}", requestId);
                throw new AiProviderException(
                    AiErrorCodes.AiOutputInvalid,
                    "Gemini response missing 'candidates' array");
            }

            var firstCandidate = candidates[0];
            if (!firstCandidate.TryGetProperty("content", out var content) ||
                !content.TryGetProperty("parts", out var parts) ||
                parts.GetArrayLength() == 0)
            {
                _logger.LogWarning("Gemini response missing content.parts: RequestId={RequestId}", requestId);
                throw new AiProviderException(
                    AiErrorCodes.AiOutputInvalid,
                    "Gemini response missing 'content.parts' array");
            }

            var firstPart = parts[0];
            if (!firstPart.TryGetProperty("text", out var textElement))
            {
                _logger.LogWarning("Gemini response part missing text: RequestId={RequestId}", requestId);
                throw new AiProviderException(
                    AiErrorCodes.AiOutputInvalid,
                    "Gemini response part missing 'text' field");
            }

            var textContent = textElement.GetString();
            if (string.IsNullOrWhiteSpace(textContent))
            {
                _logger.LogWarning("Gemini response text is empty: RequestId={RequestId}", requestId);
                throw new AiProviderException(
                    AiErrorCodes.AiOutputInvalid,
                    "Gemini response text is empty");
            }

            // Parse the text content as JSON (handle markdown code blocks if present)
            var (parseSuccess, parsedJson, _, errorDetails) = LlmResponseParser.TryParseJsonResponse(textContent);
            if (!parseSuccess || parsedJson == null)
            {
                _logger.LogWarning(
                    "Gemini response not valid JSON: RequestId={RequestId}, Error={Error}",
                    requestId, errorDetails);
                throw new AiProviderException(
                    AiErrorCodes.AiOutputInvalid,
                    $"Gemini response is not valid JSON: {errorDetails}");
            }

            // Deserialize to DslGenerateResult
            var result = JsonSerializer.Deserialize<DslGenerateResult>(
                parsedJson.Value.GetRawText(),
                JsonOptions);

            if (result == null)
            {
                _logger.LogWarning("Failed to deserialize Gemini response: RequestId={RequestId}", requestId);
                throw new AiProviderException(
                    AiErrorCodes.AiOutputInvalid,
                    "Failed to deserialize Gemini response as DslGenerateResult");
            }

            _logger.LogInformation(
                "Gemini success: RequestId={RequestId}, Model={Model}, DslProfile={Profile}",
                requestId, _config.Model, result.Dsl.Profile);

            return result with
            {
                ModelInfo = new ModelInfo
                {
                    Provider = ProviderName,
                    Model = _config.Model,
                    PromptVersion = _config.PromptVersion
                }
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Gemini JSON parse error: RequestId={RequestId}", requestId);
            throw new AiProviderException(
                AiErrorCodes.AiOutputInvalid,
                $"Failed to parse Gemini response: {ex.Message}");
        }
    }

    private object BuildChatRequest(string systemPrompt, string userPrompt)
    {
        // Gemini API request format
        var contents = new object[]
        {
            new
            {
                role = "user",
                parts = new object[]
                {
                    new { text = $"{systemPrompt}\n\n{userPrompt}" }
                }
            }
        };

        return new
        {
            contents,
            generationConfig = new
            {
                temperature = 0.2, // Lower temperature for more deterministic output
                topP = _config.TopP,
                maxOutputTokens = _config.MaxTokens
            }
        };
    }

    private string BuildSystemPrompt(DslGenerateRequest request)
    {
        // Reuse the same system prompt from HttpOpenAiCompatibleProvider
        // This is the same comprehensive prompt with Jsonata rules
        return @"You are an expert Jsonata DSL specialist. Your task is to generate valid Jsonata expressions.

## Your Responsibility
Generate a Jsonata expression that transforms the input JSON according to the user's goal.

## JSON Schema Contract
You MUST return valid JSON matching this exact structure:
{
  ""dsl"": {
    ""profile"": ""jsonata"",
    ""text"": ""<your jsonata expression>""
  },
  ""outputSchema"": ""<JSON Schema as string>"",
  ""rationale"": ""<brief explanation>"",
  ""warnings"": [<array of strings, may be empty>]
}";
    }

    private string BuildUserPrompt(DslGenerateRequest request)
    {
        // Reuse the same user prompt structure
        var sampleInputJson = JsonSerializer.Serialize(request.SampleInput, new JsonSerializerOptions { WriteIndented = true });
        
        return $@"Transform the following sample input according to the goal:

**Goal:** {request.GoalText}

**Sample Input:**
```json
{sampleInputJson}
```

**Constraints:**
- Max columns: {request.Constraints.MaxColumns}
- Allow transforms: {request.Constraints.AllowTransforms}
- Forbid network calls: {request.Constraints.ForbidNetworkCalls}
- Forbid code execution: {request.Constraints.ForbidCodeExecution}

Generate the Jsonata expression and output schema.";
    }
}

/// <summary>
/// Gemini API error response structure (for debugging)
/// </summary>
public record GeminiErrorResponse
{
    [JsonPropertyName("error")]
    public GeminiError? Error { get; init; }
}

public record GeminiError
{
    [JsonPropertyName("code")]
    public int Code { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }
}
