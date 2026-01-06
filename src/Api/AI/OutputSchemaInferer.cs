using System.Text.Json;

namespace Metrics.Api.AI;

/// <summary>
/// Infers JSON Schema from actual transformation output.
/// This replaces unreliable LLM-generated schemas with server-side determination.
/// </summary>
public static class OutputSchemaInferer
{
    /// <summary>
    /// Infer JSON Schema from a sample output.
    /// The schema describes the structure of rows that will be generated.
    /// </summary>
    public static JsonElement InferSchema(JsonElement sampleOutput)
    {
        if (sampleOutput.ValueKind == JsonValueKind.Null)
        {
            return GetEmptySchema();
        }

        // If output is an array, infer array schema (not just from first element)
        if (sampleOutput.ValueKind == JsonValueKind.Array)
        {
            if (sampleOutput.GetArrayLength() > 0)
            {
                var firstItem = sampleOutput[0];
                // Create array schema with item schema from first element
                var itemSchema = InferFromValue(firstItem);
                var arraySchema = new
                {
                    type = "array",
                    items = itemSchema
                };
                return JsonSerializer.SerializeToElement(arraySchema);
            }
            else
            {
                // Empty array
                var emptyArraySchema = new
                {
                    type = "array",
                    items = new { type = "object" }
                };
                return JsonSerializer.SerializeToElement(emptyArraySchema);
            }
        }

        // If output is an object, infer from it
        if (sampleOutput.ValueKind == JsonValueKind.Object)
        {
            return InferFromValue(sampleOutput);
        }

        // Primitive value
        return GetSchemaForType(sampleOutput.ValueKind);
    }

    /// <summary>
    /// Infer schema from a single value (object or primitive).
    /// </summary>
    private static JsonElement InferFromValue(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            return InferObjectSchema(value);
        }

        return GetSchemaForType(value.ValueKind);
    }

    /// <summary>
    /// Infer object schema by examining properties.
    /// </summary>
    private static JsonElement InferObjectSchema(JsonElement obj)
    {
        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var prop in obj.EnumerateObject())
        {
            var fieldName = prop.Name;
            var fieldValue = prop.Value;

            properties[fieldName] = InferPropertySchema(fieldValue);
            required.Add(fieldName);
        }

        var schema = new
        {
            type = "object",
            properties,
            required = required.Count > 0 ? required : null
        };

        return JsonSerializer.SerializeToElement(schema);
    }

    /// <summary>
    /// Infer schema for a property value.
    /// </summary>
    private static object InferPropertySchema(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.Object => new { type = "object", additionalProperties = true },
            JsonValueKind.Array => InferArraySchema(value),
            JsonValueKind.String => new { type = "string" },
            JsonValueKind.Number => InferNumberType(value),
            JsonValueKind.True or JsonValueKind.False => new { type = "boolean" },
            JsonValueKind.Null => new { type = "null" },
            _ => new { type = "string" }
        };
    }

    /// <summary>
    /// Infer if a number is integer or float.
    /// </summary>
    private static object InferNumberType(JsonElement value)
    {
        if (value.TryGetInt64(out _))
        {
            return new { type = "integer" };
        }
        return new { type = "number" };
    }

    /// <summary>
    /// Infer schema from array items.
    /// </summary>
    private static object InferArraySchema(JsonElement array)
    {
        if (array.GetArrayLength() == 0)
        {
            return new { type = "array", items = new { type = "null" } };
        }

        var firstItem = array[0];
        var itemSchema = InferPropertySchema(firstItem);
        return new { type = "array", items = itemSchema };
    }

    /// <summary>
    /// Get schema for a primitive JSON type.
    /// </summary>
    private static JsonElement GetSchemaForType(JsonValueKind kind)
    {
        var schema = kind switch
        {
            JsonValueKind.String => new { type = "string" },
            JsonValueKind.Number => new { type = "number" },
            JsonValueKind.True or JsonValueKind.False => new { type = "boolean" },
            JsonValueKind.Array => new { type = "array" },
            JsonValueKind.Object => new { type = "object" },
            JsonValueKind.Null => new { type = "null" },
            _ => new { type = "null" }
        };

        return JsonSerializer.SerializeToElement(schema);
    }

    /// <summary>
    /// Get empty schema (when no data available).
    /// </summary>
    private static JsonElement GetEmptySchema()
    {
        var schema = new
        {
            type = "object",
            properties = new Dictionary<string, object>(),
            required = new List<string>()
        };
        return JsonSerializer.SerializeToElement(schema);
    }
}
