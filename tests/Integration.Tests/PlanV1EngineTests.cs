using System.Text.Json;
using FluentAssertions;
using Metrics.Api.AI.Engines.Ai;
using Xunit;

namespace Integration.Tests;

/// <summary>
/// Unit tests for AI engine components (formerly PlanV1).
/// </summary>
public class PlanV1EngineTests
{
    #region RecordPathDiscovery Tests

    [Fact]
    public void RecordPathDiscovery_RootArray_ReturnsRootPath()
    {
        // Arrange
        var input = JsonSerializer.SerializeToElement(new[]
        {
            new { id = 1, name = "Item 1" },
            new { id = 2, name = "Item 2" }
        });

        // Act
        var result = RecordPathDiscovery.Discover(input);

        // Assert
        result.Success.Should().BeTrue();
        result.RecordPath.Should().Be("/");
        result.Score.Should().BeGreaterThan(0);
    }

    [Fact]
    public void RecordPathDiscovery_NestedItems_FindsItemsPath()
    {
        // Arrange
        var input = JsonSerializer.SerializeToElement(new
        {
            status = "ok",
            items = new[]
            {
                new { id = 1, name = "Item 1" },
                new { id = 2, name = "Item 2" }
            }
        });

        // Act
        var result = RecordPathDiscovery.Discover(input);

        // Assert
        result.Success.Should().BeTrue();
        result.RecordPath.Should().Be("/items");
    }

    [Fact]
    public void RecordPathDiscovery_NestedResults_FindsResultsPath()
    {
        // Arrange
        var input = JsonSerializer.SerializeToElement(new
        {
            meta = new { count = 2 },
            results = new[]
            {
                new { city = "NYC", temp = 20 },
                new { city = "LA", temp = 25 }
            }
        });

        // Act
        var result = RecordPathDiscovery.Discover(input);

        // Assert
        result.Success.Should().BeTrue();
        result.RecordPath.Should().Be("/results");
    }

    [Fact]
    public void RecordPathDiscovery_MultipleArrays_ChoosesBestCandidate()
    {
        // Arrange - products has more items and is well-known name
        var input = JsonSerializer.SerializeToElement(new
        {
            tags = new[] { "a", "b" }, // Array of primitives - should lose
            products = new[] // Array of objects - should win
            {
                new { id = 1, name = "Product 1" },
                new { id = 2, name = "Product 2" },
                new { id = 3, name = "Product 3" }
            }
        });

        // Act
        var result = RecordPathDiscovery.Discover(input);

        // Assert
        result.Success.Should().BeTrue();
        result.RecordPath.Should().Be("/products");
        result.Candidates.Count.Should().BeGreaterOrEqualTo(2);
    }

    [Fact]
    public void RecordPathDiscovery_NoArray_ReturnsError()
    {
        // Arrange
        var input = JsonSerializer.SerializeToElement(new
        {
            name = "Test",
            value = 123
        });

        // Act
        var result = RecordPathDiscovery.Discover(input);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("NoRecordsetFound");
    }

    [Fact]
    public void RecordPathDiscovery_GoalMatching_BoostsRelevantPath()
    {
        // Arrange
        var input = JsonSerializer.SerializeToElement(new
        {
            users = new[] { new { id = 1 } },
            forecast = new[] { new { day = "Mon", temp = 20 } }
        });

        // Act - goal mentions forecast
        var result = RecordPathDiscovery.Discover(input, "Extract weather forecast data");

        // Assert
        result.Success.Should().BeTrue();
        result.RecordPath.Should().Be("/forecast");
    }

    #endregion

    #region ShapeNormalizer Tests

