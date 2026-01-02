using System.Text.Json;
using Metrics.Engine;
using Xunit;
using YamlDotNet.Serialization;

namespace Metrics.Engine.Tests;

public class GoldenTests
{
    private readonly EngineService _engine;

    public GoldenTests()
    {
        var transformer = new JsonataTransformer();
        var validator = new SchemaValidator();
        var csvGenerator = new CsvGenerator();
        _engine = new EngineService(transformer, validator, csvGenerator);
    }

    [Fact]
    public void TestHostsCpuTransform()
    {
        // Load test case from YAML
        var testYamlPath = Path.Combine(AppContext.BaseDirectory, "unit-golden-tests.yaml");
        Assert.True(File.Exists(testYamlPath), $"Test YAML not found at {testYamlPath}");

        var yaml = File.ReadAllText(testYamlPath);
        var deserializer = new DeserializerBuilder().Build();
        var testData = deserializer.Deserialize<dynamic>(yaml);

        var testCase = testData["tests"][0];
        var dslProfile = (string)testCase["dslProfile"];
        
        // Load input
        var inputPath = Path.Combine(AppContext.BaseDirectory, (string)testCase["inputFile"]);
        var inputJson = File.ReadAllText(inputPath);
        var input = JsonDocument.Parse(inputJson).RootElement;

        // Load DSL
        var dslPath = Path.Combine(AppContext.BaseDirectory, (string)testCase["dslFile"]);
        var dslText = File.ReadAllText(dslPath);

        // Load expected output
        var expectedOutputPath = Path.Combine(AppContext.BaseDirectory, (string)testCase["expectedOutputFile"]);
        var expectedOutputJson = File.ReadAllText(expectedOutputPath);
        var expectedOutput = JsonDocument.Parse(expectedOutputJson).RootElement;

        // Load schema
        var schemaPath = Path.Combine(AppContext.BaseDirectory, (string)testCase["expectedSchemaFile"]);
        var schemaJson = File.ReadAllText(schemaPath);
        var schema = JsonDocument.Parse(schemaJson).RootElement;

        // Execute transformation
        var result = _engine.TransformValidateToCsv(input, dslProfile, dslText, schema);

        // Assertions
        Assert.True(result.IsValid, $"Transformation failed: {string.Join(", ", result.Errors)}");
        Assert.NotNull(result.OutputJson);
        Assert.NotNull(result.CsvPreview);

        // Validate output structure and content (deserialize and compare as objects)
        var outputArray = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(result.OutputJson.Value.GetRawText());
        var expectedArray = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(expectedOutput.GetRawText());
        
        Assert.NotNull(outputArray);
        Assert.NotNull(expectedArray);
        Assert.Equal(expectedArray.Count, outputArray.Count);
        
        for (int i = 0; i < expectedArray.Count; i++)
        {
            Assert.Equal(expectedArray[i].Count, outputArray[i].Count);
            foreach (var key in expectedArray[i].Keys)
            {
                Assert.True(outputArray[i].ContainsKey(key), $"Key '{key}' not found in output item {i}");
                var expectedVal = expectedArray[i][key]?.ToString();
                var outputVal = outputArray[i][key]?.ToString();
                Assert.Equal(expectedVal, outputVal);
            }
        }

        // Validate CSV output
        var csvLines = result.CsvPreview!.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.NotEmpty(csvLines);
        
        var csvData = testCase["csv"];
        var expectedHeader = (string)csvData["expectedHeader"];
        Assert.Equal(expectedHeader, csvLines[0]);

        var expectedFirstRow = (string)csvData["expectedFirstRow"];
        Assert.Equal(expectedFirstRow, csvLines[1]);
    }

    [Fact]
    public void TestSimpleArrayTransform()
    {
        // Simple inline test
        var input = JsonDocument.Parse(@"
        {
            ""items"": [
                { ""id"": 1, ""name"": ""Item1"" },
                { ""id"": 2, ""name"": ""Item2"" }
            ]
        }").RootElement;

        var dslText = @"items.{ ""id"": id, ""name"": name }";
        
        var schema = JsonDocument.Parse(@"
        {
            ""type"": ""array"",
            ""items"": {
                ""type"": ""object"",
                ""properties"": {
                    ""id"": { ""type"": ""integer"" },
                    ""name"": { ""type"": ""string"" }
                },
                ""required"": [""id"", ""name""]
            }
        }").RootElement;

        var result = _engine.TransformValidateToCsv(input, "jsonata", dslText, schema);

        Assert.True(result.IsValid);
        Assert.NotNull(result.OutputJson);
        Assert.NotNull(result.CsvPreview);
        
        var csv = result.CsvPreview!;
        Assert.Contains("id,name", csv);
        Assert.Contains("1,Item1", csv);
        Assert.Contains("2,Item2", csv);
    }
}
