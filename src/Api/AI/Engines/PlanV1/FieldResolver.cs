using System.Text.Json;

namespace Metrics.Api.AI.Engines.PlanV1;

/// <summary>
/// Resolves field paths using alias mapping (pt-BR to en-US and vice versa).
/// </summary>
public static class FieldResolver
{
    /// <summary>
    /// Bidirectional alias mappings (both directions)
    /// </summary>
    private static readonly Dictionary<string, HashSet<string>> AliasGroups = new(StringComparer.OrdinalIgnoreCase)
    {
        // Name variations
        ["name"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "name", "nome", "nombre" },
        ["nome"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "name", "nome", "nombre" },
        
        // City variations
        ["city"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "city", "cidade", "ciudad" },
        ["cidade"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "city", "cidade", "ciudad" },
        
        // Age variations
        ["age"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "age", "idade", "edad" },
        ["idade"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "age", "idade", "edad" },
        
        // Date variations
        ["date"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "date", "data", "fecha" },
        ["data"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "date", "data", "fecha" },
        
        // Category variations
        ["category"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "category", "categoria", "cat" },
        ["categoria"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "category", "categoria", "cat" },
        
        // Price variations
        ["price"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "price", "preco", "preço", "precio", "valor" },
        ["preco"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "price", "preco", "preço", "precio", "valor" },
        ["preço"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "price", "preco", "preço", "precio", "valor" },
        
        // Quantity variations
        ["quantity"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "quantity", "quantidade", "qty", "qtd", "cantidad" },
        ["quantidade"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "quantity", "quantidade", "qty", "qtd", "cantidad" },
        
        // Description variations
        ["description"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "description", "descricao", "descrição", "desc" },
        ["descricao"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "description", "descricao", "descrição", "desc" },
        
        // Status variations
        ["status"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "status", "estado", "situacao" },
        ["estado"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "status", "estado", "situacao" },
        
        // Total variations
        ["total"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "total", "sum", "soma" },
        ["soma"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "total", "sum", "soma" },
        
        // ID variations
        ["id"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "id", "codigo", "código", "code" },
        ["codigo"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "id", "codigo", "código", "code" },
        
        // Value variations
        ["value"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "value", "valor", "amount" },
        ["valor"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "value", "valor", "amount" },
        
        // Temperature variations
        ["temperature"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "temperature", "temp", "temperatura" },
        ["temp"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "temperature", "temp", "temperatura" },
        ["temperatura"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "temperature", "temp", "temperatura" },
        
        // Max/Min variations
        ["max"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "max", "maximum", "maximo", "máximo" },
        ["min"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "min", "minimum", "minimo", "mínimo" },
    };

    /// <summary>
    /// Result of field resolution
    /// </summary>
    public record ResolveResult(
        string ResolvedPath,
        bool WasResolved,
        string? OriginalField,
        string? ResolvedField,
        List<string> Warnings
    );

