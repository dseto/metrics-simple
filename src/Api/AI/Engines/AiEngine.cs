using System.Text.Json;
using Metrics.Api.AI.Engines.Ai;
using Metrics.Engine;
using Serilog;
using Microsoft.Extensions.Logging;

namespace Metrics.Api.AI.Engines;

/// <summary>
/// AI engine using LLM to generate IR (Intermediate Representation) plans
/// with deterministic fallback to templates when LLM fails.
/// 
/// Pipeline:
/// 1. Discover recordPath candidates
/// 2. Try LLM to generate plan (if available)
/// 3. If LLM fails: fallback to template matching
/// 4. Validate and execute plan
/// 5. Return preview + inferred schema
/// 
/// Never returns 502 - always fallback to template or 400 with clear message.
/// </summary>
public class AiEngine
{
    private readonly Serilog.ILogger _logger;
    private readonly EngineService _engine;
    private readonly PlanSchemaValidator _planValidator;
    private readonly AiLlmProvider? _llmProvider;

    public string EngineType => "ir";

    /// <summary>
    /// Creates engine with optional LLM support
    /// </summary>
    public AiEngine(
        Serilog.ILogger logger,
        EngineService engine,
        AiLlmProvider? llmProvider = null)
    {
        _logger = logger;
        _engine = engine;
        _planValidator = new PlanSchemaValidator();
        _llmProvider = llmProvider;
    }

    public async Task<AiEngineResult> GenerateAsync(
        DslGenerateRequest request,
        string correlationId,
        CancellationToken ct)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var planSource = "unknown"; // Track origin: "llm", "template", "explicit"
        AiLlmProvider.PlanErrorCategory? llmErrorCategory = null;
        long llmLatencyMs = 0;

