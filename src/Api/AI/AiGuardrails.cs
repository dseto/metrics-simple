using System.Text;
using System.Text.Json;
using NJsonSchema;

namespace Metrics.Api.AI;

/// <summary>
/// Guardrails and validation for AI-assisted DSL generation.
/// Implements constraints from specs/backend/08-ai-assist/guardrails-and-validation.md
/// </summary>
public class AiGuardrails
{
    // Limits from spec
    public const int MaxGoalTextLength = 4000;
    public const int MaxSampleInputBytes = 500_000;
    public const int MaxExistingDslLength = 20000;
    public const int MaxRationaleLength = 8000;
    public const int MaxWarningLength = 2000;
    public const int MaxWarnings = 20;

    /// <summary>
    /// Validates a DSL generation request against guardrails
    /// </summary>
    public static ValidationResult ValidateRequest(DslGenerateRequest request)
    {
        var errors = new List<AiErrorDetail>();

        // Validate goalText
        if (string.IsNullOrWhiteSpace(request.GoalText))
        {
            errors.Add(new AiErrorDetail { Path = "goalText", Message = "goalText is required" });
        }
        else if (request.GoalText.Length < 10)
        {
            errors.Add(new AiErrorDetail { Path = "goalText", Message = "goalText must be at least 10 characters" });
        }
        else if (request.GoalText.Length > MaxGoalTextLength)
        {
            errors.Add(new AiErrorDetail { Path = "goalText", Message = $"goalText must not exceed {MaxGoalTextLength} characters" });
        }

        // Validate sampleInput size
        var sampleInputJson = JsonSerializer.Serialize(request.SampleInput);
        var sampleInputBytes = Encoding.UTF8.GetByteCount(sampleInputJson);
        if (sampleInputBytes > MaxSampleInputBytes)
        {
            errors.Add(new AiErrorDetail { Path = "sampleInput", Message = $"sampleInput must not exceed {MaxSampleInputBytes} bytes (got {sampleInputBytes})" });
        }

        // Validate dslProfile
        var validProfiles = new[] { "jsonata", "jmespath" };
        if (!validProfiles.Contains(request.DslProfile, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add(new AiErrorDetail { Path = "dslProfile", Message = $"dslProfile must be one of: {string.Join(", ", validProfiles)}" });
        }

        // Validate constraints
        if (request.Constraints.MaxColumns < 1 || request.Constraints.MaxColumns > 200)
        {
            errors.Add(new AiErrorDetail { Path = "constraints.maxColumns", Message = "maxColumns must be between 1 and 200" });
        }

        // Validate existingDsl length if provided
        if (!string.IsNullOrEmpty(request.ExistingDsl) && request.ExistingDsl.Length > MaxExistingDslLength)
        {
            errors.Add(new AiErrorDetail { Path = "existingDsl", Message = $"existingDsl must not exceed {MaxExistingDslLength} characters" });
        }

        return new ValidationResult(errors.Count == 0, errors);
    }

    /// <summary>
    /// Validates a DSL generation result against the schema
    /// </summary>
    public static async Task<ValidationResult> ValidateResultAsync(DslGenerateResult result)
    {
        var errors = new List<AiErrorDetail>();

        // Validate DSL
        if (result.Dsl == null)
        {
            errors.Add(new AiErrorDetail { Path = "dsl", Message = "dsl is required" });
        }
        else
        {
            if (string.IsNullOrWhiteSpace(result.Dsl.Text))
            {
                errors.Add(new AiErrorDetail { Path = "dsl.text", Message = "dsl.text is required and cannot be empty" });
            }
            else if (result.Dsl.Text.Length > 20000)
            {
                errors.Add(new AiErrorDetail { Path = "dsl.text", Message = "dsl.text must not exceed 20000 characters" });
            }

            var validProfiles = new[] { "jsonata", "jmespath", "custom" };
            if (!validProfiles.Contains(result.Dsl.Profile, StringComparer.OrdinalIgnoreCase))
            {
                errors.Add(new AiErrorDetail { Path = "dsl.profile", Message = $"dsl.profile must be one of: {string.Join(", ", validProfiles)}" });
            }
        }

        // Validate outputSchema is a valid JSON Schema object
        if (result.OutputSchema.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new AiErrorDetail { Path = "outputSchema", Message = "outputSchema must be a JSON object" });
        }
        else
        {
            // Try to parse as JSON Schema
            try
            {
                var schemaJson = JsonSerializer.Serialize(result.OutputSchema);
                await JsonSchema.FromJsonAsync(schemaJson);
            }
            catch (Exception ex)
            {
                errors.Add(new AiErrorDetail { Path = "outputSchema", Message = $"outputSchema is not a valid JSON Schema: {ex.Message}" });
            }
        }

        // Validate rationale
        if (result.Rationale != null && result.Rationale.Length > MaxRationaleLength)
        {
            errors.Add(new AiErrorDetail { Path = "rationale", Message = $"rationale must not exceed {MaxRationaleLength} characters" });
        }

        // Validate warnings
        if (result.Warnings != null)
        {
            if (result.Warnings.Count > MaxWarnings)
            {
                errors.Add(new AiErrorDetail { Path = "warnings", Message = $"warnings must not exceed {MaxWarnings} items" });
            }
            for (int i = 0; i < result.Warnings.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(result.Warnings[i]))
                {
                    errors.Add(new AiErrorDetail { Path = $"warnings[{i}]", Message = "warning must not be empty" });
                }
                else if (result.Warnings[i].Length > MaxWarningLength)
                {
                    errors.Add(new AiErrorDetail { Path = $"warnings[{i}]", Message = $"warning must not exceed {MaxWarningLength} characters" });
                }
            }
        }

        return new ValidationResult(errors.Count == 0, errors);
    }

    /// <summary>
    /// Truncates text deterministically to fit within limits
    /// </summary>
    public static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;

        // Truncate at maxLength - suffix length
        var suffix = "... [TRUNCATED]";
        return text[..(maxLength - suffix.Length)] + suffix;
    }

    /// <summary>
    /// Computes a hash of the input for logging (to avoid logging sensitive data)
    /// </summary>
    public static string ComputeInputHash(JsonElement input)
    {
        var json = JsonSerializer.Serialize(input);
        var bytes = Encoding.UTF8.GetBytes(json);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash)[..16];
    }

    /// <summary>
    /// Computes a hash of a DSL expression for logging
    /// </summary>
    public static string ComputeDslHash(string dslText)
    {
        var bytes = Encoding.UTF8.GetBytes(dslText);
        var hash = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

/// <summary>
/// Result of validation
/// </summary>
public record ValidationResult(bool IsValid, List<AiErrorDetail> Errors);
