using System.Text.Json;

namespace Metrics.Api.AI.Engines.PlanV1;

/// <summary>
/// Validates plans with basic structural validation.
/// Full NJsonSchema validation is skipped for MVP due to $ref resolution issues.
/// </summary>
public class PlanSchemaValidator
{
    /// <summary>
    /// Result of plan validation
    /// </summary>
    public record ValidationResult(
        bool IsValid,
        List<string> Errors
    );

    /// <summary>
    /// Validates a plan with basic structural checks
    /// </summary>
    public ValidationResult Validate(TransformPlan plan)
    {
        var errors = new List<string>();

        // Check required fields
        if (string.IsNullOrEmpty(plan.PlanVersion))
        {
            errors.Add("planVersion is required");
        }
        else if (plan.PlanVersion != "1.0")
        {
            errors.Add($"planVersion must be '1.0', got '{plan.PlanVersion}'");
        }

        if (plan.Source == null)
        {
            errors.Add("source is required");
        }
        else if (string.IsNullOrEmpty(plan.Source.RecordPath))
        {
            errors.Add("source.recordPath is required");
        }

        if (plan.Steps == null || plan.Steps.Count == 0)
        {
            errors.Add("steps must have at least one item");
        }
        else
        {
            for (int i = 0; i < plan.Steps.Count; i++)
            {
                var stepErrors = ValidateStep(plan.Steps[i], i);
                errors.AddRange(stepErrors);
            }
        }

        return new ValidationResult(errors.Count == 0, errors);
    }

    /// <summary>
    /// Validates a plan from JSON string
    /// </summary>
    public ValidationResult ValidateJson(string planJson)
    {
        try
        {
            var plan = JsonSerializer.Deserialize<TransformPlan>(planJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (plan == null)
            {
                return new ValidationResult(false, new List<string> { "Failed to deserialize plan" });
            }

            return Validate(plan);
        }
        catch (JsonException ex)
        {
            return new ValidationResult(false, new List<string> { $"Invalid JSON: {ex.Message}" });
        }
    }

    private List<string> ValidateStep(PlanStep step, int index)
    {
        var errors = new List<string>();
        var prefix = $"steps[{index}]";

        if (string.IsNullOrEmpty(step.Op))
        {
            errors.Add($"{prefix}.op is required");
            return errors;
        }

        var validOps = new[] { PlanOps.Select, PlanOps.Filter, PlanOps.Compute, PlanOps.MapValue,
                               PlanOps.GroupBy, PlanOps.Aggregate, PlanOps.Sort, PlanOps.Limit };

        if (!validOps.Contains(step.Op))
        {
            errors.Add($"{prefix}.op '{step.Op}' is not valid. Expected one of: {string.Join(", ", validOps)}");
            return errors;
        }

        // Validate required properties per operation
        switch (step.Op)
        {
            case PlanOps.Select:
                if (step.Fields == null || step.Fields.Count == 0)
                {
                    errors.Add($"{prefix}: 'select' requires 'fields'");
                }
                break;

            case PlanOps.Filter:
                if (step.Where == null)
                {
                    errors.Add($"{prefix}: 'filter' requires 'where'");
                }
                break;

            case PlanOps.Compute:
                if (step.Compute == null || step.Compute.Count == 0)
                {
                    errors.Add($"{prefix}: 'compute' requires 'compute' array");
                }
                break;

            case PlanOps.MapValue:
                if (step.Map == null || step.Map.Count == 0)
                {
                    errors.Add($"{prefix}: 'mapValue' requires 'map' array");
                }
                break;

            case PlanOps.GroupBy:
                if (step.Keys == null || step.Keys.Count == 0)
                {
                    errors.Add($"{prefix}: 'groupBy' requires 'keys'");
                }
                break;

            case PlanOps.Aggregate:
                if (step.Metrics == null || step.Metrics.Count == 0)
                {
                    errors.Add($"{prefix}: 'aggregate' requires 'metrics'");
                }
                break;

            case PlanOps.Sort:
                if (string.IsNullOrEmpty(step.By))
                {
                    errors.Add($"{prefix}: 'sort' requires 'by'");
                }
                break;

            case PlanOps.Limit:
                if (step.N == null || step.N <= 0)
                {
                    errors.Add($"{prefix}: 'limit' requires 'n' > 0");
                }
                break;
        }

        return errors;
    }
}
