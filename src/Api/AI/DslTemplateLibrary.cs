using System.Text.Json;

namespace Metrics.Api.AI;

/// <summary>
/// Pre-built DSL templates for common transformation patterns.
/// Used as fallback when LLM-generated DSL is invalid.
/// </summary>
public static class DslTemplateLibrary
{
    /// <summary>
    /// Template 1: Extract + Rename
    /// Maps input fields to output fields with optional renaming.
    /// Example: Extract { name, email } from users
    /// </summary>
    public static string Template1_ExtractRename(string sourcePath, Dictionary<string, string?> fieldMappings)
    {
        // sourcePath: "" (for root array), "users", "users[0]" etc
        // fieldMappings: {"firstName": "first_name", "email": null} (null = no rename)
        
        // Build single-line object constructor for proper Jsonata syntax
        var fieldPairs = fieldMappings.Select(kvp =>
        {
            var sourceField = kvp.Key;
            var targetField = kvp.Value ?? kvp.Key;
            return $"\"{targetField}\": {sourceField}";
        });
        
        var objectConstructor = "{" + string.Join(", ", fieldPairs) + "}";
        
        // In Jsonata, to iterate over an array and transform each item, use .[...] syntax
        // .[{field: field}] means: iterate over array items, apply object constructor to each
        if (string.IsNullOrEmpty(sourcePath))
        {
            // For array at root: use .[ ] array comprehension for iteration
            return $".[{objectConstructor}]";
        }
        else
        {
            // Normal path: apply iteration to sourcePath
            return $"{sourcePath}.[{objectConstructor}]";
        }
    }

    /// <summary>
    /// Template 5: Group + Sum/Average
    /// Groups data by a field and aggregates numeric fields.
    /// Example: Group sales by category, sum by quantity and revenue
    /// </summary>
    public static string Template5_GroupAggregate(
        string sourcePath,
        string groupByField,
        List<(string field, string aggregation)> aggregations)
    {
        // sourcePath: "sales" or "data.items"
        // groupByField: "category"
        // aggregations: [("quantity", "sum"), ("price", "average")]
        
        var aggregationDsl = aggregations.Select(agg =>
        {
            var field = agg.field;
            var aggFn = agg.aggregation.ToLowerInvariant();
            
            return aggFn switch
            {
                "sum" => $"  \"total_{field}\": $sum({field})",
                "avg" or "average" => $"  \"avg_{field}\": $average({field})",
                "count" => $"  \"count_{field}\": $count({field})",
                "min" => $"  \"min_{field}\": $min({field})",
                "max" => $"  \"max_{field}\": $max({field})",
                _ => $"  \"agg_{field}\": $sum({field})"
            };
        });

        var aggString = string.Join(",\n", aggregationDsl);
        var groupDsl = $"({{\n  \"{groupByField}\": {groupByField},\n{aggString}\n}}) ~> $group({groupByField})";
        
        // Jsonata: when applied to array, object constructor + grouping iterates automatically
        if (string.IsNullOrEmpty(sourcePath))
            return groupDsl;  // Root is array, Jsonata will iterate
        else
            return $"{sourcePath}.({groupDsl})";
    }

    /// <summary>
    /// Template 7: Filter + Map
    /// Filters data based on a condition and maps to output.
    /// Example: Filter active users and extract name, email
    /// </summary>
    public static string Template7_FilterMap(
        string sourcePath,
        string? filterCondition,
        Dictionary<string, string?> fieldMappings)
    {
        // sourcePath: "users"
        // filterCondition: "status = 'active'" or null (no filter)
        // fieldMappings: {"name": null, "email": "email_address"}
        
        var filterClause = string.IsNullOrWhiteSpace(filterCondition)
            ? ""
            : $"[{filterCondition}]";

        var fields = fieldMappings.Select(kvp =>
        {
            var sourceField = kvp.Key;
            var targetField = kvp.Value ?? kvp.Key;
            return $"  \"{targetField}\": {sourceField}";
        });

        var mappingDsl = string.Join(",\n", fields);
        var objectConstructor = $"{{\n{mappingDsl}\n}}";
        
        // Jsonata: filter + object constructor
        // When applied to array, Jsonata iterates and applies filter + map to each item
        if (string.IsNullOrEmpty(sourcePath))
        {
            // Root is array: Jsonata will iterate when applying filter+object
            if (string.IsNullOrEmpty(filterClause))
                return objectConstructor;  // No filter, just map
            else
                return $"{filterClause}{objectConstructor}";
        }
        else
        {
            // sourcePath is provided: apply filter+map to that array
            if (string.IsNullOrEmpty(filterClause))
                return $"{sourcePath}.{objectConstructor}";
            else
                return $"{sourcePath}.{filterClause}{objectConstructor}";
        }
    }

