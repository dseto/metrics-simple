using System.Text.Json;

namespace Metrics.Engine;

public sealed record TransformResult(bool IsValid, JsonElement? OutputJson, IReadOnlyList<string> Errors, string? CsvPreview);

public sealed class EngineService
{
    private readonly ISchemaValidator _schemaValidator;
    private readonly ICsvGenerator _csvGenerator;

    public EngineService(ISchemaValidator schemaValidator, ICsvGenerator csvGenerator)
    {
        _schemaValidator = schemaValidator;
        _csvGenerator = csvGenerator;
    }

    public TransformResult TransformValidateToCsvFromRows(JsonElement rowsArray, JsonElement outputSchema)
    {
        try
        {
            // rowsArray is expected to be a normalized array of objects

            // Step 1: Validate normalized output against schema (skip if schema is empty {})
            if (outputSchema.GetRawText() != "{}")
            {
                var (isValid, errors) = _schemaValidator.ValidateAgainstSchema(rowsArray, outputSchema);
                if (!isValid)
                {
                    return new TransformResult(false, null, errors, null);
                }
            }

            // Step 2: Resolve column order from outputSchema (per csv-format.md)
            var columns = ResolveColumns(rowsArray, outputSchema);

            // Step 3: Generate deterministic CSV with proper columns
            var csvPreview = _csvGenerator.GenerateCsv(rowsArray, columns);

            return new TransformResult(true, rowsArray, Array.Empty<string>(), csvPreview);
        }
        catch (Exception ex)
        {
            return new TransformResult(false, null, new[] { ex.Message }, null);
        }
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
