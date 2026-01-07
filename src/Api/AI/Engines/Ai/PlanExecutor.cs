using System.Text.Json;
using System.Text.RegularExpressions;

namespace Metrics.Api.AI.Engines.Ai;

/// <summary>
/// Executes plan operations deterministically.
/// </summary>
public static partial class PlanExecutor
{
    /// <summary>
    /// Result of plan execution
    /// </summary>
    public record ExecuteResult(
        bool Success,
        List<Dictionary<string, JsonElement>>? Rows,
        List<string> Warnings,
        string? Error
    );

    /// <summary>
    /// Executes a complete plan against the input data.
    /// </summary>
    public static ExecuteResult Execute(
        TransformPlan plan,
        JsonElement input,
        Serilog.ILogger? logger = null)
    {
        var warnings = new List<string>();

        // 1. Extract recordset
        var extractResult = ShapeNormalizer.ExtractAndNormalize(input, plan.Source.RecordPath);
        if (!extractResult.Success)
        {
            return new ExecuteResult(false, null, warnings, extractResult.Error);
        }

        var rows = extractResult.Rows!;
        logger?.Debug("Plan execution: Extracted {Count} rows from {Path}", rows.Count, plan.Source.RecordPath);

        // Get sample record for field resolution
        JsonElement sampleRecord = rows.Count > 0
            ? ShapeNormalizer.ToJsonElement(new List<Dictionary<string, JsonElement>> { rows[0] })[0]
            : JsonDocument.Parse("{}").RootElement;

        // 2. Execute each step in sequence
        foreach (var step in plan.Steps)
        {
            // Resolve fields with aliases
            var (resolvedStep, stepWarnings) = FieldResolver.ResolveStep(step, sampleRecord);
            warnings.AddRange(stepWarnings);

            var stepResult = ExecuteStep(resolvedStep, rows, logger);
            if (!stepResult.Success)
            {
                return new ExecuteResult(false, null, warnings, stepResult.Error);
            }

            rows = stepResult.Rows!;
            warnings.AddRange(stepResult.Warnings);

            // Update sample record for next step
            if (rows.Count > 0)
            {
                sampleRecord = ShapeNormalizer.ToJsonElement(new List<Dictionary<string, JsonElement>> { rows[0] })[0];
            }

            logger?.Debug("Plan step '{Op}' completed: {Count} rows", step.Op, rows.Count);
        }

        return new ExecuteResult(true, rows, warnings, null);
    }

    /// <summary>
    /// Executes a single step
    /// </summary>
    public static ExecuteResult ExecuteStep(
        PlanStep step,
        List<Dictionary<string, JsonElement>> rows,
        Serilog.ILogger? logger = null)
    {
        try
        {
            return step.Op switch
            {
                PlanOps.Select => ExecuteSelect(step, rows),
                PlanOps.Filter => ExecuteFilter(step, rows),
                PlanOps.Compute => ExecuteCompute(step, rows),
                PlanOps.MapValue => ExecuteMapValue(step, rows),
                PlanOps.Sort => ExecuteSort(step, rows),
                PlanOps.GroupBy => ExecuteGroupBy(step, rows),
                PlanOps.Aggregate => ExecuteAggregate(step, rows),
                PlanOps.Limit => ExecuteLimit(step, rows),
                _ => new ExecuteResult(false, null, new List<string>(), $"Unknown operation: {step.Op}")
            };
        }
        catch (Exception ex)
        {
            logger?.Error(ex, "Plan step '{Op}' failed", step.Op);
            return new ExecuteResult(false, null, new List<string>(), $"Step '{step.Op}' failed: {ex.Message}");
        }
    }

    #region Select Operation

    private static ExecuteResult ExecuteSelect(PlanStep step, List<Dictionary<string, JsonElement>> rows)
    {
        if (step.Fields == null || step.Fields.Count == 0)
        {
            return new ExecuteResult(false, null, new List<string>(), "Select requires 'fields' specification");
        }

        var result = new List<Dictionary<string, JsonElement>>();

        foreach (var row in rows)
        {
            var newRow = new Dictionary<string, JsonElement>();

            foreach (var field in step.Fields)
            {
                var value = GetValue(row, field.From);
                newRow[field.As] = value;
            }

            result.Add(newRow);
        }

        return new ExecuteResult(true, result, new List<string>(), null);
    }

    #endregion

    #region Filter Operation

    private static ExecuteResult ExecuteFilter(PlanStep step, List<Dictionary<string, JsonElement>> rows)
    {
        if (step.Where == null)
        {
            return new ExecuteResult(false, null, new List<string>(), "Filter requires 'where' condition");
        }

        var result = rows.Where(row => EvaluateCondition(step.Where, row)).ToList();

        return new ExecuteResult(true, result, new List<string>(), null);
    }

