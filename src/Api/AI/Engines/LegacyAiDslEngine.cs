using System.Text.Json;
using Metrics.Engine;
using Serilog;

namespace Metrics.Api.AI.Engines;

/// <summary>
/// Legacy engine that uses LLM-based DSL generation (Jsonata).
/// This encapsulates the original flow: LLM prompt → parse → validate → repair → template fallback.
/// </summary>
public class LegacyAiDslEngine : IAiTransformationEngine
{
    private readonly IAiProvider _aiProvider;
    private readonly AiConfiguration _aiConfig;
    private readonly EngineService _engine;
    private readonly Serilog.ILogger _logger;

    public string EngineType => AI.EngineType.Legacy;

    public LegacyAiDslEngine(
        IAiProvider aiProvider,
        AiConfiguration aiConfig,
        EngineService engine,
        Serilog.ILogger logger)
    {
        _aiProvider = aiProvider;
        _aiConfig = aiConfig;
        _engine = engine;
        _logger = logger;
    }

    public async Task<AiEngineResult> GenerateAsync(
        DslGenerateRequest request,
        string correlationId,
        CancellationToken ct)
    {
        try
        {
            // Check if AI is enabled
            if (!_aiConfig.Enabled)
            {
                return AiEngineResult.Fail(new AiError
                {
                    Code = AiErrorCodes.AiDisabled,
                    Message = "AI functionality is disabled. Enable it in appsettings.json under AI.Enabled.",
                    CorrelationId = correlationId
                }, 503);
            }

            // Validate request against guardrails
            var requestValidation = AiGuardrails.ValidateRequest(request);
            if (!requestValidation.IsValid)
            {
                return AiEngineResult.Fail(new AiError
                {
                    Code = AiErrorCodes.AiOutputInvalid,
                    Message = "Request validation failed",
                    Details = requestValidation.Errors,
                    CorrelationId = correlationId
                }, 400);
            }

            // Log request (without sensitive data)
            var inputHash = AiGuardrails.ComputeInputHash(request.SampleInput);
            _logger.Information(
                "AI DSL Generate (Legacy): CorrelationId={CorrelationId}, Profile={Profile}, GoalLength={GoalLength}, InputHash={InputHash}",
                correlationId, request.DslProfile, request.GoalText.Length, inputHash);

            // Call AI provider with optional repair attempt
            var startTime = DateTime.UtcNow;
            const int maxRepairAttempts = 1;

            DslGenerateResult? result = null;
            List<string>? lastErrors = null;
            string? lastDslAttempt = null;

            for (int attempt = 0; attempt <= maxRepairAttempts; attempt++)
            {
                var isRepairAttempt = attempt > 0;

                // Build request (with repair hints if this is a retry)
                var currentRequest = request;
                if (isRepairAttempt && lastErrors != null && lastDslAttempt != null)
                {
                    _logger.Information(
                        "AI DSL Repair Attempt: CorrelationId={CorrelationId}, Attempt={Attempt}",
                        correlationId, attempt);

                    // Create repair request with hints containing validation errors
                    var repairHints = new Dictionary<string, string>
                    {
                        ["ValidationErrors"] = string.Join("; ", lastErrors),
                        ["JsonataDialectRules"] = "Review: no $.path (root is implicit), no [!cond] (use [cond=false] or [not cond]), $sum(array) and $average(array) are valid, $match returns [0].groups[n]"
                    };

                    currentRequest = request with
                    {
                        ExistingDsl = lastDslAttempt,
                        Hints = repairHints
                    };
                }

                try
                {
                    result = await _aiProvider.GenerateDslAsync(currentRequest, ct);
                }
                catch (AiProviderException ex)
                {
                    _logger.Error(ex, "AI provider error: {ErrorCode} - {Message}", ex.ErrorCode, ex.Message);
                    return AiEngineResult.Fail(new AiError
                    {
                        Code = ex.ErrorCode,
                        Message = ex.Message,
                        Details = ex.Details,
                        CorrelationId = correlationId
                    }, ex.ErrorCode == AiErrorCodes.AiOutputInvalid ? 502 : 503);
                }

                // Validate result structure
                var resultValidation = await AiGuardrails.ValidateResultAsync(result);
                if (!resultValidation.IsValid)
                {
                    lastErrors = resultValidation.Errors.Select(e => $"{e.Path}: {e.Message}").ToList();
                    lastDslAttempt = result.Dsl.Text;

                    if (attempt >= maxRepairAttempts)
                    {
                        _logger.Warning("AI output validation failed after repair: {Errors}", string.Join(", ", lastErrors));
                        return AiEngineResult.Fail(new AiError
                        {
                            Code = AiErrorCodes.AiOutputInvalid,
                            Message = "AI provider returned invalid output after repair attempt",
                            Details = resultValidation.Errors,
                            CorrelationId = correlationId
                        }, 502);
                    }
                    continue;
                }

                // Run preview/validation using the Engine
                try
                {
                    // Run transform without full validation
                    var previewResult = _engine.TransformValidateToCsv(
                        request.SampleInput,
                        result.Dsl.Profile,
                        result.Dsl.Text,
                        JsonSerializer.SerializeToElement(new { }));  // Use empty schema for preview

                    if (!previewResult.IsValid)
                    {
                        lastErrors = previewResult.Errors.ToList();
                        lastDslAttempt = result.Dsl.Text;

                        // Check for known bad patterns FIRST
                        var badPattern = DslBadPatternDetector.Detect(result.Dsl.Text);
                        if (badPattern != DslBadPatternDetector.BadPatternType.None)
                        {
                            _logger.Warning(
                                "Detected known bad pattern in DSL: {Pattern}. Message: {Description}. Skipping repair and going directly to template fallback.",
                                badPattern, DslBadPatternDetector.Describe(badPattern));

                            // Handle bad pattern with template fallback
                            if (attempt >= maxRepairAttempts)
                            {
                                var templateFallbackResult = TryTemplateFallback(request, result, correlationId, badPattern);
                                if (templateFallbackResult != null)
                                    return templateFallbackResult;

                                return AiEngineResult.Fail(new AiError
                                {
                                    Code = AiErrorCodes.AiOutputInvalid,
                                    Message = $"Bad DSL pattern detected: {DslBadPatternDetector.Describe(badPattern)}. Template fallback not applicable.",
                                    CorrelationId = correlationId
                                }, 502);
                            }
                        }

                        if (attempt >= maxRepairAttempts)
                        {
                            // Before giving up, try template fallback
                            _logger.Information("DSL failed after repair. Attempting template fallback...");

                            var templateFallbackResult = TryTemplateFallback(request, result, correlationId, null);
                            if (templateFallbackResult != null)
                                return templateFallbackResult;

                            // Template fallback didn't help, return error
                            _logger.Warning("AI-generated DSL preview failed after repair and template fallback");
                            return AiEngineResult.Fail(new AiError
                            {
                                Code = AiErrorCodes.AiOutputInvalid,
                                Message = "AI-generated DSL failed preview validation and template fallback failed",
                                Details = lastErrors.Select(e => new AiErrorDetail
                                {
                                    Path = "preview",
                                    Message = e
                                }).ToList(),
                                CorrelationId = correlationId
                            }, 502);
                        }

                        _logger.Information("DSL preview failed, attempting repair: {Errors}", string.Join(", ", lastErrors));
                        continue;
                    }

                    // DSL preview succeeded! Now infer schema from the output
                    _logger.Information("DSL preview succeeded, inferring output schema server-side");

                    if (previewResult.OutputJson.HasValue)
                    {
                        // Infer schema from actual preview output
                        var inferredSchema = OutputSchemaInferer.InferSchema(previewResult.OutputJson.Value);
                        result = result with { OutputSchema = inferredSchema };
                        _logger.Information("Generated output schema from preview output");
                    }

                    // Success!
                    break;
                }
                catch (Exception ex)
                {
                    lastErrors = new List<string> { ex.Message };
                    lastDslAttempt = result.Dsl.Text;

                    if (attempt >= maxRepairAttempts)
                    {
                        _logger.Warning(ex, "AI-generated DSL preview threw exception after repair");
                        return AiEngineResult.Fail(new AiError
                        {
                            Code = AiErrorCodes.AiOutputInvalid,
                            Message = $"AI-generated DSL failed preview after repair: {ex.Message}",
                            CorrelationId = correlationId
                        }, 502);
                    }

                    _logger.Information("DSL preview exception, attempting repair: {Error}", ex.Message);
                    continue;
                }
            }

            var latency = (DateTime.UtcNow - startTime).TotalMilliseconds;

            _logger.Information(
                "AI DSL Generate success (Legacy): CorrelationId={CorrelationId}, Latency={Latency}ms, DslLength={DslLength}",
                correlationId, latency, result!.Dsl.Text.Length);

            return AiEngineResult.Ok(result, EngineType);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Unexpected error in Legacy AI DSL generate: {Message}", ex.Message);
            return AiEngineResult.Fail(new AiError
            {
                Code = AiErrorCodes.AiProviderUnavailable,
                Message = "An unexpected error occurred",
                CorrelationId = correlationId
            }, 503);
        }
    }

