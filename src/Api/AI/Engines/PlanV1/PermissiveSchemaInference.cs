using System.Text.Json;

namespace Metrics.Api.AI.Engines.PlanV1;

/// <summary>
/// Infers a permissive JSON schema from data rows.
/// - No required fields
/// - additionalProperties: true
/// - Merges types across rows
/// </summary>
public static class PermissiveSchemaInference
{
    /// <summary>
    /// Infers a permissive schema from rows
    /// </summary>
    public static JsonElement InferSchema(List<Dictionary<string, JsonElement>> rows)
    {
        if (rows.Count == 0)
        {
            return CreateEmptyArraySchema();
        }

        // Collect all properties and their types across all rows
        var propertyTypes = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            foreach (var kvp in row)
            {
                if (!propertyTypes.ContainsKey(kvp.Key))
                {
                    propertyTypes[kvp.Key] = new HashSet<string>();
                }

                var jsonType = GetJsonType(kvp.Value);
                propertyTypes[kvp.Key].Add(jsonType);
            }
        }

        // Build schema
        var schema = new Dictionary<string, object>
        {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["type"] = "array",
            ["items"] = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["additionalProperties"] = true,
                ["properties"] = BuildProperties(propertyTypes)
            }
        };

        return JsonSerializer.SerializeToElement(schema);
    }

    /// <summary>
    /// Infers schema from a JsonElement array
    /// </summary>
    public static JsonElement InferSchemaFromElement(JsonElement array)
    {
        if (array.ValueKind != JsonValueKind.Array)
        {
            return CreateEmptyArraySchema();
        }

        var normalizeResult = ShapeNormalizer.Normalize(array);
        if (!normalizeResult.Success || normalizeResult.Rows == null)
        {
            return CreateEmptyArraySchema();
        }

        return InferSchema(normalizeResult.Rows);
    }

    private static Dictionary<string, object> BuildProperties(Dictionary<string, HashSet<string>> propertyTypes)
    {
        var properties = new Dictionary<string, object>();

        foreach (var kvp in propertyTypes)
        {
            var types = kvp.Value.ToList();

            if (types.Count == 1)
            {
                properties[kvp.Key] = new Dictionary<string, object>
                {
                    ["type"] = types[0]
                };
            }
            else if (types.Count > 1)
            {
                // Multiple types - use array of types
                // Filter out "null" and add it separately if present
                var hasNull = types.Contains("null");
                var nonNullTypes = types.Where(t => t != "null").ToList();

                if (nonNullTypes.Count == 1 && hasNull)
                {
                    properties[kvp.Key] = new Dictionary<string, object>
                    {
                        ["type"] = new[] { nonNullTypes[0], "null" }
                    };
                }
                else
                {
                    properties[kvp.Key] = new Dictionary<string, object>
                    {
                        ["type"] = types.ToArray()
                    };
                }
            }
        }

        return properties;
    }

    private static string GetJsonType(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => "string",
            JsonValueKind.Number => "number",
            JsonValueKind.True => "boolean",
            JsonValueKind.False => "boolean",
            JsonValueKind.Array => "array",
            JsonValueKind.Object => "object",
            JsonValueKind.Null => "null",
            JsonValueKind.Undefined => "null",
            _ => "string"
        };
    }

    private static JsonElement CreateEmptyArraySchema()
    {
        var schema = new Dictionary<string, object>
        {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["type"] = "array",
            ["items"] = new Dictionary<string, object>
            {
                ["type"] = "object",
                ["additionalProperties"] = true
            }
        };

        return JsonSerializer.SerializeToElement(schema);
    }
}