    private static bool EvaluateCondition(Condition condition, Dictionary<string, JsonElement> row)
    {
        return condition.Op switch
        {
            ConditionOps.And => condition.Items?.All(c => EvaluateCondition(c, row)) ?? false,
            ConditionOps.Or => condition.Items?.Any(c => EvaluateCondition(c, row)) ?? false,
            ConditionOps.Not => condition.Items?.Count > 0 && !EvaluateCondition(condition.Items[0], row),
            ConditionOps.Eq => CompareValues(GetOperandValue(condition.Left, row), GetOperandValue(condition.Right, row)) == 0,
            ConditionOps.Neq => CompareValues(GetOperandValue(condition.Left, row), GetOperandValue(condition.Right, row)) != 0,
            ConditionOps.Gt => CompareValues(GetOperandValue(condition.Left, row), GetOperandValue(condition.Right, row)) > 0,
            ConditionOps.Gte => CompareValues(GetOperandValue(condition.Left, row), GetOperandValue(condition.Right, row)) >= 0,
            ConditionOps.Lt => CompareValues(GetOperandValue(condition.Left, row), GetOperandValue(condition.Right, row)) < 0,
            ConditionOps.Lte => CompareValues(GetOperandValue(condition.Left, row), GetOperandValue(condition.Right, row)) <= 0,
            ConditionOps.Contains => ContainsValue(GetOperandValue(condition.Left, row), GetOperandValue(condition.Right, row)),
            ConditionOps.In => InArray(GetOperandValue(condition.Left, row), GetOperandValue(condition.Right, row)),
            _ => false
        };
    }

    private static JsonElement? GetOperandValue(JsonElement? operand, Dictionary<string, JsonElement> row)
    {
        if (operand == null) return null;

        // Check if operand is a field reference (object with "field" property)
        if (operand.Value.ValueKind == JsonValueKind.Object &&
            operand.Value.TryGetProperty("field", out var fieldProp))
        {
            var fieldPath = fieldProp.GetString();
            if (!string.IsNullOrEmpty(fieldPath))
            {
                return GetValue(row, fieldPath);
            }
        }

        // Otherwise return literal value
        return operand;
    }

    private static int CompareValues(JsonElement? left, JsonElement? right)
    {
        if (left == null && right == null) return 0;
        if (left == null) return -1;
        if (right == null) return 1;

        var leftVal = left.Value;
        var rightVal = right.Value;

        // Compare numbers
        if (IsNumber(leftVal) && IsNumber(rightVal))
        {
            var leftNum = GetNumber(leftVal);
            var rightNum = GetNumber(rightVal);
            return leftNum.CompareTo(rightNum);
        }

        // Compare strings
        var leftStr = GetString(leftVal);
        var rightStr = GetString(rightVal);
        return string.Compare(leftStr, rightStr, StringComparison.Ordinal);
    }

    private static bool ContainsValue(JsonElement? haystack, JsonElement? needle)
    {
        if (haystack == null || needle == null) return false;

        var haystackStr = GetString(haystack.Value);
        var needleStr = GetString(needle.Value);

        return haystackStr.Contains(needleStr, StringComparison.OrdinalIgnoreCase);
    }

    private static bool InArray(JsonElement? value, JsonElement? array)
    {
        if (value == null || array == null) return false;
        if (array.Value.ValueKind != JsonValueKind.Array) return false;

        foreach (var item in array.Value.EnumerateArray())
        {
            if (CompareValues(value, item) == 0) return true;
        }

        return false;
    }

    #endregion

    #region Compute Operation

    private static ExecuteResult ExecuteCompute(PlanStep step, List<Dictionary<string, JsonElement>> rows)
    {
        if (step.Compute == null || step.Compute.Count == 0)
        {
            return new ExecuteResult(false, null, new List<string>(), "Compute requires 'compute' specifications");
        }

        var result = new List<Dictionary<string, JsonElement>>();

        foreach (var row in rows)
        {
            var newRow = new Dictionary<string, JsonElement>(row);

            foreach (var compute in step.Compute)
            {
                var value = EvaluateExpression(compute.Expr, row);
                newRow[compute.As] = value;
            }

            result.Add(newRow);
        }

        return new ExecuteResult(true, result, new List<string>(), null);
    }

