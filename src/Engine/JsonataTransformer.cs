using System.Text.Json;
using System.Text.RegularExpressions;

namespace Metrics.Engine;

/// <summary>
/// Implementação simplificada de um transformador Jsonata.
/// Para produção, considere usar a biblioteca Jsonata.Net.Native.
/// Esta versão suporta os casos de uso básicos do spec.
/// </summary>
public sealed class JsonataTransformer : IDslTransformer
{
    public JsonElement Transform(JsonElement input, string dslProfile, string dslText)
    {
        if (dslProfile != "jsonata")
            throw new NotSupportedException($"DSL profile '{dslProfile}' is not supported yet. Only 'jsonata' is implemented.");

        // Parse the Jsonata expression
        var result = EvaluateJsonata(input, dslText.Trim());
        return result;
    }

    private JsonElement EvaluateJsonata(JsonElement input, string expression)
    {
        // Simple Jsonata evaluator for basic transformations
        // This is a simplified implementation that handles common patterns like:
        // - hosts.{ "host": name, "cpu": cpu }
        // - $[*].{ "id": id, "value": value }

        // Pattern: array.{ "key": field, ... }
        var arrayMapPattern = @"^(\w+)\.\{\s*(.+)\s*\}$";
        var match = Regex.Match(expression, arrayMapPattern);

        if (match.Success)
        {
            var arrayPath = match.Groups[1].Value;
            var mappingExpr = match.Groups[2].Value;

            // Get the array from input
            if (!input.TryGetProperty(arrayPath, out var arrayElement))
                throw new InvalidOperationException($"Path '{arrayPath}' not found in input");

            if (arrayElement.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException($"Path '{arrayPath}' is not an array");

            // Parse the mapping expression: "key": field, "key2": field2, ...
            var mappings = ParseMappingExpression(mappingExpr);

            // Build result array
            var results = new List<JsonElement>();
            foreach (var item in arrayElement.EnumerateArray())
            {
                var mappedObject = MapObject(item, mappings);
                results.Add(mappedObject);
            }

            // Convert to JsonElement
            return JsonSerializer.SerializeToElement(results);
        }

        throw new NotSupportedException($"Jsonata expression not supported: {expression}");
    }

    private Dictionary<string, string> ParseMappingExpression(string expr)
    {
        var mappings = new Dictionary<string, string>();
        
        // Split by comma, but be careful with nested structures
        var parts = expr.Split(',');
        
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            var colonIndex = trimmed.IndexOf(':');
            
            if (colonIndex > 0)
            {
                var key = trimmed.Substring(0, colonIndex).Trim().Trim('"');
                var field = trimmed.Substring(colonIndex + 1).Trim();
                mappings[key] = field;
            }
        }

        return mappings;
    }

    private JsonElement MapObject(JsonElement sourceObject, Dictionary<string, string> mappings)
    {
        var result = new Dictionary<string, object?>();

        foreach (var (key, field) in mappings)
        {
            if (sourceObject.TryGetProperty(field, out var value))
            {
                result[key] = JsonSerializer.Deserialize<object>(value.GetRawText());
            }
            else
            {
                result[key] = null;
            }
        }

        return JsonSerializer.SerializeToElement(result);
    }
}