    /// <summary>
    /// Detect transformation goal from natural language.
    /// Returns the most appropriate template ID.
    /// </summary>
    public static string DetectTemplate(string goalText)
    {
        var goal = goalText.ToLowerInvariant();

        // Check for aggregation keywords
        if (goal.Contains("sum") || goal.Contains("total") || goal.Contains("aggregate") || 
            goal.Contains("group") || goal.Contains("category") || goal.Contains("category"))
        {
            return "T5";
        }

        // Check for filter keywords
        if (goal.Contains("filter") || goal.Contains("where") || goal.Contains("active") ||
            goal.Contains("inactive") || goal.Contains("status") || goal.Contains("select") || goal.Contains("exclude"))
        {
            return "T7";
        }

        // Default to extract/rename
        return "T1";
    }

    /// <summary>
    /// Try to extract template parameters from sample input.
    /// Returns null if extraction fails or not applicable.
    /// </summary>
    public static DslTemplateParams? TryExtractParameters(
        JsonElement sampleInput,
        string templateId,
        string goalText)
    {
        try
        {
            return templateId switch
            {
                "T1" => ExtractTemplate1Parameters(sampleInput, goalText),
                "T5" => ExtractTemplate5Parameters(sampleInput, goalText),
                "T7" => ExtractTemplate7Parameters(sampleInput, goalText),
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    private static DslTemplateParams? ExtractTemplate1Parameters(JsonElement input, string goal)
    {
        try
        {
            // Discover the actual array path (not hardcoded "data")
            string sourcePath = "";
            
            if (input.ValueKind == JsonValueKind.Array)
            {
                sourcePath = "";  // Root is array
            }
            else if (input.ValueKind == JsonValueKind.Object)
            {
                // Check if input has a single array property
                var arrayProps = new List<string>();
                foreach (var prop in input.EnumerateObject())
                {
                    if (prop.Value.ValueKind == JsonValueKind.Array)
                        arrayProps.Add(prop.Name);
                }
                
                if (arrayProps.Count == 1)
                {
                    sourcePath = arrayProps[0];
                }
                else
                {
                    return null;  // Can't extract template
                }
            }
            else
            {
                return null;
            }

            // Extract item object
            JsonElement itemObj;
            if (string.IsNullOrEmpty(sourcePath))
            {
                // Root is array
                if (input.ValueKind == JsonValueKind.Array && input.GetArrayLength() > 0)
                {
                    itemObj = input[0];
                }
                else
                {
                    return null;
                }
            }
            else
            {
                // sourcePath is a property name
                if (input.TryGetProperty(sourcePath, out var arrayProp) && 
                    arrayProp.ValueKind == JsonValueKind.Array && 
                    arrayProp.GetArrayLength() > 0)
                {
                    itemObj = arrayProp[0];
                }
                else
                {
                    return null;
                }
            }

            if (itemObj.ValueKind != JsonValueKind.Object)
                return null;

            // Parse excluded fields from goal
            var excludedFields = ParseExcludedFieldsFromGoal(goal);

            // Build field mappings (no translation - preserve original field names)
            var fields = new Dictionary<string, string?>();
            foreach (var prop in itemObj.EnumerateObject())
            {
                var fieldName = prop.Name;
                
                // Skip excluded fields
                if (excludedFields.Contains(fieldName.ToLower()))
                    continue;

                // No mapping - preserve original field name
                fields[fieldName] = null;  // null means "use source field name as output field name"
            }

            return new DslTemplateParams
            {
                TemplateId = "T1",
                SourcePath = sourcePath,
                FieldMappings = fields
            };
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"🚨 ExtractTemplate1Parameters EXCEPTION: {ex.Message}\n{ex.StackTrace}");
            return null;
        }
    }

    /// <summary>
    /// Discovers the array path in input:
    /// - If input IS array → returns "" (root)
    /// - If input is object with single array property → returns property name
    /// - Otherwise → returns null
    /// </summary>
    private static string? DiscoverArrayPath(JsonElement input)
    {
        if (input.ValueKind == JsonValueKind.Array)
            return ""; // Root is array

        if (input.ValueKind != JsonValueKind.Object)
            return null;

        var arrayProperties = new List<string>();
        foreach (var prop in input.EnumerateObject())
        {
            if (prop.Value.ValueKind == JsonValueKind.Array)
                arrayProperties.Add(prop.Name);
        }

        // If exactly one array property → use it
        return arrayProperties.Count == 1 ? arrayProperties[0] : null;
    }

    /// <summary>
    /// Extracts the item object from array using discovered path
    /// </summary>
    private static JsonElement? ExtractItemObject(JsonElement input, string sourcePath)
    {
        JsonElement array = input;

        if (!string.IsNullOrEmpty(sourcePath))
        {
            if (input.TryGetProperty(sourcePath, out var arrayProp) && arrayProp.ValueKind == JsonValueKind.Array)
            {
                array = arrayProp;
            }
            else
            {
                return null;
            }
        }

        if (array.ValueKind != JsonValueKind.Array || array.GetArrayLength() == 0)
            return null;

        return array[0];
    }

    /// <summary>
    /// Parses excluded fields from goal text (e.g., "não preciso da idade" → ["idade"])
    /// </summary>
    private static HashSet<string> ParseExcludedFieldsFromGoal(string goal)
    {
        var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(goal))
            return excluded;

        // Pattern: "não preciso da/do FIELD" or "excluir FIELD" or "sem FIELD"
        var patterns = new[]
        {
            @"não\s+preciso\s+(?:da|do)\s+(\w+)",
            @"excluir\s+(\w+)",
            @"sem\s+(\w+)",
            @"ignorar\s+(\w+)"
        };

        foreach (var pattern in patterns)
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(goal, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                if (match.Groups.Count > 1)
                    excluded.Add(match.Groups[1].Value.ToLower());
            }
        }

        return excluded;
    }

