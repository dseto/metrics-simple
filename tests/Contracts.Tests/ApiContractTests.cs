using System.Text.Json;
using NJsonSchema;
using Xunit;
using YamlDotNet.Serialization;

namespace Metrics.Api.Tests;

public class ApiContractTests
{
    [Fact]
    public async Task ValidateOpenApiSpec()
    {
        var openApiPath = Path.Combine(AppContext.BaseDirectory, "openapi-config-api.yaml");
        Assert.True(File.Exists(openApiPath), $"OpenAPI spec not found at {openApiPath}");

        var yaml = await File.ReadAllTextAsync(openApiPath);
        var deserializer = new DeserializerBuilder().Build();
        
        // Basic YAML validation - should not throw
        var spec = deserializer.Deserialize<dynamic>(yaml);
        
        Assert.NotNull(spec);
        Assert.Equal("3.0.3", (string)spec["openapi"]);
        Assert.NotNull(spec["info"]);
        Assert.NotNull(spec["paths"]);
    }

    [Fact]
    public async Task ValidateConnectorSchema()
    {
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "schemas", "connector.schema.json");
        Assert.True(File.Exists(schemaPath), $"Connector schema not found at {schemaPath}");

        var schemaJson = await File.ReadAllTextAsync(schemaPath);
        var schema = await JsonSchema.FromJsonAsync(schemaJson);
        
        Assert.NotNull(schema);
        Assert.Equal("object", schema.Type.ToString().ToLower());
        Assert.Contains("name", schema.Properties.Keys);
        Assert.Contains("baseUrl", schema.Properties.Keys);
    }

    [Fact]
    public async Task ValidateProcessSchema()
    {
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "schemas", "process.schema.json");
        Assert.True(File.Exists(schemaPath), $"Process schema not found at {schemaPath}");

        var schemaJson = await File.ReadAllTextAsync(schemaPath);
        var schema = await JsonSchema.FromJsonAsync(schemaJson);
        
        Assert.NotNull(schema);
        Assert.Equal("object", schema.Type.ToString().ToLower());
        Assert.Contains("name", schema.Properties.Keys);
    }

    [Fact]
    public async Task ValidateProcessVersionSchema()
    {
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "schemas", "processVersion.schema.json");
        Assert.True(File.Exists(schemaPath), $"ProcessVersion schema not found at {schemaPath}");

        var schemaJson = await File.ReadAllTextAsync(schemaPath);
        
        // Replace $ref with inline validation since we're not resolving external refs
        Assert.Contains("sourceRequest", schemaJson);
        Assert.Contains("processId", schemaJson);
        Assert.Contains("version", schemaJson);
        Assert.Contains("enabled", schemaJson);
        Assert.Contains("dsl", schemaJson);
        Assert.Contains("outputSchema", schemaJson);
    }

    [Fact]
    public async Task ValidateSourceRequestSchema()
    {
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "schemas", "sourceRequest.schema.json");
        Assert.True(File.Exists(schemaPath), $"SourceRequest schema not found at {schemaPath}");

        var schemaJson = await File.ReadAllTextAsync(schemaPath);
        var schema = await JsonSchema.FromJsonAsync(schemaJson);
        
        Assert.NotNull(schema);
        Assert.Equal("object", schema.Type.ToString().ToLower());
    }

    [Fact]
    public void TestProcessDtoStructure()
    {
        // Verify that ProcessDto has the required fields per OpenAPI spec
        var processType = typeof(ProcessDto);
        
        Assert.True(processType.GetProperty("Id") != null, "ProcessDto must have Id property");
        Assert.True(processType.GetProperty("Name") != null, "ProcessDto must have Name property");
        Assert.True(processType.GetProperty("Status") != null, "ProcessDto must have Status property");
        Assert.True(processType.GetProperty("ConnectorId") != null, "ProcessDto must have ConnectorId property");
        Assert.True(processType.GetProperty("OutputDestinations") != null, "ProcessDto must have OutputDestinations property");
    }

    [Fact]
    public void TestProcessVersionDtoStructure()
    {
        // Verify that ProcessVersionDto has the required fields per OpenAPI spec
        var versionType = typeof(ProcessVersionDto);
        
        Assert.True(versionType.GetProperty("ProcessId") != null, "ProcessVersionDto must have ProcessId property");
        Assert.True(versionType.GetProperty("Version") != null, "ProcessVersionDto must have Version property");
        Assert.True(versionType.GetProperty("Enabled") != null, "ProcessVersionDto must have Enabled property");
        Assert.True(versionType.GetProperty("SourceRequest") != null, "ProcessVersionDto must have SourceRequest property");
        Assert.True(versionType.GetProperty("Dsl") != null, "ProcessVersionDto must have Dsl property");
        Assert.True(versionType.GetProperty("OutputSchema") != null, "ProcessVersionDto must have OutputSchema property");
    }

    [Fact]
    public void TestConnectorDtoStructure()
    {
        // Verify that ConnectorDto has the required fields per OpenAPI spec
        var connectorType = typeof(ConnectorDto);
        
        Assert.True(connectorType.GetProperty("Id") != null, "ConnectorDto must have Id property");
        Assert.True(connectorType.GetProperty("Name") != null, "ConnectorDto must have Name property");
        Assert.True(connectorType.GetProperty("BaseUrl") != null, "ConnectorDto must have BaseUrl property");
        Assert.True(connectorType.GetProperty("AuthRef") != null, "ConnectorDto must have AuthRef property");
        Assert.True(connectorType.GetProperty("TimeoutSeconds") != null, "ConnectorDto must have TimeoutSeconds property");
    }

    [Fact]
    public void TestPreviewTransformRequestDtoStructure()
    {
        // Verify that PreviewTransformRequestDto has the required fields per OpenAPI spec
        var requestType = typeof(PreviewTransformRequestDto);
        
        Assert.True(requestType.GetProperty("Dsl") != null, "PreviewTransformRequestDto must have Dsl property");
        Assert.True(requestType.GetProperty("OutputSchema") != null, "PreviewTransformRequestDto must have OutputSchema property");
        Assert.True(requestType.GetProperty("SampleInput") != null, "PreviewTransformRequestDto must have SampleInput property");
    }

    [Fact]
    public void TestPreviewTransformResponseDtoStructure()
    {
        // Verify that PreviewTransformResponseDto has the required fields per OpenAPI spec
        var responseType = typeof(PreviewTransformResponseDto);
        
        Assert.True(responseType.GetProperty("IsValid") != null, "PreviewTransformResponseDto must have IsValid property");
        Assert.True(responseType.GetProperty("Errors") != null, "PreviewTransformResponseDto must have Errors property");
        Assert.True(responseType.GetProperty("PreviewOutput") != null, "PreviewTransformResponseDto must have PreviewOutput property");
        Assert.True(responseType.GetProperty("PreviewCsv") != null, "PreviewTransformResponseDto must have PreviewCsv property");
    }

    [Fact]
    public void TestDslDtoStructure()
    {
        // Verify that DslDto has the required fields
        var dslType = typeof(DslDto);
        
        Assert.True(dslType.GetProperty("Profile") != null, "DslDto must have Profile property");
        Assert.True(dslType.GetProperty("Text") != null, "DslDto must have Text property");
    }

    [Fact]
    public void TestSourceRequestDtoStructure()
    {
        // Verify that SourceRequestDto has the required fields
        var sourceType = typeof(SourceRequestDto);
        
        Assert.True(sourceType.GetProperty("Method") != null, "SourceRequestDto must have Method property");
        Assert.True(sourceType.GetProperty("Path") != null, "SourceRequestDto must have Path property");
    }
}