    private static JsonElement EvaluateExpression(string expr, Dictionary<string, JsonElement> row)
    {
        // Parse simple arithmetic expressions: a + b, a - b, a * b, a / b
        // Where a and b can be field names or numeric literals

        // Try to match binary operations
        var match = BinaryExpressionRegex().Match(expr);
        if (match.Success)
        {
            var leftExpr = match.Groups[1].Value.Trim();
            var op = match.Groups[2].Value;
            var rightExpr = match.Groups[3].Value.Trim();

            var leftVal = GetExpressionValue(leftExpr, row);
            var rightVal = GetExpressionValue(rightExpr, row);

            var result = op switch
            {
                "+" => leftVal + rightVal,
                "-" => leftVal - rightVal,
                "*" => leftVal * rightVal,
                "/" when rightVal != 0 => leftVal / rightVal,
                "/" => 0.0, // Division by zero => 0
                _ => 0.0
            };

            return JsonSerializer.SerializeToElement(result);
        }

        // Not a binary expression, try as field reference or literal
        var value = GetExpressionValue(expr.Trim(), row);
        return JsonSerializer.SerializeToElement(value);
    }

    private static double GetExpressionValue(string expr, Dictionary<string, JsonElement> row)
    {
        // Try as numeric literal
        if (double.TryParse(expr, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var numericValue))
        {
            return numericValue;
        }

        // Try as field reference (with or without leading /)
        var fieldPath = expr.StartsWith("/") ? expr : "/" + expr;
        var fieldValue = GetValue(row, fieldPath);

        return GetNumber(fieldValue);
    }

    [GeneratedRegex(@"^(.+?)\s*([+\-*/])\s*(.+)$")]
    private static partial Regex BinaryExpressionRegex();

    #endregion

    #region MapValue Operation

    private static ExecuteResult ExecuteMapValue(PlanStep step, List<Dictionary<string, JsonElement>> rows)
    {
        if (step.Map == null || step.Map.Count == 0)
        {
            return new ExecuteResult(false, null, new List<string>(), "MapValue requires 'map' specifications");
        }

        var result = new List<Dictionary<string, JsonElement>>();

        foreach (var row in rows)
        {
            var newRow = new Dictionary<string, JsonElement>(row);

            foreach (var map in step.Map)
            {
                var sourceValue = GetValue(row, map.From);
                var sourceKey = GetString(sourceValue);

                // Look up in mapping
                if (map.Mapping.TryGetValue(sourceKey, out var mappedValue))
                {
                    newRow[map.As] = mappedValue.Clone();
                }
                else if (map.Default != null)
                {
                    newRow[map.As] = map.Default.Value.Clone();
                }
                else
                {
                    newRow[map.As] = sourceValue; // Keep original if no mapping
                }
            }

            result.Add(newRow);
        }

        return new ExecuteResult(true, result, new List<string>(), null);
    }

    #endregion

    #region Sort Operation

    private static ExecuteResult ExecuteSort(PlanStep step, List<Dictionary<string, JsonElement>> rows)
    {
        if (string.IsNullOrEmpty(step.By))
        {
            return new ExecuteResult(false, null, new List<string>(), "Sort requires 'by' field");
        }

        var sorted = step.Dir?.ToLowerInvariant() == "desc"
            ? rows.OrderByDescending(r => GetSortKey(r, step.By)).ToList()
            : rows.OrderBy(r => GetSortKey(r, step.By)).ToList();

        return new ExecuteResult(true, sorted, new List<string>(), null);
    }

    private static object GetSortKey(Dictionary<string, JsonElement> row, string path)
    {
        var value = GetValue(row, path);

        return value.ValueKind switch
        {
            JsonValueKind.Number => value.GetDouble(),
            JsonValueKind.String => value.GetString() ?? "",
            JsonValueKind.True => 1,
            JsonValueKind.False => 0,
            _ => ""
        };
    }

    #endregion

    #region GroupBy Operation

    private static ExecuteResult ExecuteGroupBy(PlanStep step, List<Dictionary<string, JsonElement>> rows)
    {
        if (step.Keys == null || step.Keys.Count == 0)
        {
            return new ExecuteResult(false, null, new List<string>(), "GroupBy requires 'keys' specification");
        }

        // Group rows by composite key
        var groups = new Dictionary<string, List<Dictionary<string, JsonElement>>>();

        foreach (var row in rows)
        {
            var keyParts = step.Keys.Select(k => GetString(GetValue(row, k)));
            var compositeKey = string.Join("||", keyParts);

            if (!groups.ContainsKey(compositeKey))
            {
                groups[compositeKey] = new List<Dictionary<string, JsonElement>>();
            }
            groups[compositeKey].Add(row);
        }

        // Create result rows with group keys (aggregate metrics in subsequent Aggregate step)
        var result = new List<Dictionary<string, JsonElement>>();

        foreach (var kvp in groups)
        {
            var firstRow = kvp.Value[0];
            var groupRow = new Dictionary<string, JsonElement>();

            // Add key fields
            foreach (var key in step.Keys)
            {
                var fieldName = GetFieldName(key);
                groupRow[fieldName] = GetValue(firstRow, key);
            }

            // Store group rows for later aggregation (using special key)
            groupRow["__group_rows__"] = JsonSerializer.SerializeToElement(kvp.Value);

            result.Add(groupRow);
        }

        return new ExecuteResult(true, result, new List<string>(), null);
    }