        try
        {
            _logger.Information(
                "PlanV1 engine invoked: CorrelationId={CorrelationId}, GoalLength={GoalLength}, HasLlm={HasLlm}",
                correlationId, request.GoalText.Length, _llmProvider != null);

            // 1. Discover recordPath candidates
            var discovery = RecordPathDiscovery.Discover(request.SampleInput, request.GoalText);
            string recordPath;
            IReadOnlyList<string>? candidatePaths = null;

            if (request.Hints?.TryGetValue("recordPath", out var hintPath) == true && !string.IsNullOrEmpty(hintPath))
            {
                recordPath = hintPath;
                _logger.Debug("Using hint recordPath: {RecordPath}", recordPath);
            }
            else if (discovery.Success)
            {
                recordPath = discovery.RecordPath!;
                candidatePaths = discovery.Candidates?.Take(3).Select(c => c.Path).ToList();
                _logger.Debug("Discovered recordPath: {RecordPath} (score={Score})", recordPath, discovery.Score);
            }
            else
            {
                // Can't find recordPath - this is an error
                _logger.Warning("RecordPath discovery failed: {Error}", discovery.Error);
                return CreateError(correlationId, "RECORDPATH_NOT_FOUND", 
                    $"Could not find a suitable array in the input data. {discovery.Error}");
            }

            // 2. Get or generate plan
            TransformPlan plan;

            // Check if explicit plan provided in hints
            if (request.Hints?.TryGetValue("plan", out var planJson) == true && !string.IsNullOrEmpty(planJson))
            {
                planSource = "explicit";
                var parseResult = TryParsePlan(planJson);
                if (!parseResult.Success)
                {
                    return CreateError(correlationId, "PLAN_INVALID", parseResult.Error!);
                }
                plan = parseResult.Plan!;
                _logger.Debug("Using explicit plan from hints");
            }
            else
            {
                // Try LLM first (if available)
                TransformPlan? llmPlan = null;
                if (_llmProvider != null)
                {
                    var structureAnalysis = AnalyzeJsonStructure(request.SampleInput);
                    var llmResult = await _llmProvider.GeneratePlanAsync(
                        request.GoalText,
                        request.SampleInput,
                        candidatePaths,
                        structureAnalysis,
                        ct);

                    llmLatencyMs = llmResult.LatencyMs;
                    llmErrorCategory = llmResult.ErrorCategory;

                    if (llmResult.Success && llmResult.Plan != null)
                    {
                        // Validate recordPath exists
                        var llmRecordPath = llmResult.Plan.Source.RecordPath;
                        if (!string.IsNullOrEmpty(llmRecordPath))
                        {
                            var pathCheck = ShapeNormalizer.NavigateToPath(request.SampleInput, llmRecordPath);
                            if (pathCheck == null || pathCheck.Value.ValueKind != JsonValueKind.Array)
                            {
                                _logger.Warning("LLM plan has invalid recordPath: {Path}, falling back", llmRecordPath);
                                llmErrorCategory = AiLlmProvider.PlanErrorCategory.RecordPathNotFound;
                            }
                            else
                            {
                                llmPlan = llmResult.Plan;
                                planSource = "llm";
                                _logger.Information("LLM generated valid plan: Steps={StepCount}, LatencyMs={Latency}",
                                    llmPlan.Steps.Count, llmLatencyMs);
                            }
                        }
                        else
                        {
                            // LLM omitted recordPath - use discovered one
                            llmPlan = llmResult.Plan with
                            {
                                Source = new PlanSource { RecordPath = recordPath }
                            };
                            planSource = "llm";
                            _logger.Information("LLM plan using discovered recordPath: {Path}", recordPath);
                        }
                    }
                    else
                    {
                        _logger.Warning(
                            "LLM plan generation failed: Error={Error}, Category={Category}, LatencyMs={Latency}",
                            llmResult.Error, llmResult.ErrorCategory, llmLatencyMs);
                    }
                }

                if (llmPlan != null)
                {
                    plan = llmPlan;
                }
                else
                {
                    // Fallback to template matching
                    var templateResult = PlanTemplates.TryMatchAndGenerate(
                        request.GoalText,
                        request.SampleInput,
                        recordPath,
                        _logger);

                    if (templateResult.Matched && templateResult.Plan != null)
                    {
                        plan = templateResult.Plan;
                        planSource = $"template:{templateResult.TemplateId}";
                        _logger.Information("Using template plan: Template={Template}, Reason={Reason}",
                            templateResult.TemplateId, templateResult.Reason);
                    }
                    else
                    {
                        // Last resort: build default select-all plan
                        plan = BuildDefaultPlan(recordPath, request);
                        planSource = "default";
                        _logger.Information("Using default select-all plan");
                    }
                }
            }

            // 3. Resolve field aliases
            var sampleRecord = GetSampleRecord(request.SampleInput, plan.Source.RecordPath);
            if (sampleRecord.HasValue)
            {
                var resolvedSteps = new List<PlanStep>();
                foreach (var step in plan.Steps)
                {
                    var (resolved, warnings) = FieldResolver.ResolveStep(step, sampleRecord.Value);
                    resolvedSteps.Add(resolved);
                    // Log warnings but don't fail
                    foreach (var w in warnings)
                    {
                        _logger.Debug("Field resolution warning: {Warning}", w);
                    }
                }
                plan = plan with { Steps = resolvedSteps };
            }

            // 4. Execute plan
            var execResult = PlanExecutor.Execute(plan, request.SampleInput, _logger);
            if (!execResult.Success)
            {
                _logger.Warning("Plan execution failed: {Error}", execResult.Error);
                
                // If LLM plan failed, try template fallback
                if (planSource == "llm")
                {
                    _logger.Information("LLM plan execution failed, trying template fallback");
                    var templateResult = PlanTemplates.TryMatchAndGenerate(
                        request.GoalText,
                        request.SampleInput,
                        recordPath,
                        _logger);

                    if (templateResult.Matched && templateResult.Plan != null)
                    {
                        plan = templateResult.Plan;
                        planSource = $"template:{templateResult.TemplateId}";
                        execResult = PlanExecutor.Execute(plan, request.SampleInput, _logger);
                        
                        if (!execResult.Success)
                        {
                            return CreateError(correlationId, "PLAN_EXECUTION_ERROR", execResult.Error!);
                        }
                    }
                    else
                    {
                        return CreateError(correlationId, "PLAN_EXECUTION_ERROR", execResult.Error!);
                    }
                }
                else
                {
                    return CreateError(correlationId, "PLAN_EXECUTION_ERROR", execResult.Error!);
                }
            }

            var rows = execResult.Rows!;
            _logger.Debug("Plan executed: {RowCount} rows produced", rows.Count);

            // 5. Normalize shape to array<object>
            var outputArray = ShapeNormalizer.ToJsonElement(rows);

            // 6. Infer permissive schema
            var outputSchema = PermissiveSchemaInference.InferSchema(rows);

            // 7. Generate CSV preview
            string? csvPreview = null;
            try
            {
                var transformResult = _engine.TransformValidateToCsvFromRows(
                    outputArray,
                    outputSchema);

                if (transformResult.IsValid)
                {
                    csvPreview = transformResult.CsvPreview;
                }
            }
            catch (Exception ex)
            {
                _logger.Warning(ex, "CSV generation failed (non-fatal)");
                execResult.Warnings.Add($"CSV generation failed: {ex.Message}");
            }

            stopwatch.Stop();

            // 8. Build response with observability data
            var planText = JsonSerializer.Serialize(plan, new JsonSerializerOptions { WriteIndented = false });
            var result = new DslGenerateResult
            {
                Dsl = new DslOutput
                {
                    Profile = "ir",
                    Text = planText
                },
                Plan = JsonSerializer.SerializeToElement(plan),
                OutputSchema = outputSchema,
                ExampleRows = outputArray,
                Rationale = BuildRationale(planSource, recordPath, rows.Count, llmLatencyMs, llmErrorCategory),
                Warnings = execResult.Warnings,
                ModelInfo = new ModelInfo
                {
                    Provider = planSource.StartsWith("llm") ? "llm" : "deterministic",
                    Model = planSource.StartsWith("llm") ? "ir_llm" : "ir_template",
                    PromptVersion = "1.0"
                }
            };

            _logger.Information(
                "AI engine success: CorrelationId={CorrelationId}, PlanSource={PlanSource}, Rows={RowCount}, TotalLatency={Latency}ms, LlmLatency={LlmLatency}ms",
                correlationId, planSource, rows.Count, stopwatch.ElapsedMilliseconds, llmLatencyMs);

            return AiEngineResult.Ok(result, "ir");
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.Error(ex, "AI engine unexpected error: CorrelationId={CorrelationId}", correlationId);
            
            // Never return 502 - return 400 with clear message
            return CreateError(correlationId, "AI_ERROR", 
                $"Transformation failed. Please simplify your goal or provide more specific instructions. Details: {ex.Message}");
        }
    }

    private (bool Success, TransformPlan? Plan, string? Error) TryParsePlan(string planJson)
    {
        try
        {
            var plan = JsonSerializer.Deserialize<TransformPlan>(planJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (plan == null)
            {
                return (false, null, "Failed to deserialize plan");
            }

            var validation = _planValidator.ValidateJson(planJson);
            if (!validation.IsValid)
            {
                return (false, null, $"Plan schema validation failed: {string.Join("; ", validation.Errors)}");
            }

            return (true, plan, null);
        }
        catch (JsonException ex)
        {
            return (false, null, $"Invalid plan JSON: {ex.Message}");
        }
    }

    private TransformPlan BuildDefaultPlan(string recordPath, DslGenerateRequest request)
    {
        var fields = new List<FieldSpec>();
        var sampleRecord = GetSampleRecord(request.SampleInput, recordPath);

        if (sampleRecord.HasValue && sampleRecord.Value.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in sampleRecord.Value.EnumerateObject())
            {
                // Skip complex nested objects/arrays
                if (prop.Value.ValueKind == JsonValueKind.Object || prop.Value.ValueKind == JsonValueKind.Array)
                    continue;

                fields.Add(new FieldSpec
                {
                    From = $"/{prop.Name}",
                    As = prop.Name
                });
            }
        }

        var steps = new List<PlanStep>();
        if (fields.Count > 0)
        {
            steps.Add(new PlanStep
            {
                Op = PlanOps.Select,
                Fields = fields
            });
        }

        if (request.Constraints.MaxColumns > 0 && fields.Count > request.Constraints.MaxColumns)
        {
            steps.Add(new PlanStep
            {
                Op = PlanOps.Limit,
                N = request.Constraints.MaxColumns
            });
        }

        return new TransformPlan
        {
            PlanVersion = "1.0",
            Source = new PlanSource { RecordPath = recordPath },
            Steps = steps.Count > 0 ? steps : new List<PlanStep>
            {
                new PlanStep { Op = PlanOps.Select, Fields = new List<FieldSpec>() }
            }
        };
    }

    private static JsonElement? GetSampleRecord(JsonElement input, string recordPath)
    {
        var array = ShapeNormalizer.NavigateToPath(input, recordPath);
        if (array == null || array.Value.ValueKind != JsonValueKind.Array)
            return null;

        if (array.Value.GetArrayLength() == 0)
            return null;

        return array.Value.EnumerateArray().First();
    }

    private static string BuildRationale(
        string planSource,
        string recordPath,
        int rowCount,
        long llmLatencyMs,
        AiLlmProvider.PlanErrorCategory? llmErrorCategory)
    {
        var parts = new List<string>
        {
            $"Plan source: {planSource}",
            $"RecordPath: {recordPath}",
            $"{rowCount} rows produced"
        };

        if (llmLatencyMs > 0)
        {
            parts.Add($"LLM latency: {llmLatencyMs}ms");
        }

        if (llmErrorCategory.HasValue && llmErrorCategory != AiLlmProvider.PlanErrorCategory.None)
        {
            parts.Add($"LLM fallback reason: {llmErrorCategory}");
        }

        return string.Join(". ", parts) + ".";
    }

    private static string AnalyzeJsonStructure(JsonElement element, string currentPath = "", int depth = 0, int maxDepth = 3)
    {
        if (depth > maxDepth) return "";

        var sb = new System.Text.StringBuilder();
        var indent = new string(' ', depth * 2);

        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    var propPath = string.IsNullOrEmpty(currentPath) ? $"/{prop.Name}" : $"{currentPath}/{prop.Name}";

                    if (prop.Value.ValueKind == JsonValueKind.Array)
                    {
                        var len = prop.Value.GetArrayLength();
                        sb.AppendLine($"{indent}• {propPath} : Array[{len}]");
                        if (len > 0 && prop.Value[0].ValueKind == JsonValueKind.Object)
                        {
                            sb.AppendLine($"{indent}  Fields:");
                            foreach (var itemProp in prop.Value[0].EnumerateObject().Take(10))
                            {
                                var typeName = GetTypeName(itemProp.Value);
                                sb.AppendLine($"{indent}    - {itemProp.Name}: {typeName}");
                            }
                        }
                    }
                    else if (prop.Value.ValueKind == JsonValueKind.Object)
                    {
                        sb.AppendLine($"{indent}• {propPath} : Object");
                        sb.Append(AnalyzeJsonStructure(prop.Value, propPath, depth + 1, maxDepth));
                    }
                    else
                    {
                        sb.AppendLine($"{indent}• {propPath} : {GetTypeName(prop.Value)}");
                    }
                }
                break;

            case JsonValueKind.Array:
                var arrLen = element.GetArrayLength();
                sb.AppendLine($"{indent}• / : Array[{arrLen}] (root is array)");
                if (arrLen > 0 && element[0].ValueKind == JsonValueKind.Object)
                {
                    sb.AppendLine($"{indent}  Fields:");
                    foreach (var itemProp in element[0].EnumerateObject().Take(10))
                    {
                        sb.AppendLine($"{indent}    - {itemProp.Name}: {GetTypeName(itemProp.Value)}");
                    }
                }
                break;
        }

        return sb.ToString();
    }

    private static string GetTypeName(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.String => "String",
        JsonValueKind.Number => "Number",
        JsonValueKind.True => "Boolean",
        JsonValueKind.False => "Boolean",
        JsonValueKind.Null => "Null",
        JsonValueKind.Array => $"Array[{e.GetArrayLength()}]",
        JsonValueKind.Object => "Object",
        _ => "Unknown"
    };

    private static AiEngineResult CreateError(string correlationId, string code, string message)
    {
        var error = new AiError
        {
            Code = code,
            Message = message,
            CorrelationId = correlationId
        };

        // Map to HTTP status - never 502
        var statusCode = code switch
        {
            "RECORDPATH_NOT_FOUND" => 400,
            "PLAN_INVALID" => 400,
            "PLAN_PARSE_ERROR" => 400,
            "PLAN_EXECUTION_ERROR" => 422,
            "PLAN_V1_ERROR" => 400, // Changed from 500 to 400
            _ => 400 // Default to 400, never 502
        };

        return AiEngineResult.Fail(error, statusCode);
    }

    /// <summary>
    /// Simple heuristic to detect if a goal might be suitable for plan_v1.
    /// </summary>
    public static bool IsSuitableForPlanV1(string goalText, JsonElement sampleInput)
    {
        var goal = goalText.ToLowerInvariant();

        var supportedOperations = new[]
        {
            "extract", "select", "pick", "extrair", "selecionar",
            "rename", "map", "renomear", "mapear",
            "filter", "where", "filtrar", "onde",
            "sort", "order", "ordenar",
            "group", "aggregate", "agrupar", "agregar",
            "sum", "avg", "average", "count", "min", "max",
            "soma", "média", "media", "contar", "total"
        };

        var hasSimpleOp = supportedOperations.Any(op => goal.Contains(op));
        if (!hasSimpleOp)
            return false;

        if (sampleInput.ValueKind == JsonValueKind.Array)
            return true;

        if (sampleInput.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in sampleInput.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.Array)
                    return true;
            }
        }

        return false;
    }
}
