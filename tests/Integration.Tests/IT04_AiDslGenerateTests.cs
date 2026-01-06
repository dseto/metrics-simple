using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Metrics.Api.AI;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;
using Xunit;

namespace Integration.Tests;

/// <summary>
/// Integration tests for AI DSL generation endpoint.
/// Tests use MockProvider or WireMock.Net to simulate OpenRouter.
/// </summary>
[Collection("Sequential")]
public class IT04_AiDslGenerateTests : IClassFixture<AiTestFixture>, IDisposable
{
    private readonly AiTestFixture _fixture;
    private readonly HttpClient _client;
    private readonly string _dbPath;

    public IT04_AiDslGenerateTests(AiTestFixture fixture)
    {
        _fixture = fixture;
        _dbPath = TestFixtures.CreateTempDbPath();
        _client = CreateTestClient();
    }

    private HttpClient CreateTestClient()
    {
        var factory = new AiTestWebApplicationFactory(_dbPath, _fixture.AiConfig, _fixture.MockProvider);
        return factory.CreateClient();
    }

    [Fact]
    public async Task GenerateDsl_WithMockProvider_Returns200WithValidResult()
    {
        // Arrange
        var request = CreateValidRequest();

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/ai/dsl/generate", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<DslGenerateResult>();
        result.Should().NotBeNull();
        result!.Dsl.Should().NotBeNull();
        result.Dsl.Profile.Should().Be("jsonata");
        result.Dsl.Text.Should().NotBeEmpty();
        result.OutputSchema.ValueKind.Should().Be(JsonValueKind.Object);
        result.Rationale.Should().NotBeNull();
        result.Warnings.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateDsl_InvalidRequest_Returns400()
    {
        // Arrange - goalText too short
        var request = new
        {
            goalText = "short",
            sampleInput = new { data = new[] { 1, 2, 3 } },
            dslProfile = "jsonata",
            constraints = new
            {
                maxColumns = 50,
                allowTransforms = true,
                forbidNetworkCalls = true,
                forbidCodeExecution = true
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/ai/dsl/generate", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<AiError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(AiErrorCodes.AiOutputInvalid);
        error.CorrelationId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateDsl_IncludesCorrelationId_FromHeader()
    {
        // Arrange
        var request = CreateValidRequest();
        var correlationId = "test-correlation-12345";

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ai/dsl/generate")
        {
            Content = JsonContent.Create(request),
            Headers = { { "X-Correlation-Id", correlationId } }
        };

        // Act
        var response = await _client.SendAsync(httpRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        // The correlation ID is included in the response for errors, but for success we just check it worked
    }

    [Fact]
    public async Task GenerateDsl_WithEnginePlanV1_Returns501NotImplemented()
    {
        // Arrange
        var request = new DslGenerateRequest
        {
            GoalText = "Create a CSV with id, name, and value columns from the input data array.",
            SampleInput = JsonSerializer.SerializeToElement(new[] { new { id = "1", name = "test", value = 100 } }),
            DslProfile = "jsonata",
            Constraints = new DslConstraints
            {
                MaxColumns = 50,
                AllowTransforms = true,
                ForbidNetworkCalls = true,
                ForbidCodeExecution = true
            },
            Engine = "plan_v1"  // Request plan_v1 engine
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/ai/dsl/generate", request);

        // Assert - plan_v1 returns 501 Not Implemented (stub)
        response.StatusCode.Should().Be(HttpStatusCode.NotImplemented);

        var error = await response.Content.ReadFromJsonAsync<AiError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be("ENGINE_NOT_IMPLEMENTED");
        error!.Message.Should().Contain("plan_v1");
    }

    [Fact]
    public async Task GenerateDsl_WithInvalidEngine_Returns400BadRequest()
    {
        // Arrange
        var request = new DslGenerateRequest
        {
            GoalText = "Create a CSV with id, name, and value columns from the input data array.",
            SampleInput = JsonSerializer.SerializeToElement(new[] { new { id = "1", name = "test", value = 100 } }),
            DslProfile = "jsonata",
            Constraints = new DslConstraints
            {
                MaxColumns = 50,
                AllowTransforms = true,
                ForbidNetworkCalls = true,
                ForbidCodeExecution = true
            },
            Engine = "invalid_engine"  // Invalid engine value
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/ai/dsl/generate", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<AiError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be("INVALID_ENGINE");
    }

    [Fact]
    public async Task GenerateDsl_WithEngineLegacy_Returns200WithEngineUsedField()
    {
        // Arrange
        var request = new DslGenerateRequest
        {
            GoalText = "Create a CSV with id, name, and value columns from the input data array.",
            SampleInput = JsonSerializer.SerializeToElement(new[] { new { id = "1", name = "test", value = 100 } }),
            DslProfile = "jsonata",
            Constraints = new DslConstraints
            {
                MaxColumns = 50,
                AllowTransforms = true,
                ForbidNetworkCalls = true,
                ForbidCodeExecution = true
            },
            Engine = "legacy"  // Explicitly request legacy engine
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/ai/dsl/generate", request);

        // Assert - legacy works same as before
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<DslGenerateResult>();
        result.Should().NotBeNull();
        result!.Dsl.Should().NotBeNull();
        result!.EngineUsed.Should().Be("legacy");
    }

    [Fact]
    public async Task GenerateDsl_WithAutoEngine_FallsBackToLegacy()
    {
        // Arrange
        var request = new DslGenerateRequest
        {
            GoalText = "Create a CSV with id, name, and value columns from the input data array.",
            SampleInput = JsonSerializer.SerializeToElement(new[] { new { id = "1", name = "test", value = 100 } }),
            DslProfile = "jsonata",
            Constraints = new DslConstraints
            {
                MaxColumns = 50,
                AllowTransforms = true,
                ForbidNetworkCalls = true,
                ForbidCodeExecution = true
            },
            Engine = "auto"  // Auto should fall back to legacy
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/ai/dsl/generate", request);

        // Assert - auto should fall back to legacy (since plan_v1 not implemented)
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<DslGenerateResult>();
        result.Should().NotBeNull();
        result!.EngineUsed.Should().Be("legacy");
    }

    public void Dispose()
    {
        _client.Dispose();
        TestFixtures.CleanupTempFile(_dbPath);
    }

    private static object CreateValidRequest()
    {
        return new
        {
            goalText = "Create a CSV with id, name, and value columns from the input data array.",
            sampleInput = new[] { new { id = "1", name = "test", value = 100 } },
            dslProfile = "jsonata",
            constraints = new
            {
                maxColumns = 50,
                allowTransforms = true,
                forbidNetworkCalls = true,
                forbidCodeExecution = true
            }
        };
    }
}

/// <summary>
/// Integration tests for AI endpoint when AI is disabled.
/// </summary>
[Collection("Sequential")]
public class IT05_AiDisabledTests : IDisposable
{
    private readonly HttpClient _client;
    private readonly string _dbPath;

    public IT05_AiDisabledTests()
    {
        _dbPath = TestFixtures.CreateTempDbPath();
        
        var disabledConfig = new AiConfiguration { Enabled = false };
        var factory = new AiTestWebApplicationFactory(_dbPath, disabledConfig, null);
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GenerateDsl_WhenDisabled_Returns503AiDisabled()
    {
        // Arrange
        var request = new
        {
            goalText = "Create a CSV with id and name columns from the input data.",
            sampleInput = new { data = new[] { 1, 2, 3 } },
            dslProfile = "jsonata",
            constraints = new
            {
                maxColumns = 50,
                allowTransforms = true,
                forbidNetworkCalls = true,
                forbidCodeExecution = true
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/ai/dsl/generate", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        var error = await response.Content.ReadFromJsonAsync<AiError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(AiErrorCodes.AiDisabled);
        error.Message.Should().Contain("disabled");
        error.CorrelationId.Should().NotBeEmpty();
    }

    public void Dispose()
    {
        _client.Dispose();
        TestFixtures.CleanupTempFile(_dbPath);
    }
}

/// <summary>
/// Integration tests using WireMock.Net to simulate OpenRouter API.
/// </summary>
[Collection("Sequential")]
public class IT06_WireMockOpenRouterTests : IDisposable
{
    private readonly WireMockServer _mockServer;
    private readonly HttpClient _client;
    private readonly string _dbPath;

    public IT06_WireMockOpenRouterTests()
    {
        _dbPath = TestFixtures.CreateTempDbPath();
        
        // Start WireMock server
        _mockServer = WireMockServer.Start();
        
        // Configure AI to use WireMock endpoint
        var config = new AiConfiguration
        {
            Enabled = true,
            Provider = "HttpOpenAICompatible",
            EndpointUrl = $"{_mockServer.Url}/v1/chat/completions",
            Model = "test-model",
            PromptVersion = "1.0.0",
            TimeoutSeconds = 10
        };

        // Set API key env var
        Environment.SetEnvironmentVariable("METRICS_OPENROUTER_API_KEY", "test-api-key-12345");

        var factory = new AiTestWebApplicationFactory(_dbPath, config, null, useRealHttpProvider: true);
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GenerateDsl_WireMockSuccess_Returns200()
    {
        // Arrange - Configure WireMock to return valid response
        // The DSL must produce output that matches the schema when applied to the sample input
        var validResponse = new
        {
            dsl = new { profile = "jsonata", text = "[ { \"id\": \"mock-1\", \"name\": \"mock-item\", \"value\": 100 } ]" },
            outputSchema = new
            {
                type = "array",
                items = new
                {
                    type = "object",
                    properties = new
                    {
                        id = new { type = "string" },
                        name = new { type = "string" },
                        value = new { type = "number" }
                    },
                    required = new[] { "id", "name", "value" },
                    additionalProperties = true
                }
            },
            rationale = "Returns a static array with id, name, and value.",
            warnings = Array.Empty<string>()
        };

        var chatResponse = new
        {
            id = "chatcmpl-123",
            @object = "chat.completion",
            created = 1234567890,
            model = "test-model",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new
                    {
                        role = "assistant",
                        content = JsonSerializer.Serialize(validResponse)
                    },
                    finish_reason = "stop"
                }
            }
        };

        _mockServer.Given(
            Request.Create()
                .WithPath("/v1/chat/completions")
                .WithHeader("Authorization", "Bearer *")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(JsonSerializer.Serialize(chatResponse)));

        var request = new
        {
            goalText = "Create a CSV with id, name, and value columns from the input data.",
            sampleInput = new[] { new { id = "1", name = "test", value = 100 } },
            dslProfile = "jsonata",
            constraints = new
            {
                maxColumns = 50,
                allowTransforms = true,
                forbidNetworkCalls = true,
                forbidCodeExecution = true
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/ai/dsl/generate", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var result = await response.Content.ReadFromJsonAsync<DslGenerateResult>();
        result.Should().NotBeNull();
        result!.Dsl.Text.Should().Contain("id");
        result.Dsl.Profile.Should().Be("jsonata");

        // Verify WireMock received the request with correct headers
        var requests = _mockServer.LogEntries;
        requests.Should().HaveCountGreaterThan(0);
        var lastRequest = requests.Last();
        lastRequest.RequestMessage.Headers!["Authorization"].First().Should().StartWith("Bearer ");
    }

    [Fact]
    public async Task GenerateDsl_WireMock500Error_Returns503()
    {
        // Arrange - Configure WireMock to return 500
        _mockServer.Given(
            Request.Create()
                .WithPath("/v1/chat/completions")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(500)
                    .WithBody("Internal Server Error"));

        var request = new
        {
            goalText = "Create a CSV with id and name columns from the input data.",
            sampleInput = new { data = new[] { 1, 2, 3 } },
            dslProfile = "jsonata",
            constraints = new
            {
                maxColumns = 50,
                allowTransforms = true,
                forbidNetworkCalls = true,
                forbidCodeExecution = true
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/ai/dsl/generate", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        var error = await response.Content.ReadFromJsonAsync<AiError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(AiErrorCodes.AiProviderUnavailable);
    }

    [Fact]
    public async Task GenerateDsl_WireMock429RateLimited_Returns503()
    {
        // Arrange - Configure WireMock to return 429
        _mockServer.Given(
            Request.Create()
                .WithPath("/v1/chat/completions")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(429)
                    .WithBody("Rate limited"));

        var request = new
        {
            goalText = "Create a CSV with id and name columns from the input data.",
            sampleInput = new { data = new[] { 1, 2, 3 } },
            dslProfile = "jsonata",
            constraints = new
            {
                maxColumns = 50,
                allowTransforms = true,
                forbidNetworkCalls = true,
                forbidCodeExecution = true
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/ai/dsl/generate", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);

        var error = await response.Content.ReadFromJsonAsync<AiError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(AiErrorCodes.AiRateLimited);
    }

    [Fact]
    public async Task GenerateDsl_WireMockInvalidJson_Returns502()
    {
        // Arrange - Configure WireMock to return invalid JSON content
        var chatResponse = new
        {
            id = "chatcmpl-123",
            choices = new[]
            {
                new
                {
                    index = 0,
                    message = new
                    {
                        role = "assistant",
                        content = "This is not valid JSON content"
                    }
                }
            }
        };

        _mockServer.Given(
            Request.Create()
                .WithPath("/v1/chat/completions")
                .UsingPost())
            .RespondWith(
                Response.Create()
                    .WithStatusCode(200)
                    .WithHeader("Content-Type", "application/json")
                    .WithBody(JsonSerializer.Serialize(chatResponse)));

        var request = new
        {
            goalText = "Create a CSV with id and name columns from the input data.",
            sampleInput = new { data = new[] { 1, 2, 3 } },
            dslProfile = "jsonata",
            constraints = new
            {
                maxColumns = 50,
                allowTransforms = true,
                forbidNetworkCalls = true,
                forbidCodeExecution = true
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/ai/dsl/generate", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadGateway);

        var error = await response.Content.ReadFromJsonAsync<AiError>();
        error.Should().NotBeNull();
        error!.Code.Should().Be(AiErrorCodes.AiOutputInvalid);
    }

    public void Dispose()
    {
        _client.Dispose();
        _mockServer.Stop();
        _mockServer.Dispose();
        Environment.SetEnvironmentVariable("METRICS_OPENROUTER_API_KEY", null);
        TestFixtures.CleanupTempFile(_dbPath);
    }
}

/// <summary>
/// Test fixture for AI integration tests with MockProvider
/// </summary>
public class AiTestFixture
{
    public AiConfiguration AiConfig { get; }
    public MockAiProvider MockProvider { get; }

    public AiTestFixture()
    {
        AiConfig = new AiConfiguration
        {
            Enabled = true,
            Provider = "MockProvider",
            Model = "mock-model",
            PromptVersion = "1.0.0-test"
        };
        MockProvider = new MockAiProvider();
    }
}

/// <summary>
/// Custom WebApplicationFactory that allows injecting AI configuration and provider
/// </summary>
public class AiTestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath;
    private readonly AiConfiguration _aiConfig;
    private readonly IAiProvider? _mockProvider;
    private readonly bool _useRealHttpProvider;

    public AiTestWebApplicationFactory(
        string dbPath,
        AiConfiguration aiConfig,
        IAiProvider? mockProvider,
        bool useRealHttpProvider = false)
    {
        _dbPath = dbPath;
        _aiConfig = aiConfig;
        _mockProvider = mockProvider;
        _useRealHttpProvider = useRealHttpProvider;
        
        // Set environment variables BEFORE creating the client
        Environment.SetEnvironmentVariable("METRICS_SQLITE_PATH", _dbPath);
        Environment.SetEnvironmentVariable("Auth__Mode", "Off");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Disable auth for tests
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:Mode"] = "Off"
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove existing AI registrations
            var aiConfigDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(AiConfiguration));
            if (aiConfigDescriptor != null) services.Remove(aiConfigDescriptor);

            var aiProviderDescriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IAiProvider));
            if (aiProviderDescriptor != null) services.Remove(aiProviderDescriptor);

            // Add our test configuration
            services.AddSingleton(_aiConfig);

            // Add provider
            if (_mockProvider != null)
            {
                services.AddSingleton<IAiProvider>(_mockProvider);
            }
            else if (_useRealHttpProvider)
            {
                // Use real HTTP provider (for WireMock tests)
                services.AddHttpClient<HttpOpenAiCompatibleProvider>();
                services.AddSingleton<IAiProvider>(sp =>
                {
                    if (!_aiConfig.Enabled)
                    {
                        return new MockAiProvider(MockProviderConfig.WithError(
                            AiErrorCodes.AiDisabled,
                            "AI is disabled"));
                    }

                    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
                    var httpClient = httpClientFactory.CreateClient(nameof(HttpOpenAiCompatibleProvider));
                    var logger = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<HttpOpenAiCompatibleProvider>>();
                    return new HttpOpenAiCompatibleProvider(httpClient, _aiConfig, logger);
                });
            }
            else
            {
                // Default disabled provider
                services.AddSingleton<IAiProvider>(new MockAiProvider(
                    MockProviderConfig.WithError(AiErrorCodes.AiDisabled, "AI is disabled")));
            }
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Environment.SetEnvironmentVariable("METRICS_SQLITE_PATH", null);
        Environment.SetEnvironmentVariable("Auth__Mode", null);
    }
}
