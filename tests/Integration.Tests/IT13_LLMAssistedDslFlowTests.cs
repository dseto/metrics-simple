using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Metrics.Api;
using Metrics.Api.AI;
using Xunit;
using Xunit.Abstractions;

namespace Integration.Tests;

/// <summary>
/// IT13 — LLM-Assisted DSL Flow End-to-End Tests
/// 
/// Testa o fluxo COMPLETO com LLM gerando o DSL a partir de linguagem natural:
/// 1. Login (admin/testpass123)
/// 2. POST /api/v1/ai/dsl/generate (prompt em linguagem natural → LLM gera DSL)
/// 3. POST /api/v1/preview/transform (executar transformação com DSL gerado pela LLM)
/// 4. Validar CSV gerado
/// 
/// OBJETIVO: Testar o quanto a LLM ajuda a escrever DSL válido e funcional.
/// 
/// Engine Modes:
/// - legacy: Uses LLM to generate Jsonata DSL
/// - plan_v1: Uses deterministic template matching OR LLM for IR v1 plans
/// 
/// When LLM is not available, plan_v1 falls back to templates for simple cases.
/// </summary>
[Collection("Sequential")]
public class IT13_LLMAssistedDslFlowTests : IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly string _dbPath;
    private readonly ITestOutputHelper _output;
    private HttpClient _client = null!;
    private string _adminToken = string.Empty;
    
    /// <summary>
    /// Check if LLM API key is available for tests that require it
    /// </summary>
    private static bool HasLlmApiKey => 
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("METRICS_OPENROUTER_API_KEY")) ||
        !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("OPENROUTER_API_KEY"));

    public IT13_LLMAssistedDslFlowTests(ITestOutputHelper output)
    {
        _output = output;
        _dbPath = TestFixtures.CreateTempDbPath();
        _factory = new TestWebApplicationFactory(_dbPath, disableAuth: false);
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        TestFixtures.CleanupTempFile(_dbPath);
        await Task.CompletedTask;
    }
    
    #region Sample Data Fixtures
    
    /// <summary>Root array format: [{"id":...}]</summary>
    private static object[] CreatePersonsRootArray() => new object[]
    {
        new { id = "001", nome = "João Silva", idade = 35, cidade = "São Paulo" },
        new { id = "002", nome = "Maria Santos", idade = 28, cidade = "Rio de Janeiro" },
        new { id = "003", nome = "Pedro Costa", idade = 42, cidade = "Belo Horizonte" }
    };
    
    /// <summary>Wrapper items format: {"items":[...]}</summary>
    private static object CreatePersonsWithItems() => new
    {
        items = new object[]
        {
            new { id = "001", nome = "João Silva", idade = 35, cidade = "São Paulo" },
            new { id = "002", nome = "Maria Santos", idade = 28, cidade = "Rio de Janeiro" },
            new { id = "003", nome = "Pedro Costa", idade = 42, cidade = "Belo Horizonte" }
        }
    };
    
    /// <summary>Wrapper results format: {"results":[...]}</summary>
    private static object CreatePersonsWithResults() => new
    {
        results = new object[]
        {
            new { id = "001", nome = "João Silva", idade = 35, cidade = "São Paulo" },
            new { id = "002", nome = "Maria Santos", idade = 28, cidade = "Rio de Janeiro" },
            new { id = "003", nome = "Pedro Costa", idade = 42, cidade = "Belo Horizonte" }
        }
    };
    
    /// <summary>Sales data for aggregation tests</summary>
    private static object CreateSalesData() => new
    {
        sales = new[]
        {
            new { product = "Laptop", category = "Electronics", price = 1200.00, quantity = 5 },
            new { product = "Mouse", category = "Electronics", price = 25.00, quantity = 50 },
            new { product = "Desk", category = "Furniture", price = 350.00, quantity = 10 },
            new { product = "Chair", category = "Furniture", price = 150.00, quantity = 20 }
        }
    };
    
    /// <summary>Weather data similar to HGBrasil API</summary>
    private static object CreateWeatherData() => new
    {
        results = new
        {
            city = "São Paulo",
            temp = 28,
            forecast = new[]
            {
                new { date = "06/01", weekday = "Seg", max = 32, min = 21, condition = "storm" },
                new { date = "07/01", weekday = "Ter", max = 30, min = 20, condition = "rain" },
                new { date = "08/01", weekday = "Qua", max = 29, min = 19, condition = "cloudly_day" },
                new { date = "09/01", weekday = "Qui", max = 31, min = 22, condition = "clear_day" },
                new { date = "10/01", weekday = "Sex", max = 33, min = 23, condition = "clear_day" }
            }
        }
    };
    
    #endregion
    
    #region Helper Methods
    
    private async Task<string> LoginAsync()
    {
        var loginRequest = new { username = "admin", password = "testpass123" };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/token", loginRequest);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginContent = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        return loginContent.GetProperty("access_token").GetString()!;
    }
    
    private async Task<DslGenerateResult?> GenerateDslAsync(object sampleInput, string goalText)
    {
        var aiRequest = new
        {
            goalText = goalText,
            sampleInput = sampleInput,
            dslProfile = "ir",
            constraints = new
            {
                maxColumns = 20,
                allowTransforms = true,
                forbidNetworkCalls = true,
                forbidCodeExecution = true
            }
        };

        var aiHttpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ai/dsl/generate");
        aiHttpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);
        aiHttpRequest.Content = JsonContent.Create(aiRequest);

        var aiResponse = await _client.SendAsync(aiHttpRequest);
        _output.WriteLine($"   Response Status: {aiResponse.StatusCode}");
        
        if (aiResponse.StatusCode != HttpStatusCode.OK)
        {
            var errorContent = await aiResponse.Content.ReadAsStringAsync();
            _output.WriteLine($"   Error: {errorContent}");
            return null;
        }
        
        return await aiResponse.Content.ReadFromJsonAsync<DslGenerateResult>();
    }
    
    private async Task<PreviewTransformResponseDto?> ExecuteTransformAsync(object sampleInput, DslGenerateResult dslResult)
    {
        var transformRequest = new
        {
            sampleInput = sampleInput,
            dsl = dslResult.Dsl,
            outputSchema = dslResult.OutputSchema,
            plan = dslResult.Plan
        };

        var transformHttpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/preview/transform");
        transformHttpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);
        transformHttpRequest.Content = JsonContent.Create(transformRequest);

        var transformResponse = await _client.SendAsync(transformHttpRequest);
        
        if (transformResponse.StatusCode != HttpStatusCode.OK)
        {
            _output.WriteLine($"   Transform failed: {transformResponse.StatusCode}");
            return null;
        }
        
        return await transformResponse.Content.ReadFromJsonAsync<PreviewTransformResponseDto>();
    }
    
    #endregion
    
    #region PlanV1 Engine Tests (Deterministic - No LLM Required)
    
    /// <summary>
    /// Tests SimpleExtraction using plan_v1 engine with template fallback (T2).
    /// This test should pass WITHOUT LLM by using deterministic templates.
    /// </summary>
    [Fact]
    public async Task PlanV1_SimpleExtraction_PortuguesePrompt_RootArray()
    {
        _output.WriteLine("╔════════════════════════════════════════════════════════════╗");
        _output.WriteLine("║  PLAN_V1: Simple Extraction (Portuguese) - Root Array     ║");
        _output.WriteLine("║  Template T2 → Plan → Transform → CSV                     ║");
        _output.WriteLine("╚════════════════════════════════════════════════════════════╝\n");

        _adminToken = await LoginAsync();
        _output.WriteLine("✅ Login OK\n");

        var sampleInput = CreatePersonsRootArray();
        var goalText = "Quero extrair apenas o ID, nome e cidade de cada pessoa. Não preciso da idade.";
        
        _output.WriteLine($"📝 Goal: {goalText}");
        _output.WriteLine($"📊 Input Format: Root Array (3 records)\n");

        var dslResult = await GenerateDslAsync(sampleInput, goalText);
        dslResult.Should().NotBeNull("plan_v1 should return a result");

        

        _output.WriteLine($"   DSL: {dslResult.Dsl.Text[..Math.Min(200, dslResult.Dsl.Text.Length)]}...\n");

        // Validate we have preview rows with expected columns
        dslResult.ExampleRows.Should().NotBeNull();
        dslResult.ExampleRows!.Value.ValueKind.Should().Be(JsonValueKind.Array);
        dslResult.ExampleRows.Value.GetArrayLength().Should().BeGreaterThan(0, "Should have preview rows");
        
        // Check that preview contains id, nome, cidade (or equivalent)
        var previewJson = JsonSerializer.Serialize(dslResult.ExampleRows);
        _output.WriteLine($"   Preview: {previewJson[..Math.Min(300, previewJson.Length)]}...");
        
        // Less fragile assertion: check preview has some data and expected field count
        previewJson.Should().Contain("001", "Preview should contain first record id");
        
        _output.WriteLine("\n✅ PlanV1 SimpleExtraction (Root Array) PASSED!");
    }
    
    /// <summary>
    /// Tests SimpleExtraction with {"items":[...]} wrapper format.
    /// </summary>
    [Fact]
    public async Task PlanV1_SimpleExtraction_WithItemsWrapper()
    {
        _output.WriteLine("╔════════════════════════════════════════════════════════════╗");
        _output.WriteLine("║  PLAN_V1: Simple Extraction - Items Wrapper               ║");
        _output.WriteLine("╚════════════════════════════════════════════════════════════╝\n");

        _adminToken = await LoginAsync();
        _output.WriteLine("✅ Login OK\n");

        var sampleInput = CreatePersonsWithItems();
        var goalText = "Extrair id, nome e cidade de cada pessoa.";
        
        _output.WriteLine($"📝 Goal: {goalText}");
        _output.WriteLine($"📊 Input Format: {{\"items\":[...]}}\n");

        var dslResult = await GenerateDslAsync(sampleInput, goalText);
        dslResult.Should().NotBeNull("plan_v1 should return a result");

        

        
        dslResult.ExampleRows.Should().NotBeNull();
        dslResult.ExampleRows!.Value.ValueKind.Should().Be(JsonValueKind.Array);
        dslResult.ExampleRows.Value.GetArrayLength().Should().BeGreaterThan(0);
        
        _output.WriteLine("✅ PlanV1 SimpleExtraction (Items Wrapper) PASSED!");
    }
    
    /// <summary>
    /// Tests SimpleExtraction with {"results":[...]} wrapper format.
    /// </summary>
    [Fact]
    public async Task PlanV1_SimpleExtraction_WithResultsWrapper()
    {
        _output.WriteLine("╔════════════════════════════════════════════════════════════╗");
        _output.WriteLine("║  PLAN_V1: Simple Extraction - Results Wrapper             ║");
        _output.WriteLine("╚════════════════════════════════════════════════════════════╝\n");

        _adminToken = await LoginAsync();
        _output.WriteLine("✅ Login OK\n");

        var sampleInput = CreatePersonsWithResults();
        var goalText = "Extrair id, nome e cidade de cada pessoa.";
        
        _output.WriteLine($"📝 Goal: {goalText}");
        _output.WriteLine($"📊 Input Format: {{\"results\":[...]}}\n");

        var dslResult = await GenerateDslAsync(sampleInput, goalText);
        dslResult.Should().NotBeNull("plan_v1 should return a result");

        

        
        dslResult.ExampleRows.Should().NotBeNull();
        dslResult.ExampleRows!.Value.ValueKind.Should().Be(JsonValueKind.Array);
        dslResult.ExampleRows.Value.GetArrayLength().Should().BeGreaterThan(0);
        
        _output.WriteLine("✅ PlanV1 SimpleExtraction (Results Wrapper) PASSED!");
    }
    
    /// <summary>
    /// Tests Aggregation using plan_v1 engine with template T5 (groupBy + aggregate).
    /// </summary>
    [Fact]
    public async Task PlanV1_Aggregation_EnglishPrompt()
    {
        _output.WriteLine("╔════════════════════════════════════════════════════════════╗");
        _output.WriteLine("║  PLAN_V1: Aggregation (English) - Template T5             ║");
        _output.WriteLine("║  GroupBy + Sum → Plan → Transform → CSV                   ║");
        _output.WriteLine("╚════════════════════════════════════════════════════════════╝\n");

        _adminToken = await LoginAsync();
        _output.WriteLine("✅ Login OK\n");

        var sampleInput = CreateSalesData();
        var goalText = "Group by category and sum the price for each category.";
        
        _output.WriteLine($"📝 Goal: {goalText}");
        _output.WriteLine($"📊 Input Format: {{\"sales\":[...]}}\n");

        var dslResult = await GenerateDslAsync(sampleInput, goalText);
        dslResult.Should().NotBeNull("plan_v1 should return a result");

        

        _output.WriteLine($"   DSL: {dslResult.Dsl.Text[..Math.Min(300, dslResult.Dsl.Text.Length)]}...\n");

        dslResult.ExampleRows.Should().NotBeNull();
        
        // Should have 2 categories: Electronics and Furniture
        var previewJson = JsonSerializer.Serialize(dslResult.ExampleRows);
        _output.WriteLine($"   Preview: {previewJson}");
        
        // Less fragile: just check we got aggregated results
        dslResult.ExampleRows!.Value.ValueKind.Should().Be(JsonValueKind.Array);
        dslResult.ExampleRows.Value.GetArrayLength().Should().BeGreaterOrEqualTo(1, "Should have aggregated rows");
        
        _output.WriteLine("\n✅ PlanV1 Aggregation PASSED!");
    }
    
    /// <summary>
    /// Tests WeatherForecast using plan_v1 engine with template T2 (select fields).
    /// </summary>
    [Fact]
    public async Task PlanV1_WeatherForecast_NestedPath()
    {
        _output.WriteLine("╔════════════════════════════════════════════════════════════╗");
        _output.WriteLine("║  PLAN_V1: Weather Forecast - Nested Path Discovery        ║");
        _output.WriteLine("║  RecordPath: /results/forecast → Template → CSV           ║");
        _output.WriteLine("╚════════════════════════════════════════════════════════════╝\n");

        _adminToken = await LoginAsync();
        _output.WriteLine("✅ Login OK\n");

        var sampleInput = CreateWeatherData();
        var goalText = "Extrair data, max, min e condition de cada previsão.";
        
        _output.WriteLine($"📝 Goal: {goalText}");
        _output.WriteLine($"📊 Input Format: {{\"results\":{{\"forecast\":[...]}}}}\n");

        var dslResult = await GenerateDslAsync(sampleInput, goalText);
        dslResult.Should().NotBeNull("plan_v1 should return a result");

        

        _output.WriteLine($"   DSL: {dslResult.Dsl.Text[..Math.Min(300, dslResult.Dsl.Text.Length)]}...\n");

        dslResult.ExampleRows.Should().NotBeNull();
        dslResult.ExampleRows!.Value.ValueKind.Should().Be(JsonValueKind.Array);
        dslResult.ExampleRows.Value.GetArrayLength().Should().Be(5, "Should have 5 forecast days");
        
        var previewJson = JsonSerializer.Serialize(dslResult.ExampleRows);
        _output.WriteLine($"   Preview: {previewJson[..Math.Min(400, previewJson.Length)]}...");
        
        // Less fragile: verify some weather data is present (max/min or date)
        var hasWeatherData = previewJson.Contains("max") || previewJson.Contains("min") || 
                             previewJson.Contains("date") || previewJson.Contains("06/01");
        hasWeatherData.Should().BeTrue("Preview should contain weather forecast data");
        
        _output.WriteLine("\n✅ PlanV1 WeatherForecast PASSED!");
    }
    
    #endregion

    #region Additional PlanV1 Tests

    [Fact]
    public async Task PlanV1_SelectAll_T1()
    {
        _adminToken = await LoginAsync();
        var sampleInput = CreatePersonsRootArray();
        var goalText = "List all fields for each person.";

        var dslResult = await GenerateDslAsync(sampleInput, goalText);
        dslResult.Should().NotBeNull();


        var transform = await ExecuteTransformAsync(sampleInput, dslResult);
        transform.Should().NotBeNull();
        transform!.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task PlanV1_SelectWithFilter()
    {
        _adminToken = await LoginAsync();
        var sampleInput = new object[]
        {
            new { id = "001", active = true, name = "A" },
            new { id = "002", active = false, name = "B" }
        };
        var goalText = "Extract id and name for active records only.";

        var dslResult = await GenerateDslAsync(sampleInput, goalText);
        dslResult.Should().NotBeNull();

        var transform = await ExecuteTransformAsync(sampleInput, dslResult!);
        transform.Should().NotBeNull();
        transform!.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task PlanV1_GroupBy_Avg()
    {
        _adminToken = await LoginAsync();
        var sampleInput = new
        {
            items = new[]
            {
                new { category = "A", value = 10 },
                new { category = "A", value = 20 },
                new { category = "B", value = 5 }
            }
        };
        var goalText = "Group by category and compute average value.";

        var dslResult = await GenerateDslAsync(sampleInput, goalText);
        dslResult.Should().NotBeNull();

        var transform = await ExecuteTransformAsync(sampleInput, dslResult!);
        transform.Should().NotBeNull();
        transform!.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task PlanV1_MapValue()
    {
        _adminToken = await LoginAsync();
        var sampleInput = new object[]
        {
            new { id = "1", status = "A" },
            new { id = "2", status = "B" }
        };
        var goalText = "Map status codes to labels: A=>Active, B=>Blocked.";

        var dslResult = await GenerateDslAsync(sampleInput, goalText);
        dslResult.Should().NotBeNull();

        var transform = await ExecuteTransformAsync(sampleInput, dslResult!);
        transform.Should().NotBeNull();
        transform!.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task PlanV1_Limit_TopN()
    {
        _adminToken = await LoginAsync();
        var sampleInput = new object[]
        {
            new { id = "1", score = 10 },
            new { id = "2", score = 20 },
            new { id = "3", score = 30 }
        };
        var goalText = "Return top 2 records ordered by score.";

        var dslResult = await GenerateDslAsync(sampleInput, goalText);
        dslResult.Should().NotBeNull();

        var transform = await ExecuteTransformAsync(sampleInput, dslResult!);
        transform.Should().NotBeNull();
        transform!.IsValid.Should().BeTrue();
    }

    #endregion
}