    private static DslTemplateParams? ExtractTemplate5Parameters(JsonElement input, string goal)
    {
        // Discover the actual array path
        var sourcePath = DiscoverArrayPath(input);
        if (sourcePath == null)
            return null;

        // Extract item object for field inspection
        var itemObject = ExtractItemObject(input, sourcePath);
        if (itemObject?.ValueKind != JsonValueKind.Object)
            return null;

        var fields = new List<string>();
        var numericFields = new List<string>();

        foreach (var prop in itemObject.Value.EnumerateObject())
        {
            fields.Add(prop.Name);
            if (prop.Value.ValueKind == JsonValueKind.Number)
            {
                numericFields.Add(prop.Name);
            }
        }

        // Heuristic: group by first string field, aggregate first numeric field
        var groupByField = fields.FirstOrDefault(f =>
        {
            var val = itemObject.Value.GetProperty(f);
            return val.ValueKind == JsonValueKind.String;
        }) ?? fields.FirstOrDefault() ?? "category";

        var aggregations = new List<(string, string)>();
        if (numericFields.Count > 0)
        {
            aggregations.Add((numericFields[0], "sum"));
        }

        return new DslTemplateParams
        {
            TemplateId = "T5",
            SourcePath = sourcePath,
            GroupByField = groupByField,
            Aggregations = aggregations
        };
    }

    private static DslTemplateParams? ExtractTemplate7Parameters(JsonElement input, string goal)
    {
        // Discover the actual array path
        var sourcePath = DiscoverArrayPath(input);
        if (sourcePath == null)
            return null;

        // Extract item object for field inspection
        var itemObject = ExtractItemObject(input, sourcePath);
        if (itemObject?.ValueKind != JsonValueKind.Object)
            return null;

        var fields = new Dictionary<string, string?>();
        foreach (var prop in itemObject.Value.EnumerateObject())
        {
            fields[prop.Name] = null;
        }

        return new DslTemplateParams
        {
            TemplateId = "T7",
            SourcePath = sourcePath,
            FilterCondition = null, // No filter specified
            FieldMappings = fields
        };
    }
}

/// <summary>
/// Parameters for template instantiation.
/// </summary>
public class DslTemplateParams
{
    public string TemplateId { get; set; } = "";
    public string SourcePath { get; set; } = "data";
    
    // For T1, T7
    public Dictionary<string, string?>? FieldMappings { get; set; }
    
    // For T5
    public string? GroupByField { get; set; }
    public List<(string field, string aggregation)>? Aggregations { get; set; }
    
    // For T7
    public string? FilterCondition { get; set; }
}
