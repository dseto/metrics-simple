using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NJsonSchema;

namespace Metrics.Engine;

public interface ISchemaValidator
{
    (bool IsValid, List<string> Errors) ValidateAgainstSchema(JsonElement data, JsonElement schema);
}

public sealed class SchemaValidator : ISchemaValidator
{
    private readonly ConcurrentDictionary<string, JsonSchema> _schemaCache = new();

    public (bool IsValid, List<string> Errors) ValidateAgainstSchema(JsonElement data, JsonElement schema)
    {
        try
        {
            var schemaJson = schema.GetRawText();

            // Validate schema is self-contained (no external $ref)
            var selfContainedError = ValidateSelfContained(schemaJson);
            if (selfContainedError != null)
            {
                return (false, new List<string> { selfContainedError });
            }

            // Get or parse schema from cache
            var schemaHash = ComputeHash(schemaJson);
            var jsonSchema = _schemaCache.GetOrAdd(schemaHash, _ =>
                JsonSchema.FromJsonAsync(schemaJson).GetAwaiter().GetResult()
            );

            var dataJson = data.GetRawText();
            var validationErrors = jsonSchema.Validate(dataJson);

            if (validationErrors.Count == 0)
                return (true, new List<string>());

            // Deterministic error ordering: by Path then Kind
            var errorMessages = validationErrors
                .OrderBy(e => e.Path, StringComparer.Ordinal)
                .ThenBy(e => e.Kind.ToString(), StringComparer.Ordinal)
                .Select(e => $"{e.Path}: {e.Kind}")
                .ToList();

            return (false, errorMessages);
        }
        catch (Exception ex)
        {
            return (false, new List<string> { $"Schema validation error: {ex.Message}" });
        }
    }

    /// <summary>
    /// Validates that schema is self-contained (no external $ref).
    /// Allows internal $ref like #/definitions/...
    /// Rejects external $ref like file:// or https://
    /// </summary>
    private static string? ValidateSelfContained(string schemaJson)
    {
        // Simple scan for $ref values (pragmatic, not full parsing)
        var lines = schemaJson.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        
        foreach (var line in lines)
        {
            if (line.Contains("\"$ref\""))
            {
                // Extract the value (simple heuristic: value between quotes after $ref)
                var match = System.Text.RegularExpressions.Regex.Match(line, @"""$ref""\s*:\s*""([^""]*)""");
                if (match.Success)
                {
                    var refValue = match.Groups[1].Value;
                    // Allow internal refs (starting with #/), reject external
                    if (!refValue.StartsWith("#/"))
                    {
                        return $"outputSchema must be self-contained (no external $ref). Found: {refValue}";
                    }
                }
            }
        }

        return null;
    }

    private static string ComputeHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
