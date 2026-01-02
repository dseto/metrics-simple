using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;

namespace Metrics.Api.AI;

/// <summary>
/// HTTP provider for OpenAI-compatible APIs (OpenRouter, OpenAI, Azure OpenAI, etc.)
/// Implements the contract defined in specs/backend/08-ai-assist/ai-provider-contract.md
/// 
/// Features:
/// - Structured Outputs (response_format with json_schema) for deterministic JSON
/// - Response-healing plugin for auto-repair of malformed JSON
/// - Comprehensive Jsonata dialect rules in system prompt
/// </summary>
public class HttpOpenAiCompatibleProvider : IAiProvider
{
    private readonly HttpClient _httpClient;
    private readonly AiConfiguration _config;
    private readonly ILogger<HttpOpenAiCompatibleProvider> _logger;
    private readonly string _apiKey;

    public string ProviderName => "HttpOpenAICompatible";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// JSON Schema for structured output - forces LLM to return valid DslGenerateResult
    /// NOTE: OpenAI Structured Outputs with strict=true requires additionalProperties=false
    /// on ALL objects. Since outputSchema is dynamic, we use a string type and parse it.
    /// </summary>
    private static readonly object DslResultJsonSchema = new
    {
        type = "object",
        properties = new
        {
            dsl = new
            {
                type = "object",
                properties = new
                {
                    profile = new { type = "string", @enum = new[] { "jsonata" } },
                    text = new { type = "string", description = "The Jsonata expression" }
                },
                required = new[] { "profile", "text" },
                additionalProperties = false
            },
            outputSchema = new
            {
                type = "string",
                description = "JSON Schema for the output, as a JSON string that will be parsed"
            },
            rationale = new { type = "string", description = "Brief explanation of the transformation" },
            warnings = new
            {
                type = "array",
                items = new { type = "string" }
            }
        },
        required = new[] { "dsl", "outputSchema", "rationale", "warnings" },
        additionalProperties = false
    };

    public HttpOpenAiCompatibleProvider(
        HttpClient httpClient,
        AiConfiguration config,
        ILogger<HttpOpenAiCompatibleProvider> logger)
    {
        _httpClient = httpClient;
        _config = config;
        _logger = logger;

        // Get API key from environment variables (priority: METRICS_OPENROUTER_API_KEY > OPENROUTER_API_KEY)
        _apiKey = Environment.GetEnvironmentVariable("METRICS_OPENROUTER_API_KEY")
                  ?? Environment.GetEnvironmentVariable("OPENROUTER_API_KEY")
                  ?? throw new AiProviderException(
                      AiErrorCodes.AiProviderUnavailable,
                      "API key not configured. Set METRICS_OPENROUTER_API_KEY or OPENROUTER_API_KEY environment variable.");

        _httpClient.Timeout = TimeSpan.FromSeconds(_config.TimeoutSeconds);
    }

    public async Task<DslGenerateResult> GenerateDslAsync(DslGenerateRequest request, CancellationToken ct)
    {
        var retryCount = 0;
        var maxRetries = _config.MaxRetries;

        while (true)
        {
            try
            {
                return await ExecuteRequestAsync(request, ct);
            }
            catch (Exception ex) when (IsTransientError(ex) && retryCount < maxRetries)
            {
                retryCount++;
                _logger.LogWarning(ex, "Transient error on attempt {Attempt}, retrying after backoff", retryCount);
                await Task.Delay(TimeSpan.FromMilliseconds(250 * retryCount), ct);
            }
        }
    }

    private async Task<DslGenerateResult> ExecuteRequestAsync(DslGenerateRequest request, CancellationToken ct)
    {
        var systemPrompt = BuildSystemPrompt(request);
        var userPrompt = BuildUserPrompt(request);

        // Build the chat request with structured outputs if enabled
        var chatRequest = BuildChatRequest(systemPrompt, userPrompt);

        var json = JsonSerializer.Serialize(chatRequest, JsonOptions);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, _config.EndpointUrl);
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        httpRequest.Content = content;