    [Fact]
    public void ShapeNormalizer_Array_ReturnsRows()
    {
        // Arrange
        var input = JsonSerializer.SerializeToElement(new[]
        {
            new { id = 1, name = "A" },
            new { id = 2, name = "B" }
        });

        // Act
        var result = ShapeNormalizer.Normalize(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Rows.Should().HaveCount(2);
        result.Rows![0]["id"].GetInt32().Should().Be(1);
        result.Rows![1]["name"].GetString().Should().Be("B");
    }

    [Fact]
    public void ShapeNormalizer_Object_WrapsInArray()
    {
        // Arrange
        var input = JsonSerializer.SerializeToElement(new { id = 1, name = "Single" });

        // Act
        var result = ShapeNormalizer.Normalize(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Rows.Should().HaveCount(1);
        result.Rows![0]["id"].GetInt32().Should().Be(1);
    }

    [Fact]
    public void ShapeNormalizer_Null_ReturnsEmptyArray()
    {
        // Arrange
        var input = JsonSerializer.SerializeToElement<object?>(null);

        // Act
        var result = ShapeNormalizer.Normalize(input);

        // Assert
        result.Success.Should().BeTrue();
        result.Rows.Should().BeEmpty();
    }

    [Fact]
    public void ShapeNormalizer_Primitive_ReturnsError()
    {
        // Arrange
        var input = JsonSerializer.SerializeToElement("just a string");

        // Act
        var result = ShapeNormalizer.Normalize(input);

        // Assert
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("WrongShape");
    }

    [Fact]
    public void ShapeNormalizer_ExtractAndNormalize_Works()
    {
        // Arrange
        var input = JsonSerializer.SerializeToElement(new
        {
            data = new[]
            {
                new { x = 1 },
                new { x = 2 }
            }
        });

        // Act
        var result = ShapeNormalizer.ExtractAndNormalize(input, "/data");

        // Assert
        result.Success.Should().BeTrue();
        result.Rows.Should().HaveCount(2);
    }

    #endregion

    #region PlanExecutor Tests

    [Fact]
    public void PlanExecutor_Select_ExtractsFields()
    {
        // Arrange
        var plan = new TransformPlan
        {
            PlanVersion = "1.0",
            Source = new PlanSource { RecordPath = "/" },
            Steps = new List<PlanStep>
            {
                new PlanStep
                {
                    Op = PlanOps.Select,
                    Fields = new List<FieldSpec>
                    {
                        new FieldSpec { From = "/name", As = "Name" },
                        new FieldSpec { From = "/value", As = "Amount" }
                    }
                }
            }
        };

        var input = JsonSerializer.SerializeToElement(new[]
        {
            new { name = "A", value = 100, extra = "ignored" },
            new { name = "B", value = 200, extra = "ignored" }
        });

        // Act
        var result = PlanExecutor.Execute(plan, input);

        // Assert
        result.Success.Should().BeTrue();
        result.Rows.Should().HaveCount(2);
        result.Rows![0].Should().ContainKey("Name");
        result.Rows![0].Should().ContainKey("Amount");
        result.Rows![0].Should().NotContainKey("extra");
        result.Rows![0]["Name"].GetString().Should().Be("A");
        result.Rows![0]["Amount"].GetInt32().Should().Be(100);
    }

    [Fact]
    public void PlanExecutor_Filter_FiltersRows()
    {
        // Arrange
        var plan = new TransformPlan
        {
            PlanVersion = "1.0",
            Source = new PlanSource { RecordPath = "/" },
            Steps = new List<PlanStep>
            {
                new PlanStep
                {
                    Op = PlanOps.Filter,
                    Where = new Condition
                    {
                        Op = ConditionOps.Gt,
                        Left = JsonSerializer.SerializeToElement(new { field = "/value" }),
                        Right = JsonSerializer.SerializeToElement(150)
                    }
                }
            }
        };

        var input = JsonSerializer.SerializeToElement(new[]
        {
            new { name = "A", value = 100 },
            new { name = "B", value = 200 },
            new { name = "C", value = 150 }
        });

        // Act
        var result = PlanExecutor.Execute(plan, input);

        // Assert
        result.Success.Should().BeTrue();
        result.Rows.Should().HaveCount(1);
        result.Rows![0]["name"].GetString().Should().Be("B");
    }

    [Fact]
    public void PlanExecutor_Compute_CalculatesExpression()
    {
        // Arrange
        var plan = new TransformPlan
        {
            PlanVersion = "1.0",
            Source = new PlanSource { RecordPath = "/" },
            Steps = new List<PlanStep>
            {
                new PlanStep
                {
                    Op = PlanOps.Compute,
                    Compute = new List<ComputeSpec>
                    {
                        new ComputeSpec { As = "total", Expr = "price * quantity" }
                    }
                }
            }
        };

        var input = JsonSerializer.SerializeToElement(new[]
        {
            new { name = "A", price = 10.0, quantity = 5 },
            new { name = "B", price = 20.0, quantity = 3 }
        });

        // Act
        var result = PlanExecutor.Execute(plan, input);

        // Assert
        result.Success.Should().BeTrue();
        result.Rows.Should().HaveCount(2);
        result.Rows![0]["total"].GetDouble().Should().Be(50.0);
        result.Rows![1]["total"].GetDouble().Should().Be(60.0);
    }

    [Fact]
    public void PlanExecutor_Sort_SortsRows()
    {
        // Arrange
        var plan = new TransformPlan
        {
            PlanVersion = "1.0",
            Source = new PlanSource { RecordPath = "/" },
            Steps = new List<PlanStep>
            {
                new PlanStep { Op = PlanOps.Sort, By = "/value", Dir = "desc" }
            }
        };

        var input = JsonSerializer.SerializeToElement(new[]
        {
            new { name = "A", value = 100 },
            new { name = "B", value = 300 },
            new { name = "C", value = 200 }
        });

        // Act
        var result = PlanExecutor.Execute(plan, input);

        // Assert
        result.Success.Should().BeTrue();
        result.Rows.Should().HaveCount(3);
        result.Rows![0]["name"].GetString().Should().Be("B");
        result.Rows![1]["name"].GetString().Should().Be("C");
        result.Rows![2]["name"].GetString().Should().Be("A");
    }

    [Fact]
    public void PlanExecutor_GroupByAggregate_GroupsAndAggregates()
    {
        // Arrange
        var plan = new TransformPlan
        {
            PlanVersion = "1.0",
            Source = new PlanSource { RecordPath = "/" },
            Steps = new List<PlanStep>
            {
                new PlanStep
                {
                    Op = PlanOps.GroupBy,
                    Keys = new List<string> { "/category" }
                },
                new PlanStep
                {
                    Op = PlanOps.Aggregate,
                    Metrics = new List<MetricSpec>
                    {
                        new MetricSpec { As = "TotalValue", Fn = AggregateFunctions.Sum, Field = "/value" },
                        new MetricSpec { As = "Count", Fn = AggregateFunctions.Count }
                    }
                }
            }
        };

        var input = JsonSerializer.SerializeToElement(new[]
        {
            new { category = "A", value = 100 },
            new { category = "B", value = 200 },
            new { category = "A", value = 150 },
            new { category = "B", value = 50 }
        });

        // Act
        var result = PlanExecutor.Execute(plan, input);

        // Assert
        result.Success.Should().BeTrue();
        result.Rows.Should().HaveCount(2);

        var groupA = result.Rows!.First(r => r["category"].GetString() == "A");
        var groupB = result.Rows!.First(r => r["category"].GetString() == "B");

        groupA["TotalValue"].GetDouble().Should().Be(250);
        groupA["Count"].GetInt32().Should().Be(2);
        groupB["TotalValue"].GetDouble().Should().Be(250);
        groupB["Count"].GetInt32().Should().Be(2);
    }

    [Fact]
    public void PlanExecutor_Limit_LimitsRows()
    {
        // Arrange
        var plan = new TransformPlan
        {
            PlanVersion = "1.0",
            Source = new PlanSource { RecordPath = "/" },
            Steps = new List<PlanStep>
            {
                new PlanStep { Op = PlanOps.Limit, N = 2 }
            }
        };

        var input = JsonSerializer.SerializeToElement(new[]
        {
            new { id = 1 },
            new { id = 2 },
            new { id = 3 },
            new { id = 4 }
        });

        // Act
        var result = PlanExecutor.Execute(plan, input);

        // Assert
        result.Success.Should().BeTrue();
        result.Rows.Should().HaveCount(2);
    }

    [Fact]
    public void PlanExecutor_ComplexPipeline_Works()
    {
        // Arrange - select, filter, sort, limit
        var plan = new TransformPlan
        {
            PlanVersion = "1.0",
            Source = new PlanSource { RecordPath = "/products" },
            Steps = new List<PlanStep>
            {
                new PlanStep
                {
                    Op = PlanOps.Select,
                    Fields = new List<FieldSpec>
                    {
                        new FieldSpec { From = "/name", As = "Product" },
                        new FieldSpec { From = "/price", As = "Price" }
                    }
                },
                new PlanStep
                {
                    Op = PlanOps.Filter,
                    Where = new Condition
                    {
                        Op = ConditionOps.Gte,
                        Left = JsonSerializer.SerializeToElement(new { field = "/Price" }),
                        Right = JsonSerializer.SerializeToElement(20)
                    }
                },
                new PlanStep { Op = PlanOps.Sort, By = "/Price", Dir = "asc" },
                new PlanStep { Op = PlanOps.Limit, N = 2 }
            }
        };

        var input = JsonSerializer.SerializeToElement(new
        {
            products = new[]
            {
                new { name = "A", price = 10 },
                new { name = "B", price = 30 },
                new { name = "C", price = 20 },
                new { name = "D", price = 40 }
            }
        });

        // Act
        var result = PlanExecutor.Execute(plan, input);

        // Assert
        result.Success.Should().BeTrue();
        result.Rows.Should().HaveCount(2);
        result.Rows![0]["Product"].GetString().Should().Be("C"); // price 20
        result.Rows![1]["Product"].GetString().Should().Be("B"); // price 30
    }

    #endregion

    #region FieldResolver Tests

    [Fact]
    public void FieldResolver_DirectMatch_ReturnsOriginal()
    {
        // Arrange
        var sampleRecord = JsonSerializer.SerializeToElement(new { name = "Test", value = 100 });

        // Act
        var result = FieldResolver.Resolve("/name", sampleRecord);

        // Assert
        result.WasResolved.Should().BeFalse();
        result.ResolvedPath.Should().Be("/name");
    }

    [Fact]
    public void FieldResolver_PortugueseAlias_ResolvesToEnglish()
    {
        // Arrange
        var sampleRecord = JsonSerializer.SerializeToElement(new { name = "Test", city = "NYC" });

        // Act
        var result = FieldResolver.Resolve("/nome", sampleRecord);

        // Assert
        result.WasResolved.Should().BeTrue();
        result.ResolvedPath.Should().Be("/name");
        result.OriginalField.Should().Be("nome");
        result.ResolvedField.Should().Be("name");
    }

    [Fact]
    public void FieldResolver_CaseInsensitive_Resolves()
    {
        // Arrange
        var sampleRecord = JsonSerializer.SerializeToElement(new { Name = "Test", VALUE = 100 });

        // Act
        var result = FieldResolver.Resolve("/name", sampleRecord);

        // Assert
        result.WasResolved.Should().BeTrue();
        result.ResolvedPath.Should().Be("/Name");
    }

    [Fact]
    public void FieldResolver_NotFound_ReturnsOriginalWithWarning()
    {
        // Arrange
        var sampleRecord = JsonSerializer.SerializeToElement(new { id = 1, value = 100 });

        // Act
        var result = FieldResolver.Resolve("/unknown", sampleRecord);

        // Assert
        result.WasResolved.Should().BeFalse();
        result.ResolvedPath.Should().Be("/unknown");
        result.Warnings.Should().NotBeEmpty();
    }

    [Fact]
    public void FieldResolver_PriceAlias_Works()
    {
        // Arrange
        var sampleRecord = JsonSerializer.SerializeToElement(new { price = 19.99, quantity = 5 });

        // Act
        var result = FieldResolver.Resolve("/preco", sampleRecord);

        // Assert
        result.WasResolved.Should().BeTrue();
        result.ResolvedPath.Should().Be("/price");
    }

    #endregion

    #region PermissiveSchemaInference Tests

    [Fact]
    public void PermissiveSchemaInference_InfersCorrectTypes()
    {
        // Arrange
        var rows = new List<Dictionary<string, JsonElement>>
        {
            new Dictionary<string, JsonElement>
            {
                ["name"] = JsonSerializer.SerializeToElement("Test"),
                ["value"] = JsonSerializer.SerializeToElement(100),
                ["active"] = JsonSerializer.SerializeToElement(true)
            }
        };

        // Act
        var schema = PermissiveSchemaInference.InferSchema(rows);

        // Assert
        schema.GetProperty("type").GetString().Should().Be("array");
        var items = schema.GetProperty("items");
        items.GetProperty("additionalProperties").GetBoolean().Should().BeTrue();

        var properties = items.GetProperty("properties");
        properties.GetProperty("name").GetProperty("type").GetString().Should().Be("string");
        properties.GetProperty("value").GetProperty("type").GetString().Should().Be("number");
        properties.GetProperty("active").GetProperty("type").GetString().Should().Be("boolean");
    }

    [Fact]
    public void PermissiveSchemaInference_MergesTypes()
    {
        // Arrange - same field with different types across rows
        var rows = new List<Dictionary<string, JsonElement>>
        {
            new Dictionary<string, JsonElement>
            {
                ["value"] = JsonSerializer.SerializeToElement(100)
            },
            new Dictionary<string, JsonElement>
            {
                ["value"] = JsonSerializer.SerializeToElement("text")
            }
        };

        // Act
        var schema = PermissiveSchemaInference.InferSchema(rows);

        // Assert
        var valueType = schema.GetProperty("items").GetProperty("properties").GetProperty("value").GetProperty("type");
        valueType.ValueKind.Should().Be(JsonValueKind.Array);
        valueType.GetArrayLength().Should().Be(2);
    }

    [Fact]
    public void PermissiveSchemaInference_EmptyRows_ReturnsEmptySchema()
    {
        // Arrange
        var rows = new List<Dictionary<string, JsonElement>>();

        // Act
        var schema = PermissiveSchemaInference.InferSchema(rows);

        // Assert
        schema.GetProperty("type").GetString().Should().Be("array");
        schema.GetProperty("items").GetProperty("additionalProperties").GetBoolean().Should().BeTrue();
    }

    #endregion
}
