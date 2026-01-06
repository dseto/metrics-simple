using System.Text.Json;
using Metrics.Api.AI.Engines.PlanV1;
using Metrics.Engine;
using Serilog;

namespace Metrics.Api.AI.Engines;

/// <summary>
/// Plan V1 engine using deterministic IR (Intermediate Representation) execution.
/// Executes transformation plans without LLM - fully deterministic.
/// </summary>
public class PlanV1AiEngine : IAiTransformationEngine
{
    private readonly Serilog.ILogger _logger;
    private readonly EngineService _engine;
    private readonly PlanSchemaValidator _planValidator;

    public string EngineType => AI.EngineType.PlanV1;

    public PlanV1AiEngine(Serilog.ILogger logger, EngineService engine)
    {
        _logger = logger;
        _engine = engine;
        _planValidator = new PlanSchemaValidator();
    }

    public Task<AiEngineResult> GenerateAsync(
        DslGenerateRequest request,
        string correlationId,
        CancellationToken ct)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.Information(
                "PlanV1 engine invoked: CorrelationId={CorrelationId}, GoalLength={GoalLength}",
                correlationId, request.GoalText.Length);

            // 1. Discover recordPath if not provided in hints
            string? recordPath = null;
            if (request.Hints?.TryGetValue("recordPath", out recordPath) != true || string.IsNullOrEmpty(recordPath))
            {
                var discovery = RecordPathDiscovery.Discover(request.SampleInput, request.GoalText);
                if (!discovery.Success)
                {
                    _logger.Warning("RecordPath discovery failed: {Error}", discovery.Error);
                    return Task.FromResult(CreateError(correlationId, "RECORDPATH_NOT_FOUND", discovery.Error!));
                }
                recordPath = discovery.RecordPath!;
                _logger.Debug("Discovered recordPath: {RecordPath} (score={Score})", recordPath, discovery.Score);
            }

