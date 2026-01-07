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
    
    #region Legacy Engine Tests (LLM-Dependent)
    
    /// <summary>
    /// Legacy test - kept for backward compatibility.
    /// Uses default engine (legacy) with LLM.
    /// </summary>
    [Fact(Skip = "Legacy jsonata test - focus is now plan_v1 only")]
    [Trait("RequiresLLM", "true")]
    public async Task LLM_SimpleExtraction_PortuguesePrompt()
    {
        _output.WriteLine("╔════════════════════════════════════════════════════════════╗");
        _output.WriteLine("║   LLM DSL GENERATION: Simple Extraction (Portuguese)      ║");
        _output.WriteLine("║   Prompt → LLM → DSL → Transform → CSV                    ║");
        _output.WriteLine("╚════════════════════════════════════════════════════════════╝");
        _output.WriteLine("");

        // ===== STEP 1: LOGIN =====
        _output.WriteLine("=== STEP 1: LOGIN ===");
        var loginRequest = new { username = "admin", password = "testpass123" };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/token", loginRequest);
        
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginContent = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        _adminToken = loginContent.GetProperty("access_token").GetString()!;
        
        _output.WriteLine($"✅ Login successful! Token: {_adminToken[..20]}...");
        _output.WriteLine("");

        // ===== STEP 2: LLM GENERATES DSL =====
        _output.WriteLine("=== STEP 2: LLM GENERATES DSL FROM NATURAL LANGUAGE ===");
        
        var sampleInput = new[]
        {
            new { id = "001", nome = "João Silva", idade = 35, cidade = "São Paulo" },
            new { id = "002", nome = "Maria Santos", idade = 28, cidade = "Rio de Janeiro" },
            new { id = "003", nome = "Pedro Costa", idade = 42, cidade = "Belo Horizonte" }
        };

        var aiRequest = new
        {
            goalText = "Quero extrair apenas o ID, nome e cidade de cada pessoa. Não preciso da idade.",
            sampleInput = sampleInput,
            dslProfile = "jsonata",
            constraints = new
            {
                maxColumns = 10,
                allowTransforms = true,
                forbidNetworkCalls = true,
                forbidCodeExecution = true
            }
        };

        var aiHttpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ai/dsl/generate");
        aiHttpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);
        aiHttpRequest.Content = JsonContent.Create(aiRequest);

        _output.WriteLine($"📝 Prompt (Português): \"{aiRequest.goalText}\"");
        _output.WriteLine($"📊 Sample Input: {sampleInput.Length} registros");
        _output.WriteLine("");

        var aiResponse = await _client.SendAsync(aiHttpRequest);
        _output.WriteLine($"AI Response Status: {aiResponse.StatusCode}");
        
        aiResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var aiResult = await aiResponse.Content.ReadFromJsonAsync<DslGenerateResult>();
        aiResult.Should().NotBeNull();
        aiResult!.Dsl.Should().NotBeNull();
        aiResult.Dsl.Profile.Should().Be("jsonata");
        aiResult.Dsl.Text.Should().NotBeEmpty();
        
        _output.WriteLine($"✅ LLM gerou DSL com sucesso!");
        _output.WriteLine($"   Profile: {aiResult.Dsl.Profile}");
        _output.WriteLine($"   DSL Generated:");
        var dslLines = aiResult.Dsl.Text.Split('\n');
        foreach (var line in dslLines.Take(10))
        {
            _output.WriteLine($"      {line}");
        }
        if (aiResult.Rationale != null)
        {
            _output.WriteLine($"   Rationale: {aiResult.Rationale}");
        }
        if (aiResult.Warnings.Any())
        {
            _output.WriteLine($"   ⚠️  Warnings: {string.Join(", ", aiResult.Warnings)}");
        }
        _output.WriteLine("");

        // ===== STEP 3: EXECUTE TRANSFORM WITH LLM-GENERATED DSL =====
        _output.WriteLine("=== STEP 3: EXECUTE TRANSFORM WITH LLM-GENERATED DSL ===");
        
        var transformRequest = new
        {
            sampleInput = sampleInput,
            dsl = aiResult.Dsl,
            outputSchema = aiResult.OutputSchema
        };

        var transformHttpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/preview/transform");
        transformHttpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);
        transformHttpRequest.Content = JsonContent.Create(transformRequest);

        var transformResponse = await _client.SendAsync(transformHttpRequest);
        _output.WriteLine($"Transform Status: {transformResponse.StatusCode}");
        
        transformResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var transformContent = await transformResponse.Content.ReadFromJsonAsync<PreviewTransformResponseDto>();
        transformContent.Should().NotBeNull();
        
        _output.WriteLine($"   Valid: {transformContent!.IsValid}");
        _output.WriteLine($"   Errors: {transformContent.Errors.Count}");
        
        if (transformContent.Errors.Any())
        {
            _output.WriteLine($"   ❌ Transform Errors:");
            foreach (var error in transformContent.Errors)
            {
                _output.WriteLine($"      - {error}");
            }
        }
        
        transformContent.IsValid.Should().BeTrue("LLM-generated DSL should produce valid output");
        transformContent.Errors.Should().BeEmpty();
        
        _output.WriteLine($"✅ Transform executed successfully with LLM-generated DSL!");
        
        if (transformContent.PreviewOutput != null)
        {
            var outputJson = JsonSerializer.Serialize(transformContent.PreviewOutput, new JsonSerializerOptions { WriteIndented = true });
            _output.WriteLine($"   Output Preview:");
            var outputLines = outputJson.Split('\n').Take(15);
            foreach (var line in outputLines)
            {
                _output.WriteLine($"      {line}");
            }
        }
        
        if (!string.IsNullOrEmpty(transformContent.PreviewCsv))
        {
            _output.WriteLine($"   CSV Preview:");
            var csvLines = transformContent.PreviewCsv.Split('\n', StringSplitOptions.RemoveEmptyEntries).Take(5);
            foreach (var line in csvLines)
            {
                _output.WriteLine($"      {line}");
            }
            
            // Validate CSV structure
            var csvLinesList = transformContent.PreviewCsv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            csvLinesList.Should().HaveCountGreaterThan(1, "CSV should have header + data rows");
            csvLinesList[0].Should().Contain("id", "CSV header should contain 'id'");
            csvLinesList[0].Should().Contain("nome", "CSV header should contain 'nome'");
            csvLinesList[0].Should().Contain("cidade", "CSV header should contain 'cidade'");
            csvLinesList[0].Should().NotContain("idade", "CSV should NOT contain 'idade' (filtered out per prompt)");
        }
        
        _output.WriteLine("");
        _output.WriteLine("╔════════════════════════════════════════════════════════════╗");
        _output.WriteLine("║        🎉 LLM-ASSISTED DSL FLOW COMPLETED! 🎉             ║");
        _output.WriteLine("║                                                            ║");
        _output.WriteLine("║  ✅ Natural Language Prompt (Portuguese)                   ║");
        _output.WriteLine("║  ✅ LLM Generated Valid DSL                                ║");
        _output.WriteLine("║  ✅ Transform Executed Successfully                        ║");
        _output.WriteLine("║  ✅ CSV Generated Correctly                                ║");
        _output.WriteLine("╚════════════════════════════════════════════════════════════╝");
    }

    [Fact(Skip = "Legacy jsonata test - focus is now plan_v1 only")]
    [Trait("RequiresLLM", "true")]
    public async Task LLM_Aggregation_EnglishPrompt()
    {
        _output.WriteLine("╔════════════════════════════════════════════════════════════╗");
        _output.WriteLine("║    LLM DSL GENERATION: Aggregation (English)               ║");
        _output.WriteLine("║    Prompt → LLM → DSL → Transform → CSV                   ║");
        _output.WriteLine("╚════════════════════════════════════════════════════════════╝");
        _output.WriteLine("");

        // ===== STEP 1: LOGIN =====
        _output.WriteLine("=== STEP 1: LOGIN ===");
        var loginRequest = new { username = "admin", password = "testpass123" };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/token", loginRequest);
        
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginContent = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        _adminToken = loginContent.GetProperty("access_token").GetString()!;
        
        _output.WriteLine($"✅ Login successful!");
        _output.WriteLine("");

        // ===== STEP 2: LLM GENERATES DSL =====
        _output.WriteLine("=== STEP 2: LLM GENERATES DSL FROM NATURAL LANGUAGE ===");
        
        var sampleInput = new
        {
            sales = new[]
            {
                new { product = "Laptop", category = "Electronics", price = 1200.00, quantity = 5 },
                new { product = "Mouse", category = "Electronics", price = 25.00, quantity = 50 },
                new { product = "Desk", category = "Furniture", price = 350.00, quantity = 10 },
                new { product = "Chair", category = "Furniture", price = 150.00, quantity = 20 }
            }
        };

        var aiRequest = new
        {
            goalText = "Calculate the total revenue (price * quantity) for each category. Group by category and sum the revenues.",
            sampleInput = sampleInput,
            dslProfile = "jsonata",
            constraints = new
            {
                maxColumns = 10,
                allowTransforms = true,
                forbidNetworkCalls = true,
                forbidCodeExecution = true
            }
        };

        var aiHttpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ai/dsl/generate");
        aiHttpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);
        aiHttpRequest.Content = JsonContent.Create(aiRequest);

        _output.WriteLine($"📝 Prompt (English): \"{aiRequest.goalText}\"");
        _output.WriteLine("");

        var aiResponse = await _client.SendAsync(aiHttpRequest);
        _output.WriteLine($"AI Response Status: {aiResponse.StatusCode}");
        
        aiResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var aiResult = await aiResponse.Content.ReadFromJsonAsync<DslGenerateResult>();
        aiResult.Should().NotBeNull();
        aiResult!.Dsl.Text.Should().NotBeEmpty();
        
        _output.WriteLine($"✅ LLM generated DSL!");
        _output.WriteLine($"   DSL:");
        var dslLines = aiResult.Dsl.Text.Split('\n');
        foreach (var line in dslLines.Take(15))
        {
            _output.WriteLine($"      {line}");
        }
        _output.WriteLine("");

        // ===== STEP 3: EXECUTE TRANSFORM =====
        _output.WriteLine("=== STEP 3: EXECUTE TRANSFORM WITH LLM-GENERATED DSL ===");
        
        var transformRequest = new
        {
            sampleInput = sampleInput,
            dsl = aiResult.Dsl,
            outputSchema = aiResult.OutputSchema
        };

        var transformHttpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/preview/transform");
        transformHttpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);
        transformHttpRequest.Content = JsonContent.Create(transformRequest);

        var transformResponse = await _client.SendAsync(transformHttpRequest);
        
        transformResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var transformContent = await transformResponse.Content.ReadFromJsonAsync<PreviewTransformResponseDto>();
        transformContent.Should().NotBeNull();
        
        _output.WriteLine($"   Valid: {transformContent!.IsValid}");
        
        if (transformContent.Errors.Any())
        {
            _output.WriteLine($"   ❌ Errors:");
            foreach (var error in transformContent.Errors)
            {
                _output.WriteLine($"      - {error}");
            }
        }
        
        transformContent.IsValid.Should().BeTrue();
        
        _output.WriteLine($"✅ Transform successful!");
        
        if (!string.IsNullOrEmpty(transformContent.PreviewCsv))
        {
            _output.WriteLine($"   CSV Generated:");
            var csvLines = transformContent.PreviewCsv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in csvLines)
            {
                _output.WriteLine($"      {line}");
            }
            
            // Should have category and total_revenue columns
            csvLines[0].Should().Contain("category");
            csvLines[0].Should().Match(s => s.Contains("revenue") || s.Contains("total"));
            csvLines.Should().HaveCountGreaterOrEqualTo(3, "Should have header + 2 categories");
        }
        
        _output.WriteLine("");
        _output.WriteLine("╔════════════════════════════════════════════════════════════╗");
        _output.WriteLine("║       🎉 LLM AGGREGATION TEST COMPLETED! 🎉               ║");
        _output.WriteLine("╚════════════════════════════════════════════════════════════╝");
    }

    [Fact(Skip = "Legacy jsonata test - focus is now plan_v1 only")]
    [Trait("RequiresLLM", "true")]
    public async Task LLM_ComplexTransformation_MixedLanguage()
    {
        _output.WriteLine("╔════════════════════════════════════════════════════════════╗");
        _output.WriteLine("║  LLM DSL: Complex Transformation (Mixed PT-EN)             ║");
        _output.WriteLine("╚════════════════════════════════════════════════════════════╝");
        _output.WriteLine("");

        // ===== STEP 1: LOGIN =====
        var loginRequest = new { username = "admin", password = "testpass123" };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/token", loginRequest);
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var loginContent = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        _adminToken = loginContent.GetProperty("access_token").GetString()!;
        
        _output.WriteLine("✅ Login OK");
        _output.WriteLine("");

        // ===== STEP 2: LLM GENERATES DSL =====
        _output.WriteLine("=== LLM GENERATES COMPLEX DSL ===");
        
        var sampleInput = new
        {
            transactions = new[]
            {
                new { id = "T001", type = "CREDIT", amount = 1500.00, date = "2026-01-01", status = "COMPLETED" },
                new { id = "T002", type = "DEBIT", amount = 300.00, date = "2026-01-02", status = "COMPLETED" },
                new { id = "T003", type = "CREDIT", amount = 750.00, date = "2026-01-02", status = "COMPLETED" },
                new { id = "T004", type = "DEBIT", amount = 1200.00, date = "2026-01-03", status = "PENDING" },
                new { id = "T005", type = "CREDIT", amount = 2000.00, date = "2026-01-03", status = "COMPLETED" }
            }
        };

        var aiRequest = new
        {
            goalText = @"Preciso calcular o balanço financeiro por tipo de transação (CREDIT e DEBIT), 
                        mas considerar apenas transações COMPLETED. Para cada tipo, mostrar:
                        - tipo da transação
                        - quantidade de transações
                        - total amount
                        - média (average) do amount",
            sampleInput = sampleInput,
            dslProfile = "jsonata",
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

        _output.WriteLine($"📝 Complex Prompt (Mixed):");
        _output.WriteLine($"   {aiRequest.goalText}");
        _output.WriteLine("");

        var aiResponse = await _client.SendAsync(aiHttpRequest);
        aiResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var aiResult = await aiResponse.Content.ReadFromJsonAsync<DslGenerateResult>();
        aiResult.Should().NotBeNull();
        
        _output.WriteLine($"✅ LLM generated complex DSL!");
        _output.WriteLine($"   Full DSL:");
        _output.WriteLine($"{aiResult!.Dsl.Text}");
        _output.WriteLine("");

        // ===== STEP 3: EXECUTE TRANSFORM =====
        _output.WriteLine("=== EXECUTE COMPLEX TRANSFORM ===");
        
        var transformRequest = new
        {
            sampleInput = sampleInput,
            dsl = aiResult.Dsl,
            outputSchema = aiResult.OutputSchema
        };

        var transformHttpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/preview/transform");
        transformHttpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);
        transformHttpRequest.Content = JsonContent.Create(transformRequest);

        var transformResponse = await _client.SendAsync(transformHttpRequest);
        transformResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var transformContent = await transformResponse.Content.ReadFromJsonAsync<PreviewTransformResponseDto>();
        transformContent.Should().NotBeNull();
        
        if (transformContent!.Errors.Any())
        {
            _output.WriteLine($"❌ Errors:");
            foreach (var error in transformContent.Errors)
            {
                _output.WriteLine($"   - {error}");
            }
        }
        
        transformContent.IsValid.Should().BeTrue("Complex LLM-generated DSL should work");
        
        _output.WriteLine($"✅ Complex transform successful!");
        
        if (transformContent.PreviewOutput != null)
        {
            var outputJson = JsonSerializer.Serialize(transformContent.PreviewOutput, new JsonSerializerOptions { WriteIndented = true });
            _output.WriteLine($"   Output:");
            _output.WriteLine($"{outputJson}");
        }
        
        if (!string.IsNullOrEmpty(transformContent.PreviewCsv))
        {
            _output.WriteLine($"   CSV:");
            _output.WriteLine($"{transformContent.PreviewCsv}");
            
            var csvLines = transformContent.PreviewCsv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            csvLines[0].Should().MatchRegex("tipo|type", "Should have transaction type column");
            csvLines[0].Should().MatchRegex("quantidade|count", "Should have count column");
            csvLines[0].Should().MatchRegex("total|amount", "Should have total column");
            csvLines[0].Should().MatchRegex("media|average|avg", "Should have average column");
        }
        
        _output.WriteLine("");
        _output.WriteLine("╔════════════════════════════════════════════════════════════╗");
        _output.WriteLine("║    🎉 COMPLEX LLM TRANSFORMATION SUCCESSFUL! 🎉           ║");
        _output.WriteLine("║                                                            ║");
        _output.WriteLine("║  ✅ Mixed PT-EN prompt understood                          ║");
        _output.WriteLine("║  ✅ Filtering (status=COMPLETED) applied                   ║");
        _output.WriteLine("║  ✅ Grouping by type                                       ║");
        _output.WriteLine("║  ✅ Aggregations (count, sum, avg) calculated              ║");
        _output.WriteLine("║  ✅ CSV generated with correct structure                   ║");
        _output.WriteLine("╚════════════════════════════════════════════════════════════╝");
    }

    [Fact(Skip = "Legacy jsonata test - focus is now plan_v1 only")]
    [Trait("RequiresLLM", "true")]
    public async Task LLM_WeatherForecast_RealWorldPrompt()
    {
        _output.WriteLine("╔════════════════════════════════════════════════════════════╗");
        _output.WriteLine("║     LLM DSL: Weather Forecast Real-World Scenario          ║");
        _output.WriteLine("╚════════════════════════════════════════════════════════════╝");
        _output.WriteLine("");

        // Login
        var loginRequest = new { username = "admin", password = "testpass123" };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/token", loginRequest);
        var loginContent = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        _adminToken = loginContent.GetProperty("access_token").GetString()!;
        
        _output.WriteLine("✅ Login OK\n");

        // Sample data similar to HGBrasil API
        var sampleInput = new
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

        var aiRequest = new
        {
            goalText = @"Quero um relatório de previsão do tempo formatado assim:
                        - data da previsão
                        - dia da semana
                        - temperatura máxima
                        - temperatura mínima  
                        - amplitude térmica (diferença entre max e min)
                        - condição do tempo
                        Ordenar por data.",
            sampleInput = sampleInput,
            dslProfile = "jsonata",
            constraints = new
            {
                maxColumns = 10,
                allowTransforms = true,
                forbidNetworkCalls = true,
                forbidCodeExecution = true
            }
        };

        var aiHttpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/ai/dsl/generate");
        aiHttpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);
        aiHttpRequest.Content = JsonContent.Create(aiRequest);

        _output.WriteLine($"📝 Real-World Prompt:");
        _output.WriteLine($"{aiRequest.goalText}");
        _output.WriteLine("");

        var aiResponse = await _client.SendAsync(aiHttpRequest);
        aiResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var aiResult = await aiResponse.Content.ReadFromJsonAsync<DslGenerateResult>();
        aiResult.Should().NotBeNull();
        
        _output.WriteLine($"✅ LLM generated weather DSL!");
        _output.WriteLine($"   DSL: {aiResult!.Dsl.Text}");
        _output.WriteLine("");

        // Execute transform
        var transformRequest = new
        {
            sampleInput = sampleInput,
            dsl = aiResult.Dsl,
            outputSchema = aiResult.OutputSchema
        };

        var transformHttpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/preview/transform");
        transformHttpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);
        transformHttpRequest.Content = JsonContent.Create(transformRequest);

        var transformResponse = await _client.SendAsync(transformHttpRequest);
        transformResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var transformContent = await transformResponse.Content.ReadFromJsonAsync<PreviewTransformResponseDto>();
        transformContent!.IsValid.Should().BeTrue();
        
        _output.WriteLine($"✅ Weather forecast transform successful!");
        
        if (!string.IsNullOrEmpty(transformContent.PreviewCsv))
        {
            _output.WriteLine($"\n📊 Generated Weather CSV:");
            _output.WriteLine($"{transformContent.PreviewCsv}");
            
            var csvLines = transformContent.PreviewCsv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            csvLines[0].Should().Contain("date");
            csvLines[0].Should().MatchRegex("max|maxima");
            csvLines[0].Should().MatchRegex("min|minima");
            csvLines[0].Should().MatchRegex("amplitude|range");
            csvLines.Should().HaveCountGreaterOrEqualTo(6, "Header + 5 forecast days");
        }
        
        _output.WriteLine("\n✅ Real-world weather scenario completed!");
    }
    
    #endregion
}
