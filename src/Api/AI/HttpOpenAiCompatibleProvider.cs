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
        DslErrorClassifier.ErrorCategory? lastErrorCategory = null;
        var requestId = Guid.NewGuid().ToString("N")[..12];

        while (true)
        {
            try
            {
                _logger.LogInformation(
                    "DSL generation attempt {Attempt}/{MaxRetries}: RequestId={RequestId}, Model={Model}, GoalLength={GoalLength}",
                    retryCount + 1, maxRetries, requestId, _config.Model, request.GoalText.Length);

                return await ExecuteRequestAsync(request, ct, requestId, retryCount + 1);
            }
            catch (AiProviderException aex) when (retryCount < maxRetries)
            {
                // Classify the error
                var errorCategory = DslErrorClassifier.Classify(aex.Message, aex.ErrorCode);
                
                _logger.LogWarning(
                    "DSL generation failed on attempt {Attempt}: ErrorCode={Code}, ErrorCategory={Category}, Message={Message}, RequestId={RequestId}",
                    retryCount + 1, aex.ErrorCode, errorCategory, aex.Message, requestId);

                // Check if same error is repeating
                if (lastErrorCategory == errorCategory)
                {
                    _logger.LogWarning(
                        "Same error category detected twice ({Category}). Stopping retry. RequestId={RequestId}",
                        errorCategory, requestId);
                    throw;
                }

                lastErrorCategory = errorCategory;

                // Only retry if error is retryable
                if (!DslErrorClassifier.IsRetryable(errorCategory))
                {
                    _logger.LogError(
                        "Error category {Category} is not retryable. Giving up. RequestId={RequestId}",
                        errorCategory, requestId);
                    throw;
                }

                retryCount++;
                var backoffMs = 500 * retryCount;  // Increased backoff
                _logger.LogInformation(
                    "Retrying in {BackoffMs}ms... (attempt {Attempt}/{MaxRetries}) RequestId={RequestId}",
                    backoffMs, retryCount + 1, maxRetries, requestId);
                
                await Task.Delay(TimeSpan.FromMilliseconds(backoffMs), ct);
            }
            catch (Exception ex) when (IsTransientError(ex) && retryCount < maxRetries)
            {
                retryCount++;
                _logger.LogWarning(ex, 
                    "Transient error on attempt {Attempt}/{MaxRetries}, retrying after backoff. RequestId={RequestId}",
                    retryCount + 1, maxRetries, requestId);
                await Task.Delay(TimeSpan.FromMilliseconds(250 * retryCount), ct);
            }
        }
    }

    private async Task<DslGenerateResult> ExecuteRequestAsync(DslGenerateRequest request, CancellationToken ct, string requestId, int attemptNumber)
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

        _logger.LogInformation(
            "Sending HTTP request to AI provider (attempt {Attempt}): Endpoint={Endpoint}, Model={Model}, StructuredOutputs={Structured}, RequestId={RequestId}",
            attemptNumber, _config.EndpointUrl, _config.Model, _config.EnableStructuredOutputs, requestId);

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
            _logger.LogError("AI provider HTTP error (attempt {Attempt}): {StatusCode} - {Body}, RequestId={RequestId}", 
                attemptNumber, response.StatusCode, errorBody, requestId);
            throw new AiProviderException(AiErrorCodes.AiProviderUnavailable,
                $"AI provider returned error: {response.StatusCode}");
        }

        // Parse response with resilient parsing
        var responseBody = await response.Content.ReadAsStringAsync(ct);
        return ParseChatCompletionResponse(responseBody, request, requestId, attemptNumber);
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

    private DslGenerateResult ParseChatCompletionResponse(string responseBody, DslGenerateRequest request, string requestId, int attemptNumber)
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

            // Use resilient parsing with 3-strategy fallback
            _logger.LogDebug("Attempt to parse LLM response (attempt {Attempt}, RequestId={RequestId}): Content length={Length}", 
                attemptNumber, requestId, contentString.Length);
            
            var (parseSuccess, parsedContent, errorCategory, errorDetails) = LlmResponseParser.TryParseJsonResponse(contentString);
            if (!parseSuccess)
            {
                _logger.LogWarning(
                    "Failed to parse LLM response with resilient parser (attempt {Attempt}, RequestId={RequestId}): {Category} - {Details}",
                    attemptNumber, requestId, errorCategory, errorDetails);
                throw new AiProviderException(AiErrorCodes.AiOutputInvalid,
                    $"Failed to parse LLM response as JSON: {errorCategory} - {errorDetails}");
            }

            var contentRoot = parsedContent!.Value;

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

            // ⚠️ IMPORTANT: LLM may provide outputSchema (old contract) or not (new contract).
            // We accept BOTH gracefully but always infer schema server-side from actual preview output.
            var outputSchema = JsonSerializer.SerializeToElement(new { });
            
            if (contentRoot.TryGetProperty("outputSchema", out var schemaElement))
            {
                _logger.LogInformation("LLM returned old contract (with outputSchema). Accepting for backward compatibility, but backend will infer from preview");
                
                // Parse if it's a string, otherwise use as-is
                if (schemaElement.ValueKind == JsonValueKind.String)
                {
                    var schemaString = schemaElement.GetString()!;
                    var (schemaParse, schemaParsed, _, _) = LlmResponseParser.TryParseJsonResponse(schemaString);
                    if (schemaParse)
                    {
                        outputSchema = schemaParsed!.Value;
                    }
                }
                else if (schemaElement.ValueKind == JsonValueKind.Object)
                {
                    outputSchema = schemaElement.Clone();
                }
            }
            else
            {
                _logger.LogDebug("LLM returned new contract (without outputSchema)");
            }

            // Extract notes (optional, replaces "rationale")
            var notes = contentRoot.TryGetProperty("notes", out var notesEl)
                ? notesEl.GetString() ?? ""
                : "";

            // Try to get "rationale" for backward compatibility with old LLM responses
            if (string.IsNullOrWhiteSpace(notes) && contentRoot.TryGetProperty("rationale", out var rationaleEl))
            {
                notes = rationaleEl.GetString() ?? "";
            }

            // Truncate notes if too long
            if (notes.Length > 500)
            {
                notes = notes[..500] + "...";
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

            _logger.LogInformation("Successfully parsed DSL from LLM response (attempt {Attempt}, RequestId={RequestId}): DSL length={DslLength}, Profile={Profile}",
                attemptNumber, requestId, text.Length, profile);

            return new DslGenerateResult
            {
                Dsl = new DslOutput
                {
                    Profile = profile,
                    Text = text
                },
                OutputSchema = outputSchema,
                ExampleRows = exampleRows,
                Rationale = notes,
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
            _logger.LogError(ex, "Failed to parse AI provider response as JSON (RequestId provided in context)");
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
    /// - "key"= instead of "key": (LLM confuses = with :)
    /// - Duplicate quotes ""key"" instead of "key"
    /// - Runaway quote sequences """"""""
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
        
        // Fix 1: Replace runaway quote sequences (common LLM hallucination)
        // Pattern: 3+ consecutive quotes -> reduce to single quote
        while (json.Contains("\"\"\""))
        {
            json = json.Replace("\"\"\"", "\"");
        }
        
        // Fix 1b: Convert Python-style single quotes to JSON double quotes
        // This handles LLMs that output {'key': 'value'} instead of {"key": "value"}
        // Only apply if the string starts with { and uses single quotes
        if (json.StartsWith("{") || json.StartsWith("["))
        {
            bool hasSingleQuotes = json.Contains("'");
            bool hasDoubleQuotes = json.Contains("\"");
            
            // If we have single quotes but very few or no double quotes, convert
            if (hasSingleQuotes && !hasDoubleQuotes)
            {
                json = json.Replace("'", "\"");
            }
        }
        
        // Fix 2: Replace "key"= with "key": (LLM sometimes uses = instead of :)
        // Pattern: "word"= -> "word":
        json = System.Text.RegularExpressions.Regex.Replace(
            json,
            "\"([a-zA-Z_][a-zA-Z0-9_]*)\"=",
            "\"$1\":");
        
        // Fix 3: Replace {"" with {" (double quote at start of key after brace)
        json = json.Replace("{\"\"", "{\"");
        json = json.Replace(",\"\"", ",\"");
        
        // Fix 4: Replace ""} with "} (double quote at end of value before brace)
        json = json.Replace("\"\"}", "\"}");
        json = json.Replace("\"\"]", "\"]");
        json = json.Replace("\"\",", "\",");
        
        // Fix 5: Truncate if there's garbage after a seemingly complete JSON
        int lastValidEnd = FindBalancedJsonEnd(json);
        if (lastValidEnd > 0 && lastValidEnd < json.Length - 1)
        {
            json = json[..(lastValidEnd + 1)];
        }
        
        // Fix 6: Remove trailing extra closing braces
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
        
        // Fix 7: If JSON is truncated (missing closing braces/brackets), try to complete it
        // This happens when LLM output is cut off mid-stream
        int missingBraces = openBraces - closeBraces;
        int missingBrackets = openBrackets - closeBrackets;
        
        if (missingBraces > 0 || missingBrackets > 0)
        {
            // First, try to fix incomplete strings (truncated in middle of value)
            // Count unescaped quotes
            int quoteCount = 0;
            prevChar = '\0';
            foreach (var c in json)
            {
                if (c == '"' && prevChar != '\\')
                    quoteCount++;
                prevChar = c;
            }
            
            // If odd number of quotes, add a closing quote
            if (quoteCount % 2 != 0)
            {
                json += "\"";
            }
            
            // Add missing closing brackets first (inner), then braces (outer)
            for (int i = 0; i < missingBrackets; i++)
            {
                json += "]";
            }
            for (int i = 0; i < missingBraces; i++)
            {
                json += "}";
            }
        }
        
        return json;
    }
    
    /// <summary>
    /// Finds the position of the last balanced closing brace/bracket in JSON.
    /// Returns -1 if JSON structure is completely broken.
    /// </summary>
    private static int FindBalancedJsonEnd(string json)
    {
        int depth = 0;
        bool inString = false;
        char prevChar = '\0';
        int lastBalancedPos = -1;
        
        for (int i = 0; i < json.Length; i++)
        {
            char c = json[i];
            
            if (c == '"' && prevChar != '\\')
            {
                inString = !inString;
            }
            else if (!inString)
            {
                if (c == '{' || c == '[')
                {
                    depth++;
                }
                else if (c == '}' || c == ']')
                {
                    depth--;
                    if (depth == 0)
                    {
                        lastBalancedPos = i;
                    }
                }
            }
            
            prevChar = c;
        }
        
        return lastBalancedPos;
    }

    private string BuildSystemPrompt(DslGenerateRequest request)
    {
        // Comprehensive system prompt with Jsonata dialect rules
        // These rules prevent common LLM mistakes when generating Jsonata
        return $$"""
            You are a Jsonata DSL generator for data transformation.
            
            CRITICAL OUTPUT RULES:
            - Generate ONLY valid Jsonata expressions
            - NO markdown code blocks (no ```)
            - NO explanations or comments outside JSON
            - NO prefixes like "Here's the expression:"
            - Output MUST be pure JSON matching the schema below

            ═══════════════════════════════════════════════════════════════════════════════
            CRITICAL: ANALYZE SAMPLE INPUT STRUCTURE BEFORE WRITING DSL
            ═══════════════════════════════════════════════════════════════════════════════

            MANDATORY WORKFLOW:
            1. FIRST, carefully analyze the SAMPLE INPUT DATA structure provided
            2. IDENTIFY the exact path to each field you need to reference
            3. VERIFY paths exist in the sample before using them in DSL
            4. NEVER assume paths - use only paths that EXIST in the sample

            PATH VERIFICATION RULES:
            - If data is at "results.forecast" in sample, use "results.forecast" NOT just "forecast"
            - If data is nested (e.g. object "results" containing "data" array), path is "results.data" NOT "data"
            - If root is an array, iterate directly; if root is object with array property, use that path
            - ALWAYS trace the full path from root to target field in the sample

            COMMON PATH MISTAKES TO AVOID:
            ❌ Using "forecast" when sample shows object with results.forecast nested
               → Use "results.forecast" instead
            ❌ Using "data" when sample shows object with response.data nested  
               → Use "response.data" instead
            ❌ Assuming array is at root when sample shows object containing "items" array
               → Use "items" as the array path

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
               ❌ INVALID: result.{ "key": value }          -- FORBIDDEN: "result." prefix before object
               
               If you need to wrap result, use: { "result": expression }
               NOT: result.{ ... }

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

            10. ARITHMETIC ON COLLECTIONS: When performing arithmetic on array elements:
               ✅ VALID:   $average(items.((max + min) / 2))              -- double parentheses for expression
               ✅ VALID:   $sum(items.(price * quantity))                 -- single parentheses for simple expression
               ❌ INVALID: $average(items.(max + min) / 2)                -- / 2 outside the mapping context
               ❌ INVALID: $average(items.max + items.min) / 2            -- wrong structure
               
               RULE: When mapping an expression over an array, wrap the ENTIRE expression in parentheses
               Example: To average (max+min)/2 for each item in "results.forecast":
               ✅ $average(results.forecast.((max + min) / 2))

            ═══════════════════════════════════════════════════════════════════════════════
            RESPONSE FORMAT (JSON only, no markdown)
            ═══════════════════════════════════════════════════════════════════════════════

            Return EXACTLY this JSON structure:
            {
              "dsl": {
                "profile": "{{request.DslProfile}}",
                "text": "YOUR_JSONATA_EXPRESSION_HERE"
              },
              "notes": "Optional notes about the transformation",
              "warnings": []
            }

            ⚠️ IMPORTANT: Do NOT include "outputSchema" in your response.
            The server will automatically infer the output schema from the transformation result.

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

        // Add structure analysis to help LLM understand paths
        sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
        sb.AppendLine("INPUT STRUCTURE ANALYSIS (use these exact paths in your DSL):");
        sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
        sb.AppendLine(AnalyzeJsonStructure(request.SampleInput, "", 0));
        sb.AppendLine();
        sb.AppendLine("REMINDER: Use the EXACT paths shown above. Do not shorten or assume paths.");
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

    /// <summary>
    /// Analyzes JSON structure and returns a human-readable path map.
    /// This helps the LLM understand the exact paths to use in DSL expressions.
    /// </summary>
    private static string AnalyzeJsonStructure(JsonElement element, string currentPath, int depth, int maxDepth = 4)
    {
        if (depth > maxDepth) return "";
        
        var sb = new StringBuilder();
        var indent = new string(' ', depth * 2);
        
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    var propPath = string.IsNullOrEmpty(currentPath) ? prop.Name : $"{currentPath}.{prop.Name}";
                    
                    if (prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        var arrayLen = prop.Value.GetArrayLength();
                        sb.AppendLine($"{indent}• {propPath} : Array[{arrayLen}]  ← use this path to iterate");
                        
                        // Show first element structure if array has items
                        if (arrayLen > 0)
                        {
                            var firstElement = prop.Value[0];
                            if (firstElement.ValueKind == JsonValueKind.Object)
                            {
                                sb.AppendLine($"{indent}  Each item has fields:");
                                foreach (var itemProp in firstElement.EnumerateObject())
                                {
                                    var typeName = GetJsonTypeName(itemProp.Value);
                                    sb.AppendLine($"{indent}    - {itemProp.Name} : {typeName}");
                                }
                            }
                        }
                    }
                    else if (prop.Value.ValueKind == JsonValueKind.Object)
                    {
                        sb.AppendLine($"{indent}• {propPath} : Object");
                        sb.Append(AnalyzeJsonStructure(prop.Value, propPath, depth + 1, maxDepth));
                    }
                    else
                    {
                        var typeName = GetJsonTypeName(prop.Value);
                        sb.AppendLine($"{indent}• {propPath} : {typeName}");
                    }
                }
                break;
                
            case JsonValueKind.Array:
                var len = element.GetArrayLength();
                var pathLabel = string.IsNullOrEmpty(currentPath) ? "(root)" : currentPath;
                sb.AppendLine($"{indent}• {pathLabel} : Array[{len}]  ← root is an array, iterate directly");
                
                if (len > 0 && element[0].ValueKind == JsonValueKind.Object)
                {
                    sb.AppendLine($"{indent}  Each item has fields:");
                    foreach (var itemProp in element[0].EnumerateObject())
                    {
                        var typeName = GetJsonTypeName(itemProp.Value);
                        sb.AppendLine($"{indent}    - {itemProp.Name} : {typeName}");
                    }
                }
                break;
                
            default:
                var rootType = GetJsonTypeName(element);
                sb.AppendLine($"{indent}• (root) : {rootType}");
                break;
        }
        
        return sb.ToString();
    }
    
    private static string GetJsonTypeName(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => "String",
            JsonValueKind.Number => "Number",
            JsonValueKind.True => "Boolean",
            JsonValueKind.False => "Boolean",
            JsonValueKind.Null => "Null",
            JsonValueKind.Array => $"Array[{element.GetArrayLength()}]",
            JsonValueKind.Object => "Object",
            _ => "Unknown"
        };
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
