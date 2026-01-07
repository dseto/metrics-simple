using System.Text.Json;
using Metrics.Api.AI;
using Xunit;

namespace Metrics.Api.Tests;

/// <summary>
/// Unit tests for AI guardrails, validation, and providers.
/// </summary>
public class AiGuardrailsTests
{
    [Fact]
    public void ValidateRequest_ValidRequest_ReturnsIsValid()
    {
        var request = CreateValidRequest();
        
        var result = AiGuardrails.ValidateRequest(request);
        
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateRequest_GoalTextTooShort_ReturnsError()
    {
        var request = CreateValidRequest() with { GoalText = "short" };
        
        var result = AiGuardrails.ValidateRequest(request);
        
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Path == "goalText" && e.Message.Contains("at least 10"));
    }

    [Fact]
    public void ValidateRequest_GoalTextTooLong_ReturnsError()
    {
        var request = CreateValidRequest() with { GoalText = new string('x', 4001) };
        
        var result = AiGuardrails.ValidateRequest(request);
        
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Path == "goalText" && e.Message.Contains("4000"));
    }

    [Fact]
    public void ValidateRequest_InvalidDslProfile_ReturnsError()
    {
        var request = CreateValidRequest() with { DslProfile = "invalid-profile" };
        
        var result = AiGuardrails.ValidateRequest(request);
        
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Path == "dslProfile");
    }

    [Fact]
    public void ValidateRequest_MaxColumnsOutOfRange_ReturnsError()
    {
        var request = CreateValidRequest() with
        {
            Constraints = new DslConstraints { MaxColumns = 250 }
        };
        
        var result = AiGuardrails.ValidateRequest(request);
        
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Path == "constraints.maxColumns");
    }

    [Fact]
    public void ValidateRequest_ExistingDslTooLong_ReturnsError()
    {
        var request = CreateValidRequest() with { ExistingDsl = new string('x', 20001) };
        
        var result = AiGuardrails.ValidateRequest(request);
        
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Path == "existingDsl");
    }

    [Fact]
    public async Task ValidateResult_ValidResult_ReturnsIsValid()
    {
        var result = CreateValidResult();
        
        var validation = await AiGuardrails.ValidateResultAsync(result);
        
        Assert.True(validation.IsValid);
        Assert.Empty(validation.Errors);
    }

    [Fact]
    public async Task ValidateResult_EmptyDslText_ReturnsError()
    {
        var result = CreateValidResult() with
        {
            Dsl = new DslOutput { Profile = "ir", Text = "" }
        };
        
        var validation = await AiGuardrails.ValidateResultAsync(result);
        
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, e => e.Path == "dsl.text");
    }

    [Fact]
    public async Task ValidateResult_InvalidOutputSchema_ReturnsError()
    {
        // Create a result with an invalid JSON Schema (array instead of object)
        var arrayJson = JsonDocument.Parse("[]").RootElement;
        var result = CreateValidResult() with { OutputSchema = arrayJson };
        
        var validation = await AiGuardrails.ValidateResultAsync(result);
        
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, e => e.Path == "outputSchema");
    }

    [Fact]
    public async Task ValidateResult_TooManyWarnings_ReturnsError()
    {
        var result = CreateValidResult() with
        {
            Warnings = Enumerable.Range(0, 25).Select(i => $"Warning {i}").ToList()
        };
        
        var validation = await AiGuardrails.ValidateResultAsync(result);
        
        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, e => e.Path == "warnings");
    }

    [Fact]
    public void TruncateText_ShortText_ReturnsOriginal()
    {
        var text = "short text";
        
        var result = AiGuardrails.TruncateText(text, 100);
        
        Assert.Equal(text, result);
    }

    [Fact]
    public void TruncateText_LongText_TruncatesDeterministically()
    {
        var text = new string('x', 100);
        
        var result = AiGuardrails.TruncateText(text, 50);
        
        Assert.EndsWith("[TRUNCATED]", result);
        Assert.True(result.Length <= 50);
        
        // Verify determinism
        var result2 = AiGuardrails.TruncateText(text, 50);
        Assert.Equal(result, result2);
    }

    [Fact]
    public void ComputeInputHash_DeterministicHash()
    {
        var json = JsonDocument.Parse("{\"key\": \"value\"}").RootElement;
        
        var hash1 = AiGuardrails.ComputeInputHash(json);
        var hash2 = AiGuardrails.ComputeInputHash(json);
        
        Assert.Equal(hash1, hash2);
        Assert.Equal(16, hash1.Length); // First 16 chars of hex SHA256
    }

    [Fact]
    public void ComputeInputHash_DifferentInputs_DifferentHashes()
    {
        var json1 = JsonDocument.Parse("{\"key\": \"value1\"}").RootElement;
        var json2 = JsonDocument.Parse("{\"key\": \"value2\"}").RootElement;
        
        var hash1 = AiGuardrails.ComputeInputHash(json1);
        var hash2 = AiGuardrails.ComputeInputHash(json2);
        
        Assert.NotEqual(hash1, hash2);
    }

    private static DslGenerateRequest CreateValidRequest()
    {
        var sampleInput = JsonDocument.Parse("{\"data\": [1, 2, 3]}").RootElement;
        return new DslGenerateRequest
        {
            GoalText = "Create a CSV with id and name columns from the input data.",
            SampleInput = sampleInput,
            DslProfile = "ir",
            Constraints = new DslConstraints
            {
                MaxColumns = 50,
                AllowTransforms = true,
                ForbidNetworkCalls = true,
                ForbidCodeExecution = true
            }
        };
    }

    private static DslGenerateResult CreateValidResult()
    {
        var schemaJson = JsonDocument.Parse("""
            {
                "type": "object",
                "properties": {
                    "id": { "type": "string" },
                    "name": { "type": "string" }
                },
                "required": ["id", "name"]
            }
            """).RootElement;

        return new DslGenerateResult
        {
            Dsl = new DslOutput
            {
                Profile = "ir",
                Text = "$.{ \"id\": id, \"name\": name }"
            },
            OutputSchema = schemaJson,
            Rationale = "Simple mapping of id and name fields.",
            Warnings = new List<string>()
        };
    }
}