    private AiEngineResult? TryTemplateFallback(
        DslGenerateRequest request,
        DslGenerateResult result,
        string correlationId,
        DslBadPatternDetector.BadPatternType? badPattern)
    {
        var templateId = DslTemplateLibrary.DetectTemplate(request.GoalText);
        _logger.Information("Detected template for fallback: {TemplateId}", templateId);

        var templateParams = DslTemplateLibrary.TryExtractParameters(
            JsonSerializer.SerializeToElement(request.SampleInput),
            templateId,
            request.GoalText);

        if (templateParams == null)
        {
            _logger.Warning("Template params extraction FAILED - template fallback skipped");
            return null;
        }

        var templateDsl = InstantiateTemplate(templateParams);
        _logger.Information("Generated template DSL: {TemplateDsl}", templateDsl);

        // Try template DSL with empty schema
        var templateResult = _engine.TransformValidateToCsv(
            request.SampleInput,
            result.Dsl.Profile,
            templateDsl,
            JsonSerializer.SerializeToElement(new { }));

        if (templateResult.IsValid && templateResult.OutputJson.HasValue)
        {
            var inferredSchema = OutputSchemaInferer.InferSchema(templateResult.OutputJson.Value);
            _logger.Information("Template fallback succeeded!");

            var warningMsg = badPattern.HasValue
                ? $"Bad DSL pattern detected ({badPattern}), used template fallback"
                : "Used template fallback due to DSL validation failure";

            var fallbackResult = new DslGenerateResult
            {
                Dsl = result.Dsl with { Text = templateDsl },
                OutputSchema = inferredSchema,
                Rationale = badPattern.HasValue
                    ? $"Used template fallback due to bad pattern: {DslBadPatternDetector.Describe(badPattern.Value)}"
                    : "Used template fallback due to DSL validation failure",
                ExampleRows = templateResult.OutputJson,
                Warnings = new List<string> { warningMsg },
                ModelInfo = result.ModelInfo
            };

            return AiEngineResult.Ok(fallbackResult, EngineType);
        }

        _logger.Warning("Template fallback also failed: {Errors}",
            string.Join(", ", templateResult.Errors));
        return null;
    }

    private static string InstantiateTemplate(DslTemplateParams? templateParams)
    {
        if (templateParams == null)
            return "{}";

        return templateParams.TemplateId switch
        {
            "T1" => DslTemplateLibrary.Template1_ExtractRename(
                templateParams.SourcePath,
                templateParams.FieldMappings ?? new()),

            "T5" => DslTemplateLibrary.Template5_GroupAggregate(
                templateParams.SourcePath,
                templateParams.GroupByField ?? "category",
                templateParams.Aggregations ?? new()),

            "T7" => DslTemplateLibrary.Template7_FilterMap(
                templateParams.SourcePath,
                templateParams.FilterCondition,
                templateParams.FieldMappings ?? new()),

            _ => "{}"
        };
    }
}
