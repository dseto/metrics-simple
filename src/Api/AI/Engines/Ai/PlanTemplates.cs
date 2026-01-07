using System.Text.Json;

namespace Metrics.Api.AI.Engines.Ai;

/// <summary>
/// Template-based plan generation for common patterns (T1, T2, T5).
/// Used as fallback when LLM fails or for simple deterministic cases.
/// </summary>
public static class PlanTemplates
{
    /// <summary>
    /// Template type identifiers
    /// </summary>
    public static class Templates
    {
        public const string T1_SelectAll = "T1";      // Simple extraction (select all fields)
        public const string T2_SelectFields = "T2";   // Select specific fields
        public const string T5_GroupAggregate = "T5"; // Group by + aggregate
    }

    /// <summary>
    /// Result of template matching
    /// </summary>
    public record TemplateMatchResult(
        bool Matched,
        string? TemplateId,
        TransformPlan? Plan,
        string? Reason
    );

    /// <summary>
    /// Tries to match goal text to a template and generate a plan.
    /// Returns null if no template matches.
    /// </summary>
    public static TemplateMatchResult TryMatchAndGenerate(
        string goalText,
        JsonElement sampleInput,
        string recordPath,
        Serilog.ILogger? logger = null)
    {
        var goal = goalText.ToLowerInvariant();

        // Try T5 (groupBy + aggregate) first - most specific
        var t5Match = TryMatchT5(goal, sampleInput, recordPath, logger);
        if (t5Match.Matched)
            return t5Match;

        // Try T2 (select specific fields)
        var t2Match = TryMatchT2(goal, sampleInput, recordPath, logger);
        if (t2Match.Matched)
            return t2Match;

        // Fallback to T1 (select all)
        var t1Match = TryMatchT1(goal, sampleInput, recordPath, logger);
        if (t1Match.Matched)
            return t1Match;

        return new TemplateMatchResult(false, null, null, "No template matched the goal");
    }

    /// <summary>
    /// T1: Simple extraction - select all fields
    /// Matches: "extract all", "show all", "list all", "get all", or very generic goals
    /// </summary>
    private static TemplateMatchResult TryMatchT1(
        string goal,
        JsonElement sampleInput,
        string recordPath,
        Serilog.ILogger? logger)
    {
        // T1 is the fallback - always matches if we have a valid sample
        var sampleRecord = GetSampleRecord(sampleInput, recordPath);
        if (sampleRecord == null || sampleRecord.Value.ValueKind != JsonValueKind.Object)
        {
            return new TemplateMatchResult(false, null, null, "No valid sample record found");
        }

        var fields = ExtractFieldSpecs(sampleRecord.Value);
        if (fields.Count == 0)
        {
            return new TemplateMatchResult(false, null, null, "Sample record has no fields");
        }

        var plan = new TransformPlan
        {
            PlanVersion = "1.0",
            Source = new PlanSource { RecordPath = recordPath },
            Steps = new List<PlanStep>
            {
                new PlanStep
                {
                    Op = PlanOps.Select,
                    Fields = fields
                }
            }
        };

        logger?.Debug("Template T1 matched: SelectAll with {FieldCount} fields", fields.Count);
        return new TemplateMatchResult(true, Templates.T1_SelectAll, plan, $"Select all {fields.Count} fields");
    }

    /// <summary>
    /// T2: Select specific fields based on goal mentions
    /// Matches: goal that explicitly mentions field names
    /// </summary>
    private static TemplateMatchResult TryMatchT2(
        string goal,
        JsonElement sampleInput,
        string recordPath,
        Serilog.ILogger? logger)
    {
        var sampleRecord = GetSampleRecord(sampleInput, recordPath);
        if (sampleRecord == null || sampleRecord.Value.ValueKind != JsonValueKind.Object)
        {
            return new TemplateMatchResult(false, null, null, "No valid sample record found");
        }

        // Extract fields mentioned in goal
        var mentionedFields = new List<FieldSpec>();
        var availableFields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var prop in sampleRecord.Value.EnumerateObject())
        {
            availableFields[prop.Name] = prop.Name;
            // Also check for the field name in goal (case-insensitive)
            availableFields[prop.Name.ToLowerInvariant()] = prop.Name;
        }

        // Check which fields are mentioned in goal
        foreach (var kvp in availableFields)
        {
            if (goal.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
            {
                // Avoid duplicates
                if (!mentionedFields.Any(f => f.From == $"/{kvp.Value}"))
                {
                    mentionedFields.Add(new FieldSpec
                    {
                        From = $"/{kvp.Value}",
                        As = kvp.Value
                    });
                }
            }
        }

        // Need at least 2 fields to be a T2 match
        if (mentionedFields.Count < 2)
        {
            return new TemplateMatchResult(false, null, null, "Not enough fields mentioned in goal");
        }

        var plan = new TransformPlan
        {
            PlanVersion = "1.0",
            Source = new PlanSource { RecordPath = recordPath },
            Steps = new List<PlanStep>
            {
                new PlanStep
                {
                    Op = PlanOps.Select,
                    Fields = mentionedFields
                }
            }
        };

        logger?.Debug("Template T2 matched: SelectFields with {FieldCount} fields", mentionedFields.Count);
        return new TemplateMatchResult(true, Templates.T2_SelectFields, plan, $"Select {mentionedFields.Count} mentioned fields");
    }