            // 2. Build or parse plan
            TransformPlan plan;
            if (request.Hints?.TryGetValue("plan", out var planJson) == true && !string.IsNullOrEmpty(planJson))
            {
                // Parse provided plan
                try
                {
                    plan = JsonSerializer.Deserialize<TransformPlan>(planJson, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    })!;

                    // Validate plan schema
                    var validation = _planValidator.ValidateJson(planJson);
                    if (!validation.IsValid)
                    {
                        var errorMsg = string.Join("; ", validation.Errors);
                        _logger.Warning("Plan validation failed: {Errors}", errorMsg);
                        return Task.FromResult(CreateError(correlationId, "PLAN_INVALID", $"Plan schema validation failed: {errorMsg}"));
                    }
                }
                catch (JsonException ex)
                {
                    _logger.Warning(ex, "Failed to parse plan JSON");
                    return Task.FromResult(CreateError(correlationId, "PLAN_PARSE_ERROR", $"Failed to parse plan: {ex.Message}"));
                }
            }
            else
            {
                // Build a simple default plan based on goal heuristics
                plan = BuildDefaultPlan(recordPath, request);
            }

            // 3. Execute plan
            var execResult = PlanExecutor.Execute(plan, request.SampleInput, _logger);
            if (!execResult.Success)
            {
                _logger.Warning("Plan execution failed: {Error}", execResult.Error);
                return Task.FromResult(CreateError(correlationId, "PLAN_EXECUTION_ERROR", execResult.Error!));
            }

            var rows = execResult.Rows!;
            _logger.Debug("Plan executed: {RowCount} rows produced", rows.Count);

            // 4. Normalize shape to array<object>
            var outputArray = ShapeNormalizer.ToJsonElement(rows);

            // 5. Infer permissive schema
            var outputSchema = PermissiveSchemaInference.InferSchema(rows);

            // 6. Generate CSV preview
            string? csvPreview = null;
            try
            {
                var transformResult = _engine.TransformValidateToCsv(
                    outputArray,
                    "jsonata",
                    "$", // Identity transform - already have the output
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

            // 7. Build response
            var result = new DslGenerateResult
            {
                Dsl = new DslOutput
                {
                    Profile = "plan_v1",
                    Text = "<plan_v1>" // Placeholder - actual DSL is the plan
                },
                OutputSchema = outputSchema,
                ExampleRows = outputArray,
                Rationale = $"Deterministic plan execution. RecordPath: {recordPath}. {rows.Count} rows produced.",
                Warnings = execResult.Warnings,
                EngineUsed = AI.EngineType.PlanV1,
                Plan = request.IncludePlan ? JsonSerializer.SerializeToElement(plan) : null,
                ModelInfo = new ModelInfo
                {
                    Provider = "deterministic",
                    Model = "plan_v1",
                    PromptVersion = "1.0"
                }
            };

            _logger.Information(
                "PlanV1 engine success: CorrelationId={CorrelationId}, Rows={RowCount}, Latency={Latency}ms",
                correlationId, rows.Count, stopwatch.ElapsedMilliseconds);

            return Task.FromResult(AiEngineResult.Ok(result, AI.EngineType.PlanV1));
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.Error(ex, "PlanV1 engine unexpected error: CorrelationId={CorrelationId}", correlationId);
            return Task.FromResult(CreateError(correlationId, "PLAN_V1_ERROR", $"Unexpected error: {ex.Message}"));
        }
    }

    /// <summary>
    /// Builds a simple default plan when no explicit plan is provided.
    /// For MVP, this creates a basic select-all plan.
    /// </summary>
    private TransformPlan BuildDefaultPlan(string recordPath, DslGenerateRequest request)
    {
        // Extract all fields from sample record
        var fields = new List<FieldSpec>();
        var sampleRecord = GetSampleRecord(request.SampleInput, recordPath);

        if (sampleRecord.HasValue && sampleRecord.Value.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in sampleRecord.Value.EnumerateObject())
            {
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

        // Apply limit if specified in constraints
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

    private static AiEngineResult CreateError(string correlationId, string code, string message)
    {
        var error = new AiError
        {
            Code = code,
            Message = message,
            CorrelationId = correlationId
        };

        // Map error codes to HTTP status codes
        var statusCode = code switch
        {
            "RECORDPATH_NOT_FOUND" => 400,
            "PLAN_INVALID" => 400,
            "PLAN_PARSE_ERROR" => 400,
            "PLAN_EXECUTION_ERROR" => 422,
            _ => 500
        };

        return AiEngineResult.Fail(error, statusCode);
    }

    /// <summary>
    /// Simple heuristic to detect if a goal might be suitable for plan_v1.
    /// This is used by auto mode to decide which engine to use.
    /// </summary>
    /// <param name="goalText">The user's transformation goal</param>
    /// <param name="sampleInput">Sample JSON input</param>
    /// <returns>True if the goal seems suitable for deterministic plan execution</returns>
    public static bool IsSuitableForPlanV1(string goalText, System.Text.Json.JsonElement sampleInput)
    {
        // MVP heuristic: Check for common operations that plan_v1 can handle
        var goal = goalText.ToLowerInvariant();

        // Check for supported operation keywords
        var supportedOperations = new[]
        {
            "extract", "select", "pick",           // Extract/select fields
            "rename", "map",                        // Rename fields
            "filter", "where",                      // Filter rows
            "sort", "order",                        // Sort
            "group", "aggregate",                   // Group operations
            "sum", "avg", "average", "count", "min", "max"  // Aggregations
        };

        var hasSimpleOp = supportedOperations.Any(op => goal.Contains(op));
        if (!hasSimpleOp)
            return false;

        // Check if sample input has a clear array to operate on
        if (sampleInput.ValueKind == System.Text.Json.JsonValueKind.Array)
            return true;

        if (sampleInput.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            // Look for an array property at root level
            foreach (var prop in sampleInput.EnumerateObject())
            {
                if (prop.Value.ValueKind == System.Text.Json.JsonValueKind.Array)
                    return true;
            }
        }

        return false;
    }
}
