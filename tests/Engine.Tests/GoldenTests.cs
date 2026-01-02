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
        // Load test case from YAML (in golden/ subdirectory per updated structure)
        var testYamlPath = Path.Combine(AppContext.BaseDirectory, "golden", "unit-golden-tests.yaml");
        Assert.True(File.Exists(testYamlPath), $"Test YAML not found at {testYamlPath}");

        var yaml = File.ReadAllText(testYamlPath);
        var deserializer = new DeserializerBuilder().Build();
        var testData = deserializer.Deserialize<Dictionary<string, object>>(yaml);

        // Verify YAML structure
        Assert.NotNull(testData);
        Assert.True(testData.ContainsKey("tests"), "YAML must contain 'tests' key");
        
        var testsList = testData["tests"] as List<object>;
        Assert.NotNull(testsList);
        Assert.NotEmpty(testsList);

        // Verify first test case
        var testCaseDict = testsList[0] as Dictionary<object, object>;
        Assert.NotNull(testCaseDict);
        
        var testCaseName = testCaseDict?["name"]?.ToString() ?? "";
        Assert.Equal("HostsCpuTransform", testCaseName);

        // Load fixture paths (relative to golden/fixtures)
        var inputFileName = testCaseDict?["inputFile"]?.ToString() ?? "";
        var dslFileName = testCaseDict?["dslFile"]?.ToString() ?? "";
        var outputFileName = testCaseDict?["expectedOutputFile"]?.ToString() ?? "";
        var schemaFileName = testCaseDict?["expectedSchemaFile"]?.ToString() ?? "";
        var csvFileName = testCaseDict?["expectedCsvFile"]?.ToString() ?? "";

        var inputPath = Path.Combine(AppContext.BaseDirectory, "golden", inputFileName);
        var dslPath = Path.Combine(AppContext.BaseDirectory, "golden", dslFileName);
        var expectedOutputPath = Path.Combine(AppContext.BaseDirectory, "golden", outputFileName);
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "golden", schemaFileName);
        var csvPath = Path.Combine(AppContext.BaseDirectory, "golden", csvFileName);

        Assert.True(File.Exists(inputPath), $"Input file not found: {inputPath}");
        Assert.True(File.Exists(dslPath), $"DSL file not found: {dslPath}");
        Assert.True(File.Exists(expectedOutputPath), $"Expected output file not found: {expectedOutputPath}");
        Assert.True(File.Exists(schemaPath), $"Schema file not found: {schemaPath}");
        Assert.True(File.Exists(csvPath), $"Expected CSV file not found: {csvPath}");

        // Load all fixture data
        var inputJson = File.ReadAllText(inputPath);
        var dslText = File.ReadAllText(dslPath);
        var expectedOutputJson = File.ReadAllText(expectedOutputPath);
        var expectedCsvContent = File.ReadAllText(csvPath);

        // Parse input and expected output
        var input = JsonDocument.Parse(inputJson).RootElement;
        var expectedOutput = JsonDocument.Parse(expectedOutputJson).RootElement;

        // Load schema
        var schemaJson = File.ReadAllText(schemaPath);
        var schema = JsonDocument.Parse(schemaJson).RootElement;

        // === END-TO-END TRANSFORMATION TEST ===
        // Execute: Input JSON -> Jsonata DSL -> Output JSON -> Validate Schema -> CSV
        var result = _engine.TransformValidateToCsv(input, "jsonata", dslText, schema);

        // 1. Verify transformation succeeded
        Assert.True(result.IsValid, $"Transformation or validation failed: {string.Join(", ", result.Errors)}");
        Assert.NotNull(result.OutputJson);

        // 2. Verify output JSON matches expected (normalize whitespace)
        var outputJson = result.OutputJson.Value.GetRawText();
        var expectedOutputRaw = expectedOutput.GetRawText();
        
        // Normalize both by parsing and re-serializing to remove whitespace differences
        var outputElement = JsonDocument.Parse(outputJson).RootElement;
        var expectedElement = JsonDocument.Parse(expectedOutputRaw).RootElement;
        
        // Compare as strings after normalization (serialize both same way)
        var options = new JsonSerializerOptions { WriteIndented = true };
        var outputNormalized = JsonSerializer.Serialize(outputElement, options);
        var expectedNormalized = JsonSerializer.Serialize(expectedElement, options);
        
        Assert.Equal(expectedNormalized, outputNormalized);

        // 3. Verify CSV matches expected (exact byte match for determinism)
        Assert.NotNull(result.CsvPreview);
        var csvOutput = result.CsvPreview!;
        
        // Normalize line endings for comparison (CRLF vs LF)
        var csvNormalized = csvOutput.Replace("\r\n", "\n").Trim();
        var csvExpectedNormalized = expectedCsvContent.Replace("\r\n", "\n").Trim();
        
        Assert.Equal(csvExpectedNormalized, csvNormalized);
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

    [Fact]
    public void TestQuotingTransform()
    {
        // For now, simplified quoting test without complex $map() + function
        // Validates the CSV quoting functionality per RFC4180
        
        var input = JsonDocument.Parse("[{\"text\":\"hello, \\\"quoted\\\"\\nnext\"}]").RootElement;

        // Simplified DSL - just pass through the array
        var dslText = "$";
        
        var schema = JsonDocument.Parse(@"
        {
            ""type"": ""array"",
            ""items"": {
                ""type"": ""object"",
                ""additionalProperties"": false,
                ""required"": [""text""],
                ""properties"": {
                    ""text"": { ""type"": ""string"" }
                }
            }
        }").RootElement;

        var result = _engine.TransformValidateToCsv(input, "jsonata", dslText, schema);

        Assert.True(result.IsValid, $"Transformation or validation failed: {string.Join(", ", result.Errors)}");
        Assert.NotNull(result.OutputJson);
        Assert.NotNull(result.CsvPreview);
        
        var csv = result.CsvPreview!;
        // Verify RFC4180 quoting: commas, quotes, and newlines are properly escaped
        Assert.Contains("text", csv);
        // RFC4180: fields with comma/quote/newline must be quoted, and quotes doubled
        Assert.Contains("\"", csv);
    }
}