    /// <summary>
    /// T5: Group by + aggregate
    /// Matches: goal with "group", "aggregate", "sum", "total", "count by", "average"
    /// </summary>
    private static TemplateMatchResult TryMatchT5(
        string goal,
        JsonElement sampleInput,
        string recordPath,
        Serilog.ILogger? logger)
    {
        // Check for aggregation keywords
        var hasGroupKeyword = goal.Contains("group") || goal.Contains("agrupar");
        var hasSumKeyword = goal.Contains("sum") || goal.Contains("total") || goal.Contains("soma") || goal.Contains("somar");
        var hasCountKeyword = goal.Contains("count") || goal.Contains("contar") || goal.Contains("quantidade");
        var hasAvgKeyword = goal.Contains("average") || goal.Contains("avg") || goal.Contains("média") || goal.Contains("media");

        if (!hasGroupKeyword && !hasSumKeyword && !hasCountKeyword && !hasAvgKeyword)
        {
            return new TemplateMatchResult(false, null, null, "No aggregation keywords found");
        }

        var sampleRecord = GetSampleRecord(sampleInput, recordPath);
        if (sampleRecord == null || sampleRecord.Value.ValueKind != JsonValueKind.Object)
        {
            return new TemplateMatchResult(false, null, null, "No valid sample record found");
        }

        // Find candidate group key (usually string/category fields)
        string? groupKey = null;
        string? numericField = null;

        foreach (var prop in sampleRecord.Value.EnumerateObject())
        {
            var name = prop.Name.ToLowerInvariant();

            // Look for group key - typically category, type, status, name-like fields
            if (groupKey == null &&
                (name.Contains("category") || name.Contains("categoria") ||
                 name.Contains("type") || name.Contains("tipo") ||
                 name.Contains("status") || name.Contains("group") ||
                 name.Contains("name") || name.Contains("nome")))
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    groupKey = prop.Name;
                }
            }

            // Look for numeric field to aggregate
            if (numericField == null && prop.Value.ValueKind == JsonValueKind.Number)
            {
                var fieldName = prop.Name.ToLowerInvariant();
                if (fieldName.Contains("price") || fieldName.Contains("preco") ||
                    fieldName.Contains("amount") || fieldName.Contains("valor") ||
                    fieldName.Contains("quantity") || fieldName.Contains("quantidade") ||
                    fieldName.Contains("total") || fieldName.Contains("value"))
                {
                    numericField = prop.Name;
                }
            }
        }

        // If no group key found, try first string field
        if (groupKey == null)
        {
            foreach (var prop in sampleRecord.Value.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                {
                    groupKey = prop.Name;
                    break;
                }
            }
        }

        // If no numeric field found, try first number field
        if (numericField == null)
        {
            foreach (var prop in sampleRecord.Value.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Number)
                {
                    numericField = prop.Name;
                    break;
                }
            }
        }

        if (groupKey == null)
        {
            return new TemplateMatchResult(false, null, null, "No suitable group key field found");
        }

        // Build aggregation metrics
        var metrics = new List<MetricSpec>();

        if (hasCountKeyword || (!hasSumKeyword && !hasAvgKeyword))
        {
            metrics.Add(new MetricSpec { As = "count", Fn = AggregateFunctions.Count });
        }

        if ((hasSumKeyword || hasGroupKeyword) && numericField != null)
        {
            metrics.Add(new MetricSpec { As = "total", Fn = AggregateFunctions.Sum, Field = $"/{numericField}" });
        }

        if (hasAvgKeyword && numericField != null)
        {
            metrics.Add(new MetricSpec { As = "average", Fn = AggregateFunctions.Avg, Field = $"/{numericField}" });
        }

        if (metrics.Count == 0)
        {
            metrics.Add(new MetricSpec { As = "count", Fn = AggregateFunctions.Count });
        }

        var plan = new TransformPlan
        {
            PlanVersion = "1.0",
            Source = new PlanSource { RecordPath = recordPath },
            Steps = new List<PlanStep>
            {
                new PlanStep
                {
                    Op = PlanOps.GroupBy,
                    Keys = new List<string> { $"/{groupKey}" }
                },
                new PlanStep
                {
                    Op = PlanOps.Aggregate,
                    Metrics = metrics
                }
            }
        };

        logger?.Debug("Template T5 matched: GroupAggregate with key={Key}, metrics={MetricCount}", groupKey, metrics.Count);
        return new TemplateMatchResult(true, Templates.T5_GroupAggregate, plan, $"Group by {groupKey} with {metrics.Count} metrics");
    }

    /// <summary>
    /// Gets a sample record from the input for field analysis
    /// </summary>
    private static JsonElement? GetSampleRecord(JsonElement input, string recordPath)
    {
        var array = ShapeNormalizer.NavigateToPath(input, recordPath);
        if (array == null || array.Value.ValueKind != JsonValueKind.Array)
            return null;

        if (array.Value.GetArrayLength() == 0)
            return null;

        return array.Value.EnumerateArray().First();
    }

    /// <summary>
    /// Extracts FieldSpec list from a sample record
    /// </summary>
    private static List<FieldSpec> ExtractFieldSpecs(JsonElement sampleRecord)
    {
        var fields = new List<FieldSpec>();

        if (sampleRecord.ValueKind != JsonValueKind.Object)
            return fields;

        foreach (var prop in sampleRecord.EnumerateObject())
        {
            // Skip complex nested objects/arrays for simple extraction
            if (prop.Value.ValueKind == JsonValueKind.Object || prop.Value.ValueKind == JsonValueKind.Array)
                continue;

            fields.Add(new FieldSpec
            {
                From = $"/{prop.Name}",
                As = prop.Name,
                TypeHint = GetTypeHint(prop.Value)
            });
        }

        return fields;
    }

    private static string GetTypeHint(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => "string",
            JsonValueKind.Number => "number",
            JsonValueKind.True => "boolean",
            JsonValueKind.False => "boolean",
            JsonValueKind.Null => "null",
            _ => "unknown"
        };
    }
}
