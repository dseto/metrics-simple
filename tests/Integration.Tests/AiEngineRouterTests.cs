using System.Text.Json;
using FluentAssertions;
using Metrics.Api.AI;
using Metrics.Api.AI.Engines;
using Metrics.Engine;
using Serilog;
using Xunit;

namespace Integration.Tests;

/// <summary>
/// Unit tests for AiEngineRouter - verifies engine selection logic.
/// </summary>
public class AiEngineRouterTests
{
    private readonly ILogger _testLogger = new LoggerConfiguration().CreateLogger();
    
    private AiEngineRouter CreateRouter(string defaultEngine = EngineType.Legacy)
    {
        var config = new AiConfiguration
        {
            Enabled = true,
            DefaultEngine = defaultEngine
        };
        
        // Create mock/stub engines
        var legacyEngine = CreateLegacyEngineMock();
        var planV1Engine = new PlanV1AiEngine(_testLogger);
        
        return new AiEngineRouter(legacyEngine, planV1Engine, config, _testLogger);
    }
    
    private LegacyAiDslEngine CreateLegacyEngineMock()
    {
        // For routing tests, we just need the engine to exist - not actually execute
        var mockProvider = new MockAiProvider();
        var config = new AiConfiguration { Enabled = true };
        var transformer = new JsonataTransformer();
        var validator = new SchemaValidator();
        var csvGenerator = new CsvGenerator();
        var engineService = new EngineService(transformer, validator, csvGenerator);
        
        return new LegacyAiDslEngine(mockProvider, config, engineService, _testLogger);
    }
    
    private DslGenerateRequest CreateRequest(string? engine = null)
    {
        return new DslGenerateRequest
        {
            GoalText = "Extract name and value fields",
            SampleInput = JsonSerializer.SerializeToElement(new[] 
            { 
                new { name = "test", value = 123 } 
            }),
            DslProfile = "jsonata",
            Constraints = new DslConstraints(),
            Engine = engine
        };
    }

    [Fact]
    public void SelectEngine_NoEngineSpecified_UsesDefaultLegacy()
    {
        // Arrange
        var router = CreateRouter(defaultEngine: EngineType.Legacy);
        var request = CreateRequest(engine: null);

        // Act
        var selected = router.SelectEngine(request);

        // Assert
        selected.EngineType.Should().Be(EngineType.Legacy);
    }

    [Fact]
    public void SelectEngine_ExplicitLegacy_ReturnsLegacyEngine()
    {
        // Arrange
        var router = CreateRouter();
        var request = CreateRequest(engine: EngineType.Legacy);

        // Act
        var selected = router.SelectEngine(request);

        // Assert
        selected.EngineType.Should().Be(EngineType.Legacy);
    }

    [Fact]
    public void SelectEngine_ExplicitPlanV1_ReturnsPlanV1Engine()
    {
        // Arrange
        var router = CreateRouter();
        var request = CreateRequest(engine: EngineType.PlanV1);

        // Act
        var selected = router.SelectEngine(request);

        // Assert
        selected.EngineType.Should().Be(EngineType.PlanV1);
    }

    [Fact]
    public void SelectEngine_AutoMode_FallsBackToLegacy_WhenPlanV1NotImplemented()
    {
        // Arrange
        var router = CreateRouter();
        var request = CreateRequest(engine: EngineType.Auto);

        // Act
        var selected = router.SelectEngine(request);

        // Assert
        // Auto mode should select legacy for MVP since plan_v1 is not implemented
        selected.EngineType.Should().Be(EngineType.Legacy);
    }

    [Fact]
    public void SelectEngine_InvalidEngine_FallsBackToDefault()
    {
        // Arrange
        var router = CreateRouter(defaultEngine: EngineType.Legacy);
        var request = CreateRequest(engine: "invalid_engine");

        // Act
        var selected = router.SelectEngine(request);

        // Assert
        selected.EngineType.Should().Be(EngineType.Legacy);
    }

    [Fact]
    public void SelectEngine_DefaultPlanV1_UsesPlanV1WhenNoEngineSpecified()
    {
        // Arrange
        var router = CreateRouter(defaultEngine: EngineType.PlanV1);
        var request = CreateRequest(engine: null);

        // Act
        var selected = router.SelectEngine(request);

        // Assert
        selected.EngineType.Should().Be(EngineType.PlanV1);
    }

    [Fact]
    public void GetResolvedEngineType_ReturnsCorrectType()
    {
        // Arrange
        var router = CreateRouter();

        // Act & Assert
        router.GetResolvedEngineType(CreateRequest(EngineType.Legacy))
            .Should().Be(EngineType.Legacy);
        
        router.GetResolvedEngineType(CreateRequest(EngineType.PlanV1))
            .Should().Be(EngineType.PlanV1);
    }

    [Fact]
    public void EngineType_IsValid_ReturnsTrueForValidValues()
    {
        EngineType.IsValid(null).Should().BeTrue();
        EngineType.IsValid("").Should().BeTrue();
        EngineType.IsValid(EngineType.Legacy).Should().BeTrue();
        EngineType.IsValid(EngineType.PlanV1).Should().BeTrue();
        EngineType.IsValid(EngineType.Auto).Should().BeTrue();
    }

    [Fact]
    public void EngineType_IsValid_ReturnsFalseForInvalidValues()
    {
        EngineType.IsValid("invalid").Should().BeFalse();
        EngineType.IsValid("LEGACY").Should().BeFalse(); // Case sensitive
        EngineType.IsValid("plan_v2").Should().BeFalse();
    }

    [Fact]
    public void PlanV1Heuristic_DetectsSimpleExtractOperations()
    {
        // Arrange - Array at root level with simple extraction goal
        var input = JsonSerializer.SerializeToElement(new[]
        {
            new { name = "test", value = 123 }
        });

        // Act
        var suitable = PlanV1AiEngine.IsSuitableForPlanV1("Extract the name field", input);

        // Assert
        suitable.Should().BeTrue();
    }

    [Fact]
    public void PlanV1Heuristic_DetectsGroupOperations()
    {
        // Arrange
        var input = JsonSerializer.SerializeToElement(new[]
        {
            new { category = "A", amount = 100 },
            new { category = "B", amount = 200 }
        });

        // Act
        var suitable = PlanV1AiEngine.IsSuitableForPlanV1("Group by category and sum amounts", input);

        // Assert
        suitable.Should().BeTrue();
    }

    [Fact]
    public void PlanV1Heuristic_RejectsComplexGoals()
    {
        // Arrange - No simple operation keywords
        var input = JsonSerializer.SerializeToElement(new[]
        {
            new { data = "test" }
        });

        // Act
        var suitable = PlanV1AiEngine.IsSuitableForPlanV1("Perform complex transformation with custom logic", input);

        // Assert
        suitable.Should().BeFalse();
    }

    [Fact]
    public void PlanV1Heuristic_RejectsNonArrayInput()
    {
        // Arrange - Simple object, no array
        var input = JsonSerializer.SerializeToElement(new { name = "test" });

        // Act
        var suitable = PlanV1AiEngine.IsSuitableForPlanV1("Extract name", input);

        // Assert
        suitable.Should().BeFalse();
    }

    [Fact]
    public void PlanV1Heuristic_DetectsNestedArray()
    {
        // Arrange - Object with array property
        var input = JsonSerializer.SerializeToElement(new
        {
            results = new[]
            {
                new { name = "test" }
            }
        });

        // Act
        var suitable = PlanV1AiEngine.IsSuitableForPlanV1("Select name from results", input);

        // Assert
        suitable.Should().BeTrue();
    }
}
