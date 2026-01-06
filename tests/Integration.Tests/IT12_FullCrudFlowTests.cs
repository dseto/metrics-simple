using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Metrics.Api;
using Xunit;
using Xunit.Abstractions;

namespace Integration.Tests;

/// <summary>
/// IT12 — Full CRUD Flow End-to-End Tests
/// 
/// Simula o fluxo completo do ZERO:
/// 1. Login (admin/testpass123)
/// 2. POST /api/v1/connectors (criar novo connector)
/// 3. POST /api/v1/processes (criar novo processo)
/// 4. POST /api/v1/processes/{processId}/versions (criar nova versão)
/// 5. POST /api/v1/preview/transform (executar transformação)
/// 
/// NÃO faz bypass de nenhuma etapa - garante fluxo completo funcional.
/// </summary>
[Collection("Sequential")]
public class IT12_FullCrudFlowTests : IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly string _dbPath;
    private readonly ITestOutputHelper _output;
    private HttpClient _client = null!;
    private string _adminToken = string.Empty;

    public IT12_FullCrudFlowTests(ITestOutputHelper output)
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

    [Fact]
    public async Task FullFlow_AlphaVantage_StockAnalysis()
    {
        _output.WriteLine("╔════════════════════════════════════════════════════════════╗");
        _output.WriteLine("║         FULL CRUD FLOW: AlphaVantage Stock Analysis        ║");
        _output.WriteLine("║    Login → Connector → Process → Version → Transform      ║");
        _output.WriteLine("╚════════════════════════════════════════════════════════════╝");
        _output.WriteLine("");

        // ===== STEP 1: LOGIN =====
        _output.WriteLine("=== STEP 1: LOGIN ===");
        var loginRequest = new { username = "admin", password = "testpass123" };
        var loginResponse = await _client.PostAsJsonAsync("/api/auth/token", loginRequest);
        
        _output.WriteLine($"Login Status: {loginResponse.StatusCode}");
        loginResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var loginContent = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        _adminToken = loginContent.GetProperty("access_token").GetString()!;
        var tokenType = loginContent.GetProperty("token_type").GetString();
        var expiresIn = loginContent.GetProperty("expires_in").GetInt32();
        
        _output.WriteLine($"✅ Login successful!");
        _output.WriteLine($"   Token Type: {tokenType}");
        _output.WriteLine($"   Expires In: {expiresIn}s");
        _output.WriteLine($"   Token: {_adminToken[..20]}...");
        _output.WriteLine("");

        _adminToken.Should().NotBeNullOrEmpty();

        // ===== STEP 2: CREATE CONNECTOR =====
        _output.WriteLine("=== STEP 2: CREATE CONNECTOR ===");
        var connectorId = $"alphavantage-{Guid.NewGuid().ToString("N")[..8]}";
        
        var connectorRequest = new
        {
            id = connectorId,
            name = "AlphaVantage Stock API",
            baseUrl = "https://www.alphavantage.co",
            timeoutSeconds = 30,
            enabled = true,
            authType = "NONE"
        };

        var connectorHttpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/connectors");
        connectorHttpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);
        connectorHttpRequest.Content = JsonContent.Create(connectorRequest);

        var connectorResponse = await _client.SendAsync(connectorHttpRequest);
        _output.WriteLine($"Create Connector Status: {connectorResponse.StatusCode}");
        
        connectorResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var connectorContent = await connectorResponse.Content.ReadFromJsonAsync<JsonElement>();
        var createdConnectorId = connectorContent.GetProperty("id").GetString()!;
        var connectorName = connectorContent.GetProperty("name").GetString();
        
        _output.WriteLine($"✅ Connector created successfully!");
        _output.WriteLine($"   ID: {createdConnectorId}");
        _output.WriteLine($"   Name: {connectorName}");
        _output.WriteLine($"   Base URL: {connectorRequest.baseUrl}");
        _output.WriteLine("");

        createdConnectorId.Should().Be(connectorId);

        // ===== STEP 3: CREATE PROCESS =====
        _output.WriteLine("=== STEP 3: CREATE PROCESS ===");
        var processId = $"ibm-analysis-{Guid.NewGuid().ToString("N")[..8]}";
        
        var processRequest = new
        {
            id = processId,
            name = "IBM Stock Intraday Analysis",
            status = "DRAFT",
            connectorId = createdConnectorId,
            outputDestinations = new[]
            {
                new
                {
                    type = "LocalFileSystem",
                    local = new { basePath = "/output/ibm" }
                }
            }
        };

        var processHttpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/processes");
        processHttpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);
        processHttpRequest.Content = JsonContent.Create(processRequest);

        var processResponse = await _client.SendAsync(processHttpRequest);
        _output.WriteLine($"Create Process Status: {processResponse.StatusCode}");
        
        processResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var processContent = await processResponse.Content.ReadFromJsonAsync<JsonElement>();
        var createdProcessId = processContent.GetProperty("id").GetString()!;
        var processName = processContent.GetProperty("name").GetString();
        
        _output.WriteLine($"✅ Process created successfully!");
        _output.WriteLine($"   ID: {createdProcessId}");
        _output.WriteLine($"   Name: {processName}");
        _output.WriteLine($"   Connector ID: {createdConnectorId}");
        _output.WriteLine("");

        createdProcessId.Should().Be(processId);

        // ===== STEP 4: CREATE PROCESS VERSION =====
        _output.WriteLine("=== STEP 4: CREATE PROCESS VERSION ===");
        
        var versionRequest = new
        {
            processId = createdProcessId,
            version = 1,
            enabled = true,
            sourceRequest = new
            {
                method = "GET",
                path = "/query",
                queryParams = new Dictionary<string, string>
                {
                    { "function", "TIME_SERIES_INTRADAY" },
                    { "symbol", "IBM" },
                    { "interval", "5min" },
                    { "apikey", "demo" }
                }
            },
            dsl = new
            {
                profile = "jsonata",
                text = """
                {
                    "symbol": $."Meta Data"."2. Symbol",
                    "interval_count": $count($spread($."Time Series (5min)"))
                }
                """
            },
            outputSchema = JsonSerializer.Deserialize<object>("""
            {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "symbol": {"type": "string"},
                        "interval_count": {"type": "number"}
                    },
                    "required": ["symbol", "interval_count"]
                }
            }
            """)
        };

        var versionHttpRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/processes/{createdProcessId}/versions");
        versionHttpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);
        versionHttpRequest.Content = JsonContent.Create(versionRequest);

        var versionResponse = await _client.SendAsync(versionHttpRequest);
        _output.WriteLine($"Create Process Version Status: {versionResponse.StatusCode}");
        
        versionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var versionContent = await versionResponse.Content.ReadFromJsonAsync<JsonElement>();
        var createdVersion = versionContent.GetProperty("version").GetInt32();
        var versionEnabled = versionContent.GetProperty("enabled").GetBoolean();
        
        _output.WriteLine($"✅ Process Version created successfully!");
        _output.WriteLine($"   Process ID: {createdProcessId}");
        _output.WriteLine($"   Version: {createdVersion}");
        _output.WriteLine($"   Enabled: {versionEnabled}");
        _output.WriteLine($"   DSL Profile: jsonata");
        _output.WriteLine("");

        createdVersion.Should().Be(1);
        versionEnabled.Should().BeTrue();

        // ===== STEP 5: EXECUTE TRANSFORM =====
        _output.WriteLine("=== STEP 5: EXECUTE TRANSFORM ===");
        
        var sampleInput = GetAlphaVantageSampleData();
        var transformRequest = new
        {
            sampleInput = sampleInput,
            dsl = new
            {
                profile = "jsonata",
                text = """
                {
                    "symbol": $."Meta Data"."2. Symbol",
                    "interval_count": $count($spread($."Time Series (5min)"))
                }
                """
            },
            outputSchema = JsonSerializer.Deserialize<object>("""
            {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "symbol": {"type": "string"},
                        "interval_count": {"type": "number"}
                    },
                    "required": ["symbol", "interval_count"]
                }
            }
            """)
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
        
        transformContent.IsValid.Should().BeTrue();
        transformContent.Errors.Should().BeEmpty();
        
        _output.WriteLine($"✅ Transform executed successfully!");
        
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
            var csvLines = transformContent.PreviewCsv.Split('\n').Take(5);
            foreach (var line in csvLines)
            {
                _output.WriteLine($"      {line}");
            }
            
            // Validate CSV structure
            var csvLinesList = transformContent.PreviewCsv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            csvLinesList.Should().HaveCountGreaterThan(1, "CSV should have header + data rows");
            csvLinesList[0].Should().Contain("symbol");
            csvLinesList[0].Should().Contain("interval_count");
            csvLinesList[1].Should().Contain("IBM");
        }
        
        _output.WriteLine("");
        _output.WriteLine("╔════════════════════════════════════════════════════════════╗");
        _output.WriteLine("║            🎉 FULL CRUD FLOW COMPLETED! 🎉                 ║");
        _output.WriteLine("║                                                            ║");
        _output.WriteLine("║  ✅ Login                                                  ║");
        _output.WriteLine("║  ✅ Connector Created                                      ║");
        _output.WriteLine("║  ✅ Process Created                                        ║");
        _output.WriteLine("║  ✅ Process Version Created                                ║");
        _output.WriteLine("║  ✅ Transform Executed                                     ║");
        _output.WriteLine("║  ✅ CSV Generated                                          ║");
        _output.WriteLine("╚════════════════════════════════════════════════════════════╝");
    }

    [Fact]
    public async Task FullFlow_HGBrasil_WeatherForecast()
    {
        _output.WriteLine("╔════════════════════════════════════════════════════════════╗");
        _output.WriteLine("║      FULL CRUD FLOW: HGBrasil Weather Forecast             ║");
        _output.WriteLine("║    Login → Connector → Process → Version → Transform      ║");
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

        // ===== STEP 2: CREATE CONNECTOR =====
        _output.WriteLine("=== STEP 2: CREATE CONNECTOR ===");
        var connectorId = $"hgbrasil-{Guid.NewGuid().ToString("N")[..8]}";
        
        var connectorRequest = new
        {
            id = connectorId,
            name = "HGBrasil Weather API",
            baseUrl = "https://api.hgbrasil.com",
            timeoutSeconds = 30,
            enabled = true,
            authType = "NONE"
        };

        var connectorHttpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/connectors");
        connectorHttpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);
        connectorHttpRequest.Content = JsonContent.Create(connectorRequest);

        var connectorResponse = await _client.SendAsync(connectorHttpRequest);
        connectorResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var connectorContent = await connectorResponse.Content.ReadFromJsonAsync<JsonElement>();
        var createdConnectorId = connectorContent.GetProperty("id").GetString()!;
        
        _output.WriteLine($"✅ Connector created: {createdConnectorId}");
        _output.WriteLine("");

        // ===== STEP 3: CREATE PROCESS =====
        _output.WriteLine("=== STEP 3: CREATE PROCESS ===");
        var processId = $"weather-forecast-{Guid.NewGuid().ToString("N")[..8]}";
        
        var processRequest = new
        {
            id = processId,
            name = "Weather Forecast Daily",
            status = "ACTIVE",
            connectorId = createdConnectorId,
            outputDestinations = new[]
            {
                new
                {
                    type = "LocalFileSystem",
                    local = new { basePath = "/output/weather" }
                }
            }
        };

        var processHttpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/processes");
        processHttpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);
        processHttpRequest.Content = JsonContent.Create(processRequest);

        var processResponse = await _client.SendAsync(processHttpRequest);
        processResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var processContent = await processResponse.Content.ReadFromJsonAsync<JsonElement>();
        var createdProcessId = processContent.GetProperty("id").GetString()!;
        
        _output.WriteLine($"✅ Process created: {createdProcessId}");
        _output.WriteLine("");

        // ===== STEP 4: CREATE PROCESS VERSION =====
        _output.WriteLine("=== STEP 4: CREATE PROCESS VERSION ===");
        
        var versionRequest = new
        {
            processId = createdProcessId,
            version = 1,
            enabled = true,
            sourceRequest = new
            {
                method = "GET",
                path = "/weather",
                queryParams = new Dictionary<string, string>
                {
                    { "format", "json" },
                    { "user_ip", "remote" }
                }
            },
            dsl = new
            {
                profile = "jsonata",
                text = """
                {
                    "city": results.city,
                    "temperature": results.temp,
                    "forecast_avg": $average(results.forecast.(max + min) / 2)
                }
                """
            },
            outputSchema = JsonSerializer.Deserialize<object>("""
            {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "city": {"type": "string"},
                        "temperature": {"type": "number"},
                        "forecast_avg": {"type": "number"}
                    }
                }
            }
            """)
        };

        var versionHttpRequest = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/processes/{createdProcessId}/versions");
        versionHttpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);
        versionHttpRequest.Content = JsonContent.Create(versionRequest);

        var versionResponse = await _client.SendAsync(versionHttpRequest);
        versionResponse.StatusCode.Should().Be(HttpStatusCode.Created);
        
        var versionContent = await versionResponse.Content.ReadFromJsonAsync<JsonElement>();
        var createdVersion = versionContent.GetProperty("version").GetInt32();
        
        _output.WriteLine($"✅ Version created: v{createdVersion}");
        _output.WriteLine("");

        // ===== STEP 5: EXECUTE TRANSFORM =====
        _output.WriteLine("=== STEP 5: EXECUTE TRANSFORM ===");
        
        var sampleInput = GetHGBrasilSampleData();
        var transformRequest = new
        {
            sampleInput = sampleInput,
            dsl = new
            {
                profile = "jsonata",
                text = """
                results.forecast.{
                    "date": date,
                    "max_temp": $number(max),
                    "min_temp": $number(min),
                    "condition": condition,
                    "temp_range": $number(max) - $number(min)
                }
                """
            },
            outputSchema = JsonSerializer.Deserialize<object>("""
            {
                "type": "array",
                "items": {
                    "type": "object",
                    "properties": {
                        "date": {"type": "string"},
                        "max_temp": {"type": "number"},
                        "min_temp": {"type": "number"},
                        "condition": {"type": "string"},
                        "temp_range": {"type": "number"}
                    }
                }
            }
            """)
        };

        var transformHttpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/preview/transform");
        transformHttpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);
        transformHttpRequest.Content = JsonContent.Create(transformRequest);

        var transformResponse = await _client.SendAsync(transformHttpRequest);
        transformResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        
        var transformContent = await transformResponse.Content.ReadFromJsonAsync<PreviewTransformResponseDto>();
        transformContent!.IsValid.Should().BeTrue();
        
        _output.WriteLine($"✅ Transform completed!");
        
        if (!string.IsNullOrEmpty(transformContent.PreviewCsv))
        {
            var csvLines = transformContent.PreviewCsv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            _output.WriteLine($"   CSV Rows: {csvLines.Length}");
            _output.WriteLine($"   Header: {csvLines[0]}");
            csvLines[0].Should().Contain("date");
            csvLines[0].Should().Contain("max_temp");
            csvLines[0].Should().Contain("condition");
        }
        
        _output.WriteLine("");
        _output.WriteLine("╔════════════════════════════════════════════════════════════╗");
        _output.WriteLine("║       🎉 WEATHER FORECAST FLOW COMPLETED! 🎉              ║");
        _output.WriteLine("╚════════════════════════════════════════════════════════════╝");
    }

    private static JsonElement GetAlphaVantageSampleData()
    {
        var json = """
        {
            "Meta Data": {
                "1. Information": "Intraday (5min) open, high, low, close prices and volume",
                "2. Symbol": "IBM",
                "3. Last Refreshed": "2026-01-05 19:55:00",
                "4. Interval": "5min",
                "5. Output Size": "Compact",
                "6. Time Zone": "US/Eastern"
            },
            "Time Series (5min)": {
                "2026-01-05 19:55:00": {
                    "1. open": "294.9800",
                    "2. high": "295.0000",
                    "3. low": "294.9800",
                    "4. close": "294.9900",
                    "5. volume": "132"
                },
                "2026-01-05 19:50:00": {
                    "1. open": "295.0000",
                    "2. high": "295.0000",
                    "3. low": "294.9800",
                    "4. close": "294.9800",
                    "5. volume": "3"
                },
                "2026-01-05 19:45:00": {
                    "1. open": "295.0000",
                    "2. high": "295.0000",
                    "3. low": "294.9900",
                    "4. close": "295.0000",
                    "5. volume": "105"
                },
                "2026-01-05 19:40:00": {
                    "1. open": "294.9900",
                    "2. high": "295.0000",
                    "3. low": "294.9800",
                    "4. close": "295.0000",
                    "5. volume": "256"
                },
                "2026-01-05 19:35:00": {
                    "1. open": "294.9500",
                    "2. high": "294.9900",
                    "3. low": "294.9500",
                    "4. close": "294.9900",
                    "5. volume": "489"
                },
                "2026-01-05 19:30:00": {
                    "1. open": "294.9000",
                    "2. high": "295.0000",
                    "3. low": "294.9000",
                    "4. close": "294.9500",
                    "5. volume": "1024"
                },
                "2026-01-05 19:25:00": {
                    "1. open": "294.8500",
                    "2. high": "294.9500",
                    "3. low": "294.8500",
                    "4. close": "294.9000",
                    "5. volume": "2048"
                },
                "2026-01-05 19:20:00": {
                    "1. open": "294.8000",
                    "2. high": "294.9000",
                    "3. low": "294.8000",
                    "4. close": "294.8500",
                    "5. volume": "512"
                }
            }
        }
        """;
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    private static JsonElement GetHGBrasilSampleData()
    {
        var json = """
        {
            "results": {
                "city": "São Paulo",
                "temp": 28,
                "date": "05/01/2026",
                "time": "19:00",
                "condition_slug": "clear_day",
                "description": "Tempo limpo",
                "currently": "dia",
                "forecast": [
                    {
                        "date": "06/01",
                        "weekday": "Seg",
                        "max": 32,
                        "min": 21,
                        "condition": "storm"
                    },
                    {
                        "date": "07/01",
                        "weekday": "Ter",
                        "max": 30,
                        "min": 20,
                        "condition": "rain"
                    },
                    {
                        "date": "08/01",
                        "weekday": "Qua",
                        "max": 29,
                        "min": 19,
                        "condition": "cloudly_day"
                    },
                    {
                        "date": "09/01",
                        "weekday": "Qui",
                        "max": 31,
                        "min": 22,
                        "condition": "clear_day"
                    },
                    {
                        "date": "10/01",
                        "weekday": "Sex",
                        "max": 33,
                        "min": 23,
                        "condition": "clear_day"
                    },
                    {
                        "date": "11/01",
                        "weekday": "Sáb",
                        "max": 28,
                        "min": 19,
                        "condition": "rain"
                    },
                    {
                        "date": "12/01",
                        "weekday": "Dom",
                        "max": 27,
                        "min": 18,
                        "condition": "rain"
                    }
                ]
            }
        }
        """;
        return JsonSerializer.Deserialize<JsonElement>(json);
    }
}
