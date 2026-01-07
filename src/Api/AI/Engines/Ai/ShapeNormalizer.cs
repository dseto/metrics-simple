using System.Text.Json;

namespace Metrics.Api.AI.Engines.Ai;

/// <summary>
/// Normalizes output shape to array&lt;object&gt; as required by the spec.
/// </summary>
public static class ShapeNormalizer
{
    /// <summary>
    /// Result of shape normalization
    /// </summary>
    public record NormalizeResult(
        bool Success,
        List<Dictionary<string, JsonElement>>? Rows,
        string? Error
    );

    /// <summary>
    /// Normalizes any JSON element to an array of objects (rows).
    /// </summary>
    /// <param name="input">The JSON element to normalize</param>
    /// <returns>Normalized rows or error</returns>
    public static NormalizeResult Normalize(JsonElement input)
    {
        try
        {
            return input.ValueKind switch
            {
                JsonValueKind.Array => NormalizeArray(input),
                JsonValueKind.Object => NormalizeObject(input),
                JsonValueKind.Null => new NormalizeResult(true, new List<Dictionary<string, JsonElement>>(), null),
                JsonValueKind.Undefined => new NormalizeResult(true, new List<Dictionary<string, JsonElement>>(), null),
                _ => new NormalizeResult(false, null, $"WrongShape: Cannot normalize {input.ValueKind} to array<object>. Expected array, object, or null.")
            };
        }
        catch (Exception ex)
        {
            return new NormalizeResult(false, null, $"WrongShape: {ex.Message}");
        }
    }

    /// <summary>
    /// Normalizes an array to rows
    /// </summary>
    private static NormalizeResult NormalizeArray(JsonElement array)
    {
        var rows = new List<Dictionary<string, JsonElement>>();

        foreach (var item in array.EnumerateArray())
        {
            var row = NormalizeItem(item);
            if (row != null)
            {
                rows.Add(row);
            }
        }

        return new NormalizeResult(true, rows, null);
    }

    /// <summary>
    /// Normalizes a single object to one row
    /// </summary>
    private static NormalizeResult NormalizeObject(JsonElement obj)
    {
        var row = new Dictionary<string, JsonElement>();

        foreach (var prop in obj.EnumerateObject())
        {
            row[prop.Name] = prop.Value.Clone();
        }

        return new NormalizeResult(true, new List<Dictionary<string, JsonElement>> { row }, null);
    }

    /// <summary>
    /// Normalizes a single array item to a row
    /// </summary>
    private static Dictionary<string, JsonElement>? NormalizeItem(JsonElement item)
    {
        // Object => use as row
        if (item.ValueKind == JsonValueKind.Object)
        {
            var row = new Dictionary<string, JsonElement>();
            foreach (var prop in item.EnumerateObject())
            {
                row[prop.Name] = prop.Value.Clone();
            }
            return row;
        }

        // Primitive => wrap in object with "value" key
        if (item.ValueKind != JsonValueKind.Null && item.ValueKind != JsonValueKind.Undefined)
        {
            return new Dictionary<string, JsonElement>
            {
                ["value"] = item.Clone()
            };
        }

        // Null/undefined => skip
        return null;
    }

    /// <summary>
    /// Extracts rows from a JSON element using a JSON pointer path.
    /// </summary>
    /// <param name="input">The input JSON</param>
    /// <param name="recordPath">JSON pointer path (e.g., "/items", "/")</param>
    /// <returns>Extracted rows or error</returns>
    public static NormalizeResult ExtractAndNormalize(JsonElement input, string recordPath)
    {
        try
        {
            var target = NavigateToPath(input, recordPath);
            if (target == null)
            {
                return new NormalizeResult(false, null, $"WrongShape: Path '{recordPath}' not found in input");
            }

            return Normalize(target.Value);
        }
        catch (Exception ex)
        {
            return new NormalizeResult(false, null, $"WrongShape: Failed to extract from path '{recordPath}': {ex.Message}");
        }
    }

    /// <summary>
    /// Navigates to a JSON pointer path
    /// </summary>
    public static JsonElement? NavigateToPath(JsonElement element, string path)
    {
        if (string.IsNullOrEmpty(path) || path == "/")
        {
            return element;
        }

        // Parse JSON pointer
        var segments = path.TrimStart('/').Split('/');
        var current = element;

        foreach (var segment in segments)
        {
            if (string.IsNullOrEmpty(segment)) continue;

            if (current.ValueKind == JsonValueKind.Object)
            {
                if (!current.TryGetProperty(segment, out var next))
                {
                    return null;
                }
                current = next;
            }
            else if (current.ValueKind == JsonValueKind.Array)
            {
                if (!int.TryParse(segment, out var index))
                {
                    return null;
                }
                if (index < 0 || index >= current.GetArrayLength())
                {
                    return null;
                }
                current = current[index];
            }
            else
            {
                return null;
            }
        }

        return current;
    }

    /// <summary>
    /// Converts rows back to a JsonElement array
    /// </summary>
    public static JsonElement ToJsonElement(List<Dictionary<string, JsonElement>> rows)
    {
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        writer.WriteStartArray();
        foreach (var row in rows)
        {
            writer.WriteStartObject();
            foreach (var kvp in row)
            {
                writer.WritePropertyName(kvp.Key);
                kvp.Value.WriteTo(writer);
            }
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
        writer.Flush();

        stream.Position = 0;
        using var doc = JsonDocument.Parse(stream);
        return doc.RootElement.Clone();
    }
}
