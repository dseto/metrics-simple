using System.Text.Json;

namespace Metrics.Api.AI;

/// <summary>
/// Utility for resilient JSON parsing from LLM responses.
/// Handles malformed JSON, markdown code blocks, and partial responses.
/// </summary>
public static class LlmResponseParser
{
    /// <summary>
    /// Parse JSON response from LLM with multiple fallback strategies.
    /// </summary>
    public static (bool Success, JsonElement? Parsed, string? ErrorCategory, string? ErrorDetails) TryParseJsonResponse(
        string responseContent)
    {
        if (string.IsNullOrWhiteSpace(responseContent))
            return (false, null, "LLM_RESPONSE_EMPTY", "LLM returned empty response");

        // Strategy 1: Direct parse
        var result = TryDirectParse(responseContent);
        if (result.Success)
            return result;

        // Strategy 2: Remove markdown code blocks
        var cleaned = RemoveMarkdownCodeBlocks(responseContent);
        if (cleaned != responseContent)
        {
            result = TryDirectParse(cleaned);
            if (result.Success)
                return result;
        }

        // Strategy 3: Extract JSON from first { to last }
        var extracted = ExtractJsonObject(responseContent);
        if (extracted != null)
        {
            result = TryDirectParse(extracted);
            if (result.Success)
                return result;
        }

        // All strategies failed
        return (false, null, "LLM_RESPONSE_NOT_JSON", 
            $"Failed to parse JSON after 3 strategies. Raw length: {responseContent.Length}");
    }

    /// <summary>
    /// Attempt direct JSON parse.
    /// </summary>
    private static (bool Success, JsonElement? Parsed, string? ErrorCategory, string? ErrorDetails) 
        TryDirectParse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return (true, doc.RootElement.Clone(), null, null);
        }
        catch (JsonException ex)
        {
            return (false, null, "PARSE_ERROR", ex.Message);
        }
        catch (Exception ex)
        {
            return (false, null, "PARSE_EXCEPTION", ex.GetType().Name);
        }
    }

    /// <summary>
    /// Remove common markdown code block markers: ```json, ```python, ```, etc.
    /// </summary>
    private static string RemoveMarkdownCodeBlocks(string content)
    {
        var cleaned = content
            .Replace("```json", "")
            .Replace("```python", "")
            .Replace("```", "")
            .Trim();
        return cleaned;
    }

    /// <summary>
    /// Extract JSON object from position of first '{' to last '}'.
    /// Handles cases where LLM outputs text before/after JSON.
    /// </summary>
    private static string? ExtractJsonObject(string content)
    {
        var firstBrace = content.IndexOf('{');
        var lastBrace = content.LastIndexOf('}');

        if (firstBrace < 0 || lastBrace < 0 || firstBrace >= lastBrace)
            return null;

        return content.Substring(firstBrace, lastBrace - firstBrace + 1);
    }

    /// <summary>
    /// Normalize DSL text for comparison (used in retry detection).
    /// Removes whitespace, comments, and formatting variations.
    /// </summary>
    public static string NormalizeDslForComparison(string dslText)
    {
        if (string.IsNullOrEmpty(dslText))
            return "";

        // Remove all whitespace
        var normalized = System.Text.RegularExpressions.Regex.Replace(dslText, @"\s+", "");
        
        // Remove common Jsonata comments if any
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\/\*.*?\*\/", "");
        normalized = System.Text.RegularExpressions.Regex.Replace(normalized, @"\/\/.*$", "", 
            System.Text.RegularExpressions.RegexOptions.Multiline);

        return normalized.ToLowerInvariant();
    }
}