    #endregion

    #region Aggregate Operation

    private static ExecuteResult ExecuteAggregate(PlanStep step, List<Dictionary<string, JsonElement>> rows)
    {
        if (step.Metrics == null || step.Metrics.Count == 0)
        {
            return new ExecuteResult(false, null, new List<string>(), "Aggregate requires 'metrics' specification");
        }

        var result = new List<Dictionary<string, JsonElement>>();

        foreach (var row in rows)
        {
            var newRow = new Dictionary<string, JsonElement>();

            // Copy non-group fields
            foreach (var kvp in row)
            {
                if (kvp.Key != "__group_rows__")
                {
                    newRow[kvp.Key] = kvp.Value;
                }
            }

            // Get group rows for aggregation
            List<Dictionary<string, JsonElement>> groupRows;
            if (row.TryGetValue("__group_rows__", out var groupRowsElement))
            {
                groupRows = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(groupRowsElement.GetRawText())!;
            }
            else
            {
                // No grouping - aggregate over single row
                groupRows = new List<Dictionary<string, JsonElement>> { row };
            }

            // Calculate metrics
            foreach (var metric in step.Metrics)
            {
                var aggregateValue = CalculateAggregate(metric, groupRows);
                newRow[metric.As] = aggregateValue;
            }

            result.Add(newRow);
        }

        return new ExecuteResult(true, result, new List<string>(), null);
    }

    private static JsonElement CalculateAggregate(MetricSpec metric, List<Dictionary<string, JsonElement>> rows)
    {
        if (metric.Fn == AggregateFunctions.Count)
        {
            return JsonSerializer.SerializeToElement(rows.Count);
        }

        // Get values to aggregate
        var values = new List<double>();
        foreach (var row in rows)
        {
            double value;
            if (!string.IsNullOrEmpty(metric.Expr))
            {
                var computed = EvaluateExpression(metric.Expr, row);
                value = GetNumber(computed);
            }
            else if (!string.IsNullOrEmpty(metric.Field))
            {
                value = GetNumber(GetValue(row, metric.Field));
            }
            else
            {
                continue;
            }
            values.Add(value);
        }

        if (values.Count == 0)
        {
            return JsonSerializer.SerializeToElement(0);
        }

        var result = metric.Fn switch
        {
            AggregateFunctions.Sum => values.Sum(),
            AggregateFunctions.Avg => values.Average(),
            AggregateFunctions.Min => values.Min(),
            AggregateFunctions.Max => values.Max(),
            _ => 0.0
        };

        return JsonSerializer.SerializeToElement(result);
    }

    #endregion

    #region Limit Operation

    private static ExecuteResult ExecuteLimit(PlanStep step, List<Dictionary<string, JsonElement>> rows)
    {
        if (step.N == null || step.N <= 0)
        {
            return new ExecuteResult(false, null, new List<string>(), "Limit requires 'n' > 0");
        }

        var result = rows.Take(step.N.Value).ToList();

        return new ExecuteResult(true, result, new List<string>(), null);
    }

    #endregion

    #region Helpers

    private static JsonElement GetValue(Dictionary<string, JsonElement> row, string path)
    {
        // Handle JSON pointer format
        var fieldName = GetFieldName(path);

        if (row.TryGetValue(fieldName, out var value))
        {
            return value;
        }

        // Try case-insensitive
        var key = row.Keys.FirstOrDefault(k => k.Equals(fieldName, StringComparison.OrdinalIgnoreCase));
        if (key != null)
        {
            return row[key];
        }

        return JsonSerializer.SerializeToElement<object?>(null);
    }

    private static string GetFieldName(string path)
    {
        // Convert /field to field
        if (path.StartsWith("/"))
        {
            path = path[1..];
        }

        // Get first segment only (no nested paths for now)
        var slashIndex = path.IndexOf('/');
        if (slashIndex >= 0)
        {
            path = path[..slashIndex];
        }

        return path;
    }

    private static bool IsNumber(JsonElement element)
    {
        return element.ValueKind == JsonValueKind.Number;
    }

    private static double GetNumber(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.String when double.TryParse(element.GetString(), out var n) => n,
            _ => 0.0
        };
    }

    private static string GetString(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? "",
            JsonValueKind.Number => element.GetDouble().ToString(System.Globalization.CultureInfo.InvariantCulture),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => ""
        };
    }

    #endregion
}