    /// <summary>
    /// Resolves a field path using the sample record to find matching fields.
    /// </summary>
    /// <param name="path">The JSON pointer path (e.g., "/nome")</param>
    /// <param name="sampleRecord">A sample record to check field existence</param>
    /// <returns>Resolution result with resolved path and any warnings</returns>
    public static ResolveResult Resolve(string path, JsonElement sampleRecord)
    {
        var warnings = new List<string>();

        if (string.IsNullOrEmpty(path))
        {
            return new ResolveResult(path, false, null, null, warnings);
        }

        // Handle root reference
        if (path == "/")
        {
            return new ResolveResult(path, false, null, null, warnings);
        }

        // Parse path segments
        var segments = ParsePath(path);
        if (segments.Count == 0)
        {
            return new ResolveResult(path, false, null, null, warnings);
        }

        // Try to resolve first segment (field name) against sample record
        var fieldName = segments[0];
        var availableFields = GetAvailableFields(sampleRecord);

        // Check if field exists directly
        if (availableFields.Contains(fieldName))
        {
            return new ResolveResult(path, false, null, null, warnings);
        }

        // Try alias resolution
        var resolved = TryResolveAlias(fieldName, availableFields);
        if (resolved != null)
        {
            // Update first segment with resolved name
            segments[0] = resolved;
            var newPath = "/" + string.Join("/", segments);
            return new ResolveResult(newPath, true, fieldName, resolved, warnings);
        }

        // Try case-insensitive matching
        var caseMatch = availableFields.FirstOrDefault(f =>
            f.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
        if (caseMatch != null && caseMatch != fieldName)
        {
            segments[0] = caseMatch;
            var newPath = "/" + string.Join("/", segments);
            warnings.Add($"Field '{fieldName}' resolved to '{caseMatch}' via case-insensitive match");
            return new ResolveResult(newPath, true, fieldName, caseMatch, warnings);
        }

        // Could not resolve - return original
        warnings.Add($"Field '{fieldName}' not found in sample record. Available: {string.Join(", ", availableFields)}");
        return new ResolveResult(path, false, fieldName, null, warnings);
    }

    /// <summary>
    /// Resolves all fields in a plan step
    /// </summary>
    public static (PlanStep ResolvedStep, List<string> Warnings) ResolveStep(PlanStep step, JsonElement sampleRecord)
    {
        var warnings = new List<string>();
        var resolvedStep = step;

        switch (step.Op)
        {
            case PlanOps.Select:
                if (step.Fields != null)
                {
                    var resolvedFields = new List<FieldSpec>();
                    foreach (var field in step.Fields)
                    {
                        var result = Resolve(field.From, sampleRecord);
                        warnings.AddRange(result.Warnings);
                        resolvedFields.Add(field with { From = result.ResolvedPath });
                    }
                    resolvedStep = step with { Fields = resolvedFields };
                }
                break;

            case PlanOps.Sort:
                if (!string.IsNullOrEmpty(step.By))
                {
                    var result = Resolve(step.By, sampleRecord);
                    warnings.AddRange(result.Warnings);
                    resolvedStep = step with { By = result.ResolvedPath };
                }
                break;

            case PlanOps.GroupBy:
                if (step.Keys != null)
                {
                    var resolvedKeys = new List<string>();
                    foreach (var key in step.Keys)
                    {
                        var result = Resolve(key, sampleRecord);
                        warnings.AddRange(result.Warnings);
                        resolvedKeys.Add(result.ResolvedPath);
                    }
                    resolvedStep = step with { Keys = resolvedKeys };
                }
                break;

            case PlanOps.Aggregate:
                if (step.Metrics != null)
                {
                    var resolvedMetrics = new List<MetricSpec>();
                    foreach (var metric in step.Metrics)
                    {
                        var resolvedMetric = metric;
                        if (!string.IsNullOrEmpty(metric.Field))
                        {
                            var result = Resolve(metric.Field, sampleRecord);
                            warnings.AddRange(result.Warnings);
                            resolvedMetric = metric with { Field = result.ResolvedPath };
                        }
                        resolvedMetrics.Add(resolvedMetric);
                    }
                    resolvedStep = step with { Metrics = resolvedMetrics };
                }
                break;
        }

        return (resolvedStep, warnings);
    }

    private static string? TryResolveAlias(string fieldName, HashSet<string> availableFields)
    {
        if (!AliasGroups.TryGetValue(fieldName, out var aliases))
        {
            return null;
        }

        // Find first available alias
        foreach (var alias in aliases)
        {
            if (availableFields.Contains(alias))
            {
                return alias;
            }
        }

        // Try case-insensitive match on aliases
        foreach (var alias in aliases)
        {
            var match = availableFields.FirstOrDefault(f =>
                f.Equals(alias, StringComparison.OrdinalIgnoreCase));
            if (match != null)
            {
                return match;
            }
        }

        return null;
    }

    private static List<string> ParsePath(string path)
    {
        // Handle JSON pointer format
        if (path.StartsWith("/"))
        {
            path = path[1..];
        }

        if (string.IsNullOrEmpty(path))
        {
            return new List<string>();
        }

        return path.Split('/').ToList();
    }

    private static HashSet<string> GetAvailableFields(JsonElement element)
    {
        var fields = new HashSet<string>(StringComparer.Ordinal);

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in element.EnumerateObject())
            {
                fields.Add(prop.Name);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array && element.GetArrayLength() > 0)
        {
            // Check first item if it's an object
            var first = element.EnumerateArray().FirstOrDefault();
            if (first.ValueKind == JsonValueKind.Object)
            {
                foreach (var prop in first.EnumerateObject())
                {
                    fields.Add(prop.Name);
                }
            }
        }

        return fields;
    }
}
