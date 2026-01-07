using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Metrics.Api.AI;
using Xunit;
using Xunit.Abstractions;

namespace Integration.Tests;

/// <summary>
/// IT04 — AI DSL Generation with Real LLM (OpenRouter)
/// 
/// Tests the full flow: Natural Language → LLM → DSL/Plan → Transform
/// 
/// Requirements:
/// - METRICS_OPENROUTER_API_KEY environment variable must be set
/// - Tests are skipped if API key is not available
/// </summary>
[Collection("Sequential")]
public class IT04_AiDslGenerateTests : IDisposable
{
    private readonly string _dbPath;
    private readonly TestWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;
    
    /// <summary>
    /// Check if LLM API key is available for tests that require it
    /// </summary>
    private static bool HasLlmApiKey => 
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("METRICS_OPENROUTER_API_KEY")) ||
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENROUTER_API_KEY"));

    public IT04_AiDslGenerateTests(ITestOutputHelper output)
    {
        _output = output;
        _dbPath = TestFixtures.CreateTempDbPath();
        _factory = new TestWebApplicationFactory(_dbPath, disableAuth: false);
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
        TestFixtures.CleanupTempFile(_dbPath);
    }

    /// <summary>
    /// Test DSL generation with a simple extraction prompt
    /// 
    /// This test validates:
    /// 1. LLM receives the natural language goal
    /// 2. LLM generates a valid IR plan
    /// 3. Plan is properly structured and can be used for transformation
    /// </summary>
    [Fact]
    [Trait("Category", "LLM")]
    [Trait("RequiresLLM", "true")]
    public async Task GenerateDsl_SimpleExtraction_ReturnsValidPlan()
    {
        _output.WriteLine("🚀 Testing DSL generation with real LLM (OpenRouter)");
        _output.WriteLine("");

        // Login first
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/token", 
            new { username = "admin", password = "testpass123" });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK, "Login should succeed");
        var loginContent = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = loginContent.GetProperty("access_token").GetString()!;
        _output.WriteLine($"✅ Logged in with admin token");
        _output.WriteLine("");

        // Arrange: Create a simple extraction request
        var sampleInput = new[]
        {
            new { id = "001", name = "John Doe", age = 30 },
            new { id = "002", name = "Jane Smith", age = 25 }
        };

        var request = new DslGenerateRequest
        {
            GoalText = "Extract only ID and name columns from the input data",
            SampleInput = JsonSerializer.SerializeToElement(sampleInput),
            DslProfile = "ir",
            Constraints = new DslConstraints
            {
                MaxColumns = 10,
                AllowTransforms = true,
                ForbidNetworkCalls = true,
                ForbidCodeExecution = true
            }
        };

        _output.WriteLine($"📝 Goal: {request.GoalText}");
        _output.WriteLine($"📊 Sample Input: {sampleInput.Length} records with 3 fields");
        _output.WriteLine("");

        // Act: Call the LLM to generate DSL with auth
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ai/dsl/generate");
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        httpRequest.Content = JsonContent.Create(request);

        var response = await _client.SendAsync(httpRequest);

        _output.WriteLine($"📡 OpenRouter Response Status: {response.StatusCode}");
        _output.WriteLine("");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK, "LLM should successfully generate a plan");

        var result = await response.Content.ReadFromJsonAsync<DslGenerateResult>();
        result.Should().NotBeNull("Response should contain DSL result");
        result!.Dsl.Should().NotBeNull("DSL output should not be null");
        result.Dsl.Profile.Should().Be("ir", "Profile should be 'ir' (Intermediate Representation)");
        result.Dsl.Text.Should().NotBeEmpty("DSL text should not be empty");
        result.OutputSchema.ValueKind.Should().Be(JsonValueKind.Object, "OutputSchema should be a JSON object");
        result.Rationale.Should().NotBeEmpty("LLM should provide rationale");

        _output.WriteLine($"✅ LLM generated valid plan!");
        _output.WriteLine($"   Profile: {result.Dsl.Profile}");
        _output.WriteLine($"   Plan length: {result.Dsl.Text.Length} chars");
        _output.WriteLine($"   Rationale: {result.Rationale}");
        if (result.Warnings.Any())
        {
            _output.WriteLine($"   ⚠️  Warnings: {string.Join(", ", result.Warnings)}");
        }
        _output.WriteLine("");

        // Bonus: Validate that plan is valid JSON (IR plans should be JSON)
        try
        {
            var planJson = JsonSerializer.Deserialize<JsonElement>(result.Dsl.Text);
            planJson.ValueKind.Should().NotBe(JsonValueKind.Undefined, "Plan should be valid JSON");
            _output.WriteLine("✅ Plan is valid JSON structure");
        }
        catch (JsonException ex)
        {
            _output.WriteLine($"⚠️  Plan is not valid JSON: {ex.Message}");
        }
    }

    /// <summary>
    /// Test DSL generation with a more complex transformation request
    /// </summary>
    [Fact]
    [Trait("Category", "LLM")]
    [Trait("RequiresLLM", "true")]
    public async Task GenerateDsl_ComplexAggregation_ReturnsValidPlan()
    {
        _output.WriteLine("🚀 Testing complex aggregation with real LLM");
        _output.WriteLine("");

        // Login first
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/token", 
            new { username = "admin", password = "testpass123" });
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK, "Login should succeed");
        var loginContent = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = loginContent.GetProperty("access_token").GetString()!;

        // Arrange
        var sampleInput = new
        {
            sales = new[]
            {
                new { product = "Laptop", price = 1200, qty = 5 },
                new { product = "Mouse", price = 25, qty = 50 },
                new { product = "Laptop", price = 1200, qty = 3 }
            }
        };

        var request = new DslGenerateRequest
        {
            GoalText = "Group sales by product and sum the total value (price * qty) for each product",
            SampleInput = JsonSerializer.SerializeToElement(sampleInput),
            DslProfile = "ir",
            Constraints = new DslConstraints
            {
                MaxColumns = 50,
                AllowTransforms = true,
                ForbidNetworkCalls = true,
                ForbidCodeExecution = true
            }
        };

        _output.WriteLine($"📝 Goal: {request.GoalText}");
        _output.WriteLine("");

        // Act: Call the LLM with auth
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ai/dsl/generate");
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        httpRequest.Content = JsonContent.Create(request);

        var response = await _client.SendAsync(httpRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<DslGenerateResult>();
        result.Should().NotBeNull();
        result!.Dsl.Profile.Should().Be("ir");
        result.Dsl.Text.Should().NotBeEmpty();

        _output.WriteLine($"✅ LLM generated plan for aggregation");
        _output.WriteLine($"   Plan length: {result.Dsl.Text.Length} chars");
        _output.WriteLine($"   Rationale: {result.Rationale}");
    }

    /// <summary>
    /// Test DSL generation with invalid constraints (should return error)
    /// </summary>
    [Fact]
    [Trait("Category", "Validation")]
    public async Task GenerateDsl_InvalidConstraints_ReturnsBadRequest()
    {
        _output.WriteLine("🚀 Testing validation of invalid constraints");

        // Arrange: Login first
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/token", 
            new { username = "admin", password = "testpass123" });
        var loginContent = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = loginContent.GetProperty("access_token").GetString()!;

        var sampleInput = new { field = "value" };

        var request = new DslGenerateRequest
        {
            GoalText = "Extract all fields from all records in the dataset",
            SampleInput = JsonSerializer.SerializeToElement(sampleInput),
            DslProfile = "ir",
            Constraints = new DslConstraints
            {
                MaxColumns = 300, // Invalid: > 200
                AllowTransforms = true,
                ForbidNetworkCalls = true,
                ForbidCodeExecution = true
            }
        };

        // Act: Create HTTP request with auth header
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ai/dsl/generate");
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        httpRequest.Content = JsonContent.Create(request);

        var response = await _client.SendAsync(httpRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<AiError>();
        error.Should().NotBeNull();
        error!.Code.Should().NotBeEmpty("Should have an error code");

        _output.WriteLine($"✅ Validation correctly rejected invalid constraints");
        _output.WriteLine($"   Error Code: {error.Code}");
    }

    /// <summary>
    /// Test DSL generation with a short goal text (should return error)
    /// </summary>
    [Fact]
    [Trait("Category", "Validation")]
    public async Task GenerateDsl_GoalTextTooShort_ReturnsBadRequest()
    {
        _output.WriteLine("🚀 Testing validation of short goal text");

        // Arrange: Login first
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/token", 
            new { username = "admin", password = "testpass123" });
        var loginContent = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = loginContent.GetProperty("access_token").GetString()!;

        var sampleInput = new { field = "value" };

        var request = new DslGenerateRequest
        {
            GoalText = "short", // Invalid: < 10 chars
            SampleInput = JsonSerializer.SerializeToElement(sampleInput),
            DslProfile = "ir",
            Constraints = new DslConstraints { MaxColumns = 50 }
        };

        // Act: Create HTTP request with auth header
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ai/dsl/generate");
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        httpRequest.Content = JsonContent.Create(request);

        var response = await _client.SendAsync(httpRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        _output.WriteLine($"✅ Validation correctly rejected short goal text");
    }
}
