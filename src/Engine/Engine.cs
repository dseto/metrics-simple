using System.Text.Json;

namespace Metrics.Engine;

public sealed record TransformResult(bool IsValid, JsonElement? OutputJson, IReadOnlyList<string> Errors, string? CsvPreview);

public interface IDslTransformer
{
    JsonElement Transform(JsonElement input, string dslProfile, string dslText);
}

public sealed class EngineService
{
    private readonly IDslTransformer _transformer;
    private readonly ISchemaValidator _schemaValidator;
    private readonly ICsvGenerator _csvGenerator;

    public EngineService(IDslTransformer transformer, ISchemaValidator schemaValidator, ICsvGenerator csvGenerator)
    {
        _transformer = transformer;
        _schemaValidator = schemaValidator;
        _csvGenerator = csvGenerator;
    }

    public TransformResult TransformValidateToCsv(JsonElement input, string dslProfile, string dslText, JsonElement outputSchema)
    {
        try
        {
            // Step 1: Execute DSL transformation
            var output = _transformer.Transform(input, dslProfile, dslText);

            // Step 2: Normalize output to rows array (per dsl-engine.md)
            var rows = NormalizeRowsToArray(output);

            // Step 3: Validate normalized output against schema
            var (isValid, errors) = _schemaValidator.ValidateAgainstSchema(rows, outputSchema);
            if (!isValid)
            {
                return new TransformResult(false, null, errors, null);
            }

            // Step 4: Resolve column order from outputSchema (per csv-format.md)
            var columns = ResolveColumns(rows, outputSchema);

            // Step 5: Generate deterministic CSV with proper columns
            var csvPreview = _csvGenerator.GenerateCsv(rows, columns);

            return new TransformResult(true, rows, Array.Empty<string>(), csvPreview);
        }
        catch (Exception ex)
        {
            return new TransformResult(false, null, new[] { ex.Message }, null);
        }
    }

    /// <summary>
    /// Normalizes output to an array of objects (rows).
    /// - array => return as is
    /// - object => wrap in [object]
    /// - null => []
    /// - others => throw TRANSFORM_FAILED
    /// </summary>
    private static JsonElement NormalizeRowsToArray(JsonElement output)
    {
        return output.ValueKind switch
        {
            JsonValueKind.Array => output,
            JsonValueKind.Object => 
                JsonDocument.Parse($"[{output.GetRawText()}]").RootElement.Clone(),
            JsonValueKind.Null => 
                JsonDocument.Parse("[]").RootElement.Clone(),
            _ => throw new InvalidOperationException(
                $"TRANSFORM_FAILED: Jsonata output must be array/object/null, got {output.ValueKind}")
        };
    }

    /// <summary>
    /// Resolves column order from outputSchema.
    /// Priority:
    /// 1. If outputSchema is array with items.properties => use items.properties order
    /// 2. If outputSchema is object with properties => use properties order
    /// 3. Fallback: union of all keys in rows + alphabetical sort (StringComparer.Ordinal)
    /// </summary>
    private static IReadOnlyList<string> ResolveColumns(JsonElement rows, JsonElement outputSchema)
    {
        // Try to extract column order from outputSchema
        if (outputSchema.TryGetProperty("type", out var typeElement) && 
            typeElement.GetString() == "array")
        {
            // Array type: look at items.properties
            if (outputSchema.TryGetProperty("items", out var itemsElement) &&
                itemsElement.TryGetProperty("type", out var itemsTypeElement) &&
                itemsTypeElement.GetString() == "object" &&
                itemsElement.TryGetProperty("properties", out var propsElement))
            {
                var columns = propsElement.EnumerateObject()
                    .Select(p => p.Name)
                    .ToList();
                if (columns.Count > 0)
                    return columns;
            }
        }
        else if (outputSchema.TryGetProperty("type", out var objTypeElement) &&
                 objTypeElement.GetString() == "object" &&
                 outputSchema.TryGetProperty("properties", out var objPropsElement))
        {
            // Object type: use properties order
            var columns = objPropsElement.EnumerateObject()
                .Select(p => p.Name)
                .ToList();
            if (columns.Count > 0)
                return columns;
        }

        // Fallback: gather all keys from rows and sort deterministically
        var allKeys = new HashSet<string>();
        foreach (var row in rows.EnumerateArray())
        {
            if (row.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in row.EnumerateObject())
                {
                    allKeys.Add(prop.Name);
                }
            }
        }

        return allKeys.OrderBy(k => k, StringComparer.Ordinal).ToList();
    }
}
