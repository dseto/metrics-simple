using Serilog;

namespace Metrics.Api.AI.Engines;

/// <summary>
/// Plan V1 engine using deterministic IR (Intermediate Representation) execution.
/// TODO: This is a stub implementation. The actual plan_v1 engine will be implemented later.
/// </summary>
public class PlanV1AiEngine : IAiTransformationEngine
{
    private readonly Serilog.ILogger _logger;

    public string EngineType => AI.EngineType.PlanV1;

    public PlanV1AiEngine(Serilog.ILogger logger)
    {
        _logger = logger;
    }

    public Task<AiEngineResult> GenerateAsync(
        DslGenerateRequest request,
        string correlationId,
        CancellationToken ct)
    {
        _logger.Information(
            "PlanV1 engine invoked (STUB): CorrelationId={CorrelationId}, GoalLength={GoalLength}",
            correlationId, request.GoalText.Length);

        // Return 501 Not Implemented for now
        // This stub will be replaced with actual IR/executor logic in a future iteration
        var error = new AiError
        {
            Code = "ENGINE_NOT_IMPLEMENTED",
            Message = "The plan_v1 engine is not yet implemented. Use engine=legacy or engine=auto for now.",
            CorrelationId = correlationId
        };

        return Task.FromResult(AiEngineResult.Fail(error, 501));
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
