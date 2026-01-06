using Serilog;

namespace Metrics.Api.AI.Engines;

/// <summary>
/// Routes DSL generation requests to the appropriate engine based on:
/// 1. Explicit engine field in request
/// 2. Default engine from configuration
/// 3. Heuristics for auto mode
/// </summary>
public class AiEngineRouter
{
    private readonly LegacyAiDslEngine _legacyEngine;
    private readonly PlanV1AiEngine _planV1Engine;
    private readonly AiConfiguration _config;
    private readonly Serilog.ILogger _logger;

    public AiEngineRouter(
        LegacyAiDslEngine legacyEngine,
        PlanV1AiEngine planV1Engine,
        AiConfiguration config,
        Serilog.ILogger logger)
    {
        _legacyEngine = legacyEngine;
        _planV1Engine = planV1Engine;
        _config = config;
        _logger = logger;
    }

    /// <summary>
    /// Select and execute the appropriate engine for the request
    /// </summary>
    public async Task<AiEngineResult> RouteAndExecuteAsync(
        DslGenerateRequest request,
        string correlationId,
        CancellationToken ct)
    {
        var selectedEngine = SelectEngine(request, correlationId);

        _logger.Information(
            "Engine routing: CorrelationId={CorrelationId}, RequestedEngine={RequestedEngine}, SelectedEngine={SelectedEngine}",
            correlationId,
            request.Engine ?? "(not specified)",
            selectedEngine.EngineType);

        return await selectedEngine.GenerateAsync(request, correlationId, ct);
    }

    /// <summary>
    /// Select the engine to use based on request and configuration
    /// </summary>
    public IAiTransformationEngine SelectEngine(DslGenerateRequest request, string? correlationId = null)
    {
        // 1. Validate engine field if provided
        var requestedEngine = request.Engine;
        if (!string.IsNullOrEmpty(requestedEngine) && !EngineType.IsValid(requestedEngine))
        {
            _logger.Warning(
                "Invalid engine requested: {Engine}. Falling back to default. CorrelationId={CorrelationId}",
                requestedEngine, correlationId);
            requestedEngine = null;
        }

        // 2. Determine effective engine
        var effectiveEngine = requestedEngine ?? _config.DefaultEngine;

        // 3. Handle auto mode
        if (effectiveEngine == EngineType.Auto)
        {
            effectiveEngine = ResolveAutoMode(request, correlationId);
        }

        // 4. Return the appropriate engine
        return effectiveEngine switch
        {
            EngineType.PlanV1 => _planV1Engine,
            _ => _legacyEngine  // Default to legacy for any unknown value
        };
    }

    /// <summary>
    /// Resolve auto mode to a specific engine based on heuristics
    /// </summary>
    private string ResolveAutoMode(DslGenerateRequest request, string? correlationId)
    {
        // Use plan_v1 heuristic to check if suitable
        var suitableForPlanV1 = PlanV1AiEngine.IsSuitableForPlanV1(request.GoalText, request.SampleInput);

        if (suitableForPlanV1)
        {
            _logger.Information(
                "Auto mode selected plan_v1 (heuristic match). CorrelationId={CorrelationId}",
                correlationId);

            // For MVP, still use legacy even if heuristic matches
            // because plan_v1 is not implemented yet
            _logger.Information(
                "plan_v1 not yet implemented, falling back to legacy. CorrelationId={CorrelationId}",
                correlationId);
            return EngineType.Legacy;
        }

        _logger.Information(
            "Auto mode selected legacy (no heuristic match). CorrelationId={CorrelationId}",
            correlationId);

        return EngineType.Legacy;
    }

    /// <summary>
    /// Get the resolved engine type for logging purposes
    /// </summary>
    public string GetResolvedEngineType(DslGenerateRequest request) =>
        SelectEngine(request).EngineType;
}
