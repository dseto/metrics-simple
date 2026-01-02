using System.Text.Json;
using NJsonSchema;
using Xunit;
using YamlDotNet.Serialization;

namespace Metrics.Api.Tests;

/// <summary>
/// Contract tests validate that the backend's contracts (OpenAPI, JSON Schemas, DTOs)
/// match the specs in `specs/shared/openapi/` and `specs/shared/domain/schemas/`.
///
/// Per SCHEMA_GUIDE.md, schemas are loaded by file (FromFileAsync) to preserve documentPath
/// and allow NJsonSchema to resolve $ref relative to the schema directory.
/// </summary>
public class ApiContractTests
{
    private string SchemaDirectory => Path.Combine(AppContext.BaseDirectory, "schemas");
    private string OpenApiDirectory => Path.Combine(AppContext.BaseDirectory, "openapi");

    [Fact]
    public async Task ValidateOpenApiSpec()
    {
        var openApiPath = Path.Combine(OpenApiDirectory, "config-api.yaml");
        Assert.True(File.Exists(openApiPath), $"OpenAPI spec not found at {openApiPath}");

        var yaml = await File.ReadAllTextAsync(openApiPath);
        var deserializer = new DeserializerBuilder().Build();
        
        // Validate YAML structure
        var spec = deserializer.Deserialize<dynamic>(yaml);
        
        Assert.NotNull(spec);
        Assert.Equal("3.0.3", (string)spec["openapi"]);
        Assert.NotNull(spec["info"]);
        Assert.NotNull(spec["paths"]);
        
        // Verify required paths exist
        var paths = (IDictionary<object, object>)spec["paths"];
        Assert.Contains("/api/connectors", paths.Keys.Cast<string>());
        Assert.Contains("/api/processes", paths.Keys.Cast<string>());
        Assert.Contains("/api/preview/transform", paths.Keys.Cast<string>());
    }

    [Fact]
    public async Task ValidateConnectorSchema_WithRefResolution()
    {
        var schemaPath = Path.Combine(SchemaDirectory, "connector.schema.json");
        Assert.True(File.Exists(schemaPath), $"Connector schema not found at {schemaPath}");

        // Load via file to preserve documentPath for $ref resolution
        var schema = await JsonSchema.FromFileAsync(schemaPath);
        
        Assert.NotNull(schema);
        Assert.Equal(JsonObjectType.Object, schema.Type);
        
        // Verify required properties per spec
        Assert.True(schema.Properties.ContainsKey("id"), "connector must have 'id' property");
        Assert.True(schema.Properties.ContainsKey("name"), "connector must have 'name' property");
        Assert.True(schema.Properties.ContainsKey("baseUrl"), "connector must have 'baseUrl' property");
        Assert.True(schema.Properties.ContainsKey("authRef"), "connector must have 'authRef' property");
        Assert.True(schema.Properties.ContainsKey("timeoutSeconds"), "connector must have 'timeoutSeconds' property");
        
        // Verify 'id' property exists (it's a $ref to id.schema.json, resolved by NJsonSchema)
        var idProp = schema.Properties["id"];
        Assert.NotNull(idProp);
    }

    [Fact]
    public async Task ValidateProcessSchema_WithRefResolution()
    {
        var schemaPath = Path.Combine(SchemaDirectory, "process.schema.json");
        Assert.True(File.Exists(schemaPath), $"Process schema not found at {schemaPath}");

        var schema = await JsonSchema.FromFileAsync(schemaPath);
        
        Assert.NotNull(schema);
        Assert.Equal(JsonObjectType.Object, schema.Type);
        
        // Verify required properties per spec
        Assert.True(schema.Properties.ContainsKey("id"));
        Assert.True(schema.Properties.ContainsKey("name"));
        Assert.True(schema.Properties.ContainsKey("status"));
        Assert.True(schema.Properties.ContainsKey("connectorId"));
        Assert.True(schema.Properties.ContainsKey("outputDestinations"));
    }

    [Fact]
    public async Task ValidateProcessVersionSchema_WithInlineSourceRequest()
    {
        var schemaPath = Path.Combine(SchemaDirectory, "processVersion.schema.json");
        Assert.True(File.Exists(schemaPath), $"ProcessVersion schema not found at {schemaPath}");

        var schema = await JsonSchema.FromFileAsync(schemaPath);
        
        Assert.NotNull(schema);
        Assert.Equal(JsonObjectType.Object, schema.Type);
        
        // Verify required properties per spec
        Assert.True(schema.Properties.ContainsKey("processId"));
        Assert.True(schema.Properties.ContainsKey("version"));
        Assert.True(schema.Properties.ContainsKey("enabled"));
        Assert.True(schema.Properties.ContainsKey("sourceRequest"));
        Assert.True(schema.Properties.ContainsKey("dsl"));
        Assert.True(schema.Properties.ContainsKey("outputSchema"));
        
        // Note: sourceRequest is inline in processVersion.schema.json (per SCHEMA_GUIDE)
        // It should NOT have a separate file
        var sourceRequestSchema = schema.Properties["sourceRequest"];
        Assert.NotNull(sourceRequestSchema);
        Assert.Equal(JsonObjectType.Object, sourceRequestSchema.Type);
        Assert.True(sourceRequestSchema.Properties.ContainsKey("method"));
        Assert.True(sourceRequestSchema.Properties.ContainsKey("path"));
    }

    [Fact]
    public async Task ValidateIdSchema_IsPrimitiveString()
    {
        var schemaPath = Path.Combine(SchemaDirectory, "id.schema.json");
        Assert.True(File.Exists(schemaPath), $"ID schema not found at {schemaPath}");

        var schema = await JsonSchema.FromFileAsync(schemaPath);
        
        Assert.NotNull(schema);
        Assert.Equal(JsonObjectType.String, schema.Type);
        Assert.NotNull(schema.Pattern); // Should have regex validation
    }

    [Fact]
    public async Task ValidateApiErrorSchema()
    {
        var schemaPath = Path.Combine(SchemaDirectory, "apiError.schema.json");
        Assert.True(File.Exists(schemaPath), $"ApiError schema not found at {schemaPath}");

        var schema = await JsonSchema.FromFileAsync(schemaPath);
        
        Assert.NotNull(schema);
        Assert.Equal(JsonObjectType.Object, schema.Type);
        
        // Verify required error fields per spec
        Assert.True(schema.Properties.ContainsKey("code"));
        Assert.True(schema.Properties.ContainsKey("message"));
        Assert.True(schema.Properties.ContainsKey("correlationId"));
    }

    [Fact]
    public void ValidateNoSourceRequestSchema_SeparateFile()
    {
        // Per SCHEMA_GUIDE.md: sourceRequest is inline in processVersion.schema.json
        // It should NOT have a separate sourceRequest.schema.json file
        var schemaPath = Path.Combine(SchemaDirectory, "sourceRequest.schema.json");
        Assert.False(File.Exists(schemaPath), 
            "sourceRequest.schema.json should NOT exist (sourceRequest is inline in processVersion.schema.json per spec)");
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
}