        _logger.LogInformation("Sending request to AI provider: {Endpoint}, Model: {Model}, StructuredOutputs: {Structured}",
            _config.EndpointUrl, _config.Model, _config.EnableStructuredOutputs);

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(httpRequest, ct);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new AiProviderException(AiErrorCodes.AiTimeout, "Request to AI provider timed out", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new AiProviderException(AiErrorCodes.AiProviderUnavailable,
                $"Failed to connect to AI provider: {ex.Message}", ex);
        }

        // Handle rate limiting
        if (response.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new AiProviderException(AiErrorCodes.AiRateLimited,
                "AI provider rate limit exceeded. Please try again later.");
        }

        // Handle server errors (transient)
        if ((int)response.StatusCode >= 500)
        {
            throw new AiProviderException(AiErrorCodes.AiProviderUnavailable,
                $"AI provider returned server error: {response.StatusCode}");
        }

        // Handle client errors
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("AI provider error: {StatusCode} - {Body}", response.StatusCode, errorBody);
            throw new AiProviderException(AiErrorCodes.AiProviderUnavailable,
                $"AI provider returned error: {response.StatusCode}");
        }

        // Parse response
        var responseBody = await response.Content.ReadAsStringAsync(ct);
        return ParseChatCompletionResponse(responseBody, request);
    }

    /// <summary>
    /// Builds the chat request with optional structured outputs and plugins
    /// </summary>
    private object BuildChatRequest(string systemPrompt, string userPrompt)
    {
        var messages = new[]
        {
            new { role = "system", content = systemPrompt },
            new { role = "user", content = userPrompt }
        };

        // Build base request
        var requestObj = new JsonObject
        {
            ["model"] = _config.Model,
            ["messages"] = JsonSerializer.SerializeToNode(messages),
            ["max_tokens"] = _config.MaxTokens,
            ["temperature"] = _config.Temperature,
            ["top_p"] = _config.TopP,
            ["stream"] = false
        };

        // Add structured outputs (OpenRouter response_format)
        if (_config.EnableStructuredOutputs)
        {
            requestObj["response_format"] = new JsonObject
            {
                ["type"] = "json_schema",
                ["json_schema"] = new JsonObject
                {
                    ["name"] = "dsl_generate_result",
                    ["strict"] = true,
                    ["schema"] = JsonSerializer.SerializeToNode(DslResultJsonSchema)
                }
            };
        }

        // Add response-healing plugin (OpenRouter specific)
        if (_config.EnableResponseHealing)
        {
            requestObj["plugins"] = JsonSerializer.SerializeToNode(new[]
            {
                new { id = "response-healing" }
            });
        }

        return requestObj;
    }

    private DslGenerateResult ParseChatCompletionResponse(string responseBody, DslGenerateRequest request)
    {
        try
        {
            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;

            // Extract choices[0].message.content
            if (!root.TryGetProperty("choices", out var choices) ||
                choices.GetArrayLength() == 0)
            {
                throw new AiProviderException(AiErrorCodes.AiOutputInvalid,
                    "AI provider response missing choices array");
            }

            var firstChoice = choices[0];
            if (!firstChoice.TryGetProperty("message", out var message) ||
                !message.TryGetProperty("content", out var contentElement))
            {
                throw new AiProviderException(AiErrorCodes.AiOutputInvalid,
                    "AI provider response missing message.content");
            }

            var contentString = contentElement.GetString();
            if (string.IsNullOrWhiteSpace(contentString))
            {
                throw new AiProviderException(AiErrorCodes.AiOutputInvalid,
                    "AI provider returned empty content");
            }

            // Clean up markdown code blocks if present
            contentString = CleanJsonContent(contentString);

            // Parse the content as JSON
            using var contentDoc = JsonDocument.Parse(contentString);
            var contentRoot = contentDoc.RootElement;

            // Extract and validate DSL
            if (!contentRoot.TryGetProperty("dsl", out var dslElement))
            {
                throw new AiProviderException(AiErrorCodes.AiOutputInvalid,
                    "AI provider response missing 'dsl' property");
            }

            var profile = dslElement.TryGetProperty("profile", out var profileEl)
                ? profileEl.GetString() ?? request.DslProfile
                : request.DslProfile;

            var text = dslElement.TryGetProperty("text", out var textEl)
                ? textEl.GetString() ?? ""
                : "";

            if (string.IsNullOrWhiteSpace(text))
            {
                throw new AiProviderException(AiErrorCodes.AiOutputInvalid,
                    "AI provider response missing 'dsl.text'");
            }

            // Extract and validate outputSchema
            if (!contentRoot.TryGetProperty("outputSchema", out var schemaElement))
            {
                throw new AiProviderException(AiErrorCodes.AiOutputInvalid,
                    "AI provider response missing 'outputSchema' property");
            }

            // If outputSchema is a string, parse it
            JsonElement outputSchema;
            if (schemaElement.ValueKind == JsonValueKind.String)
            {
                var schemaString = schemaElement.GetString()!;
                
                // Try to parse, and if it fails, attempt to fix common LLM mistakes
                schemaString = TryFixMalformedJson(schemaString);
                
                _logger.LogDebug("Parsing outputSchema string: {SchemaString}", schemaString);
                try
                {
                    using var schemaDoc = JsonDocument.Parse(schemaString);
                    outputSchema = schemaDoc.RootElement.Clone();
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to parse outputSchema string: {SchemaString}", schemaString);
                    throw new AiProviderException(AiErrorCodes.AiOutputInvalid,
                        $"outputSchema is not valid JSON: {ex.Message}. Raw: {schemaString[..Math.Min(200, schemaString.Length)]}", ex);
                }
            }
            else
            {
                outputSchema = schemaElement.Clone();
            }

            // Validate outputSchema is an object
            if (outputSchema.ValueKind != JsonValueKind.Object)
            {
                throw new AiProviderException(AiErrorCodes.AiOutputInvalid,
                    "outputSchema must be a JSON object");
            }

            // Extract rationale
            var rationale = contentRoot.TryGetProperty("rationale", out var rationaleEl)
                ? rationaleEl.GetString() ?? ""
                : "";

            // Truncate rationale if too long
            if (rationale.Length > 500)
            {
                rationale = rationale[..500] + "...";
            }

            // Extract warnings
            var warnings = new List<string>();
            if (contentRoot.TryGetProperty("warnings", out var warningsEl) &&
                warningsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var warning in warningsEl.EnumerateArray())
                {
                    var w = warning.GetString();
                    if (!string.IsNullOrWhiteSpace(w))
                    {
                        warnings.Add(w);
                    }
                }
            }

            // Extract optional exampleRows
            JsonElement? exampleRows = null;
            if (contentRoot.TryGetProperty("exampleRows", out var exampleRowsEl) &&
                exampleRowsEl.ValueKind == JsonValueKind.Array)
            {
                exampleRows = exampleRowsEl.Clone();
            }

            return new DslGenerateResult
            {
                Dsl = new DslOutput
                {
                    Profile = profile,
                    Text = text
                },
                OutputSchema = outputSchema,
                ExampleRows = exampleRows,
                Rationale = rationale,
                Warnings = warnings,
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
            _logger.LogError(ex, "Failed to parse AI provider response as JSON");
            throw new AiProviderException(AiErrorCodes.AiOutputInvalid,
                $"Failed to parse AI response as JSON: {ex.Message}", ex);
        }
    }

    private static string CleanJsonContent(string content)
    {
        content = content.Trim();

        // Remove markdown code blocks
        if (content.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            content = content[7..];
        }
        else if (content.StartsWith("```"))
        {
            content = content[3..];
        }

        if (content.EndsWith("```"))
        {
            content = content[..^3];
        }

        return content.Trim();
    }

    /// <summary>
    /// Attempts to fix common LLM mistakes in JSON output:
    /// - Extra trailing braces (LLM sometimes adds extra } at the end)
    /// - Unbalanced braces
    /// </summary>
    private static string TryFixMalformedJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return json;
        
        json = json.Trim();
        
        // Try parsing as-is first
        try
        {
            using var doc = JsonDocument.Parse(json);
            return json; // Already valid
        }
        catch (JsonException)
        {
            // Continue to fix attempts
        }
        
        // Fix 1: Remove trailing extra closing braces
        // Count braces to find imbalance
        int openBraces = 0;
        int closeBraces = 0;
        int openBrackets = 0;
        int closeBrackets = 0;
        bool inString = false;
        char prevChar = '\0';
        
        foreach (var c in json)
        {
            if (c == '"' && prevChar != '\\')
            {
                inString = !inString;
            }
            else if (!inString)
            {
                switch (c)
                {
                    case '{': openBraces++; break;
                    case '}': closeBraces++; break;
                    case '[': openBrackets++; break;
                    case ']': closeBrackets++; break;
                }
            }
            prevChar = c;
        }
        
        // If there are more closing braces than opening, trim the excess
        int excessClosingBraces = closeBraces - openBraces;
        if (excessClosingBraces > 0)
        {
            // Remove trailing } characters
            while (excessClosingBraces > 0 && json.EndsWith("}"))
            {
                json = json[..^1].TrimEnd();
                excessClosingBraces--;
            }
        }
        
        // Same for brackets
        int excessClosingBrackets = closeBrackets - openBrackets;
        if (excessClosingBrackets > 0)
        {
            while (excessClosingBrackets > 0 && json.EndsWith("]"))
            {
                json = json[..^1].TrimEnd();
                excessClosingBrackets--;
            }
        }
        
        return json;
    }

    private string BuildSystemPrompt(DslGenerateRequest request)
    {
        // Comprehensive system prompt with Jsonata dialect rules
        // These rules prevent common LLM mistakes when generating Jsonata
        return $$"""
            You are a Jsonata DSL generator for data transformation. Generate ONLY valid Jsonata expressions.

            ═══════════════════════════════════════════════════════════════════════════════
            CRITICAL JSONATA DIALECT RULES (STRICT - violations cause compilation errors)
            ═══════════════════════════════════════════════════════════════════════════════

            1. ROOT PATH: In Jsonata, root is IMPLICIT. Start with field name directly.
               ✅ VALID:   users.firstName
               ❌ INVALID: $.users.firstName  (jQuery/JSONPath syntax - FORBIDDEN)
               ❌ INVALID: $['users']         (JSONPath bracket notation - FORBIDDEN)

            2. NEGATION: Use 'not' operator or explicit comparison.
               ✅ VALID:   users[inactive = false]
               ✅ VALID:   users[not inactive]
               ❌ INVALID: users[!inactive]   ('!' operator does not exist in Jsonata)

            3. AGGREGATION FUNCTIONS: These are BUILT-IN in Jsonata.
               ✅ VALID:   $sum(sales.quantity)
               ✅ VALID:   $average(sales.price)
               ✅ VALID:   $sum(sales.(quantity * price))   -- computed expression
               ❌ INVALID: sales.quantity | $sum()          -- wrong pipe syntax

            4. STRING CONCATENATION: Use '&' operator.
               ✅ VALID:   firstName & " " & lastName
               ❌ INVALID: firstName + " " + lastName

            5. OBJECT CONSTRUCTION: Use curly braces with key: value.
               ✅ VALID:   users.{ "fullName": firstName & " " & lastName, "email": email }
               ✅ VALID:   { "total": $sum(items.price), "count": $count(items) }

            6. REGEX with $match: Returns array with groups property.
               ✅ VALID:   $match(text, /Memory:\s*(\d+)MB/)[0].groups[0]
               ❌ INVALID: $match(text, /pattern/)[0][1]    -- wrong indexing
               ❌ INVALID: $m := $match(...); $m[1]         -- invalid variable access

            7. NUMBER EXTRACTION from strings:
               ✅ VALID:   $number($match(text, /(\d+)/)[0].groups[0])
               ✅ VALID:   $number($substringBefore($substringAfter(text, "Memory: "), "MB"))

            8. ARRAY OUTPUT: For CSV generation, result MUST be an array of objects OR single object.
               ✅ VALID:   users.{ "name": name, "age": age }              -- maps array to array of objects
               ✅ VALID:   { "total": $sum(items.price) }                  -- single object (engine wraps to array)
               ❌ INVALID: [{ "total": $sum(items.price) }]                -- [] is NOT array creation syntax!

            9. AGGREGATION PATTERNS: For computing totals/averages across an array:
               ✅ VALID:   { "total": $sum(sales.quantity), "avg": $average(sales.price) }
               ❌ INVALID: [{ "total": $sum(sales.quantity) }]            -- DO NOT wrap in []
               The engine will automatically wrap single objects in an array for CSV output.

            ═══════════════════════════════════════════════════════════════════════════════
            RESPONSE FORMAT (JSON only, no markdown)
            ═══════════════════════════════════════════════════════════════════════════════

            Return EXACTLY this JSON structure:
            {
              "dsl": {
                "profile": "{{request.DslProfile}}",
                "text": "YOUR_JSONATA_EXPRESSION_HERE"
              },
              "outputSchema": "STRINGIFIED_JSON_SCHEMA_HERE",
              "rationale": "Brief explanation of transformation logic",
              "warnings": []
            }

            CRITICAL: outputSchema MUST be a JSON STRING (stringified JSON with escaped quotes).
            Do NOT return outputSchema as an object - stringify it first.

            ═══════════════════════════════════════════════════════════════════════════════
            CONSTRAINTS
            ═══════════════════════════════════════════════════════════════════════════════
            
            - Maximum output columns: {{request.Constraints.MaxColumns}}
            - Allow transforms: {{request.Constraints.AllowTransforms}}
            - NEVER include network/HTTP calls in DSL
            - NEVER include code execution in DSL
            - If EXISTING OUTPUT SCHEMA is provided, preserve its structure exactly (it is the SSOT)
            """;
    }

    private string BuildUserPrompt(DslGenerateRequest request)
    {
        var sb = new StringBuilder();
        sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
        sb.AppendLine("TRANSFORMATION GOAL:");
        sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
        sb.AppendLine(TruncateText(request.GoalText, 4000));
        sb.AppendLine();

        sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
        sb.AppendLine("SAMPLE INPUT DATA:");
        sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
        var sampleInputJson = JsonSerializer.Serialize(request.SampleInput, new JsonSerializerOptions { WriteIndented = true });
        sb.AppendLine(TruncateSampleInput(sampleInputJson, 500_000));
        sb.AppendLine();

        // Existing DSL for repair attempts
        if (!string.IsNullOrWhiteSpace(request.ExistingDsl))
        {
            sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
            sb.AppendLine("PREVIOUS DSL ATTEMPT (FAILED - fix the errors below):");
            sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
            sb.AppendLine(TruncateText(request.ExistingDsl, 20000));
            sb.AppendLine();
        }

        // Hints (validation errors for repair attempts)
        if (request.Hints != null && request.Hints.Count > 0)
        {
            sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
            sb.AppendLine("VALIDATION ERRORS TO FIX:");
            sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
            foreach (var hint in request.Hints)
            {
                sb.AppendLine($"• {hint.Key}: {hint.Value}");
            }
            sb.AppendLine();
            sb.AppendLine("IMPORTANT: Review the Jsonata dialect rules and fix the syntax errors above.");
            sb.AppendLine();
        }

        // Existing output schema (SSOT - must be preserved)
        if (request.ExistingOutputSchema.HasValue)
        {
            sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
            sb.AppendLine("REQUIRED OUTPUT SCHEMA (SSOT - preserve this structure exactly):");
            sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
            sb.AppendLine(JsonSerializer.Serialize(request.ExistingOutputSchema.Value, new JsonSerializerOptions { WriteIndented = true }));
            sb.AppendLine();
            sb.AppendLine("The outputSchema in your response MUST match this structure. Do not change field names or types.");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        return text[..maxLength] + "... [TRUNCATED]";
    }

    private static string TruncateSampleInput(string json, int maxBytes)
    {
        var bytes = Encoding.UTF8.GetByteCount(json);
        if (bytes <= maxBytes)
            return json;

        // Simple truncation - take first N chars that fit
        var chars = maxBytes / 3; // conservative estimate for UTF-8
        return json[..chars] + "... [TRUNCATED - input too large]";
    }

    private static bool IsTransientError(Exception ex)
    {
        if (ex is TaskCanceledException)
            return true;

        if (ex is AiProviderException ape)
        {
            return ape.ErrorCode == AiErrorCodes.AiTimeout ||
                   ape.ErrorCode == AiErrorCodes.AiProviderUnavailable ||
                   ape.ErrorCode == AiErrorCodes.AiRateLimited;
        }

        if (ex is HttpRequestException)
            return true;

        return false;
    }
}
