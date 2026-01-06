using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Metrics.Api;
using Xunit;

namespace Integration.Tests;

/// <summary>
/// IT10 — Real API Integration Tests
/// 
/// Tests `/api/v1/preview/transform` endpoint with real HGBrasil Weather API data.
/// Validates DSL transformation with various patterns:
/// - Simple extraction (city, temperature)
/// - Aggregation functions ($average, $count)
/// - Complex arithmetic on collections
/// - Filtering arrays
/// - Mapping transformations
/// </summary>
[Collection("Sequential")]
public class IT10_PreviewTransformRealApiTests : IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly string _dbPath;
    private HttpClient _client = null!;
    private string _adminToken = string.Empty;

    public IT10_PreviewTransformRealApiTests()
    {
        _dbPath = TestFixtures.CreateTempDbPath();
        _factory = new TestWebApplicationFactory(_dbPath, disableAuth: false);
    }

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        _adminToken = await GetAdminTokenAsync();
        await Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        _factory.Dispose();
        TestFixtures.CleanupTempFile(_dbPath);
        await Task.CompletedTask;
    }

    private async Task<string> GetAdminTokenAsync()
    {
        var request = new { username = "admin", password = "testpass123" };
        var response = await _client.PostAsJsonAsync("/api/auth/token", request);
        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = content.GetProperty("access_token").GetString()!;
        return token;
    }

    /// <summary>
    /// Real HGBrasil Weather API response data for Curitiba, PR
    /// </summary>
    private static JsonElement GetHgBrasilSampleData()
    {
        var json = """
        {
            "by": "city_name",
            "valid_key": true,
            "results": {
                "temp": 16,
                "date": "05/01/2026",
                "time": "19:01",
                "condition_code": "28",
                "description": "Tempo nublado",
                "currently": "dia",
                "city": "Curitiba, PR",
                "humidity": 83,
                "cloudiness": 75.0,
                "rain": 0.0,
                "wind_speedy": "5.66 km/h",
                "city_name": "Curitiba",
                "timezone": "-03:00",
                "forecast": [
                    {
                        "date": "05/01",
                        "weekday": "Seg",
                        "max": 25,
                        "min": 13,
                        "humidity": 53,
                        "rain": 0.1,
                        "rain_probability": 20,
                        "description": "Chuvas esparsas",
                        "condition": "rain"
                    },
                    {
                        "date": "06/01",
                        "weekday": "Ter",
                        "max": 23,
                        "min": 13,
                        "humidity": 64,
                        "rain": 0.24,
                        "rain_probability": 48,
                        "description": "Chuvas esparsas",
                        "condition": "rain"
                    },
                    {
                        "date": "07/01",
                        "weekday": "Qua",
                        "max": 27,
                        "min": 15,
                        "humidity": 45,
                        "rain": 0.0,
                        "rain_probability": 5,
                        "description": "Céu limpo",
                        "condition": "clear"
                    }
                ]
            },
            "execution_time": 0.0,
            "from_cache": true
        }
        """;
        return JsonSerializer.Deserialize<JsonElement>(json);
    }

    [Fact]
    public async Task PreviewTransform_SimpleExtraction_Returns200()
    {
        // Arrange
        var sampleData = GetHgBrasilSampleData();
        // Schema should match normalized output (which is always an array of objects)
        var outputSchema = JsonSerializer.Deserialize<object>("""
        {
            "type":"array",
            "items":{
                "type":"object",
                "properties":{
                    "city":{"type":"string"},
                    "current_temp":{"type":"number"}
                }
            }
        }
        """);

        var request = new
        {
            sampleInput = sampleData,
            dsl = new
            {
                profile = "jsonata",
                text = """{"city": results.city_name, "current_temp": results.temp}"""
            },
            outputSchema
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/preview/transform");
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);
        httpRequest.Content = JsonContent.Create(request);

        // Act
        var response = await _client.SendAsync(httpRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PreviewTransformResponseDto>();
        result.Should().NotBeNull();
        if (!result!.IsValid)
        {
            var errors = string.Join("; ", result.Errors);
            throw new Exception($"SimpleExtraction DSL validation failed: {errors}");
        }
        result.IsValid.Should().BeTrue();
        result.PreviewOutput.Should().NotBeNull();
    }

    [Fact]
    public async Task PreviewTransform_WithAggregation_Returns200()
    {
        // Arrange - calculate average forecast temperatures
        var sampleData = GetHgBrasilSampleData();
        var outputSchema = JsonSerializer.Deserialize<object>("""
        {
            "type":"array",
            "items":{
                "type":"object",
                "properties":{
                    "city":{"type":"string"},
                    "avg_high":{"type":"number"},
                    "count":{"type":"number"}
                }
            }
        }
        """);

        var request = new
        {
            sampleInput = sampleData,
            dsl = new
            {
                profile = "jsonata",
                text = """{"city": results.city_name, "avg_high": $average(results.forecast.max), "count": $count(results.forecast)}"""
            },
            outputSchema
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/preview/transform");
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);
        httpRequest.Content = JsonContent.Create(request);

        // Act
        var response = await _client.SendAsync(httpRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PreviewTransformResponseDto>();
        result.Should().NotBeNull();
        if (!result!.IsValid)
        {
            var errors = string.Join("; ", result.Errors);
            throw new Exception($"Aggregation DSL validation failed: {errors}");
        }
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task PreviewTransform_WithComplexArithmetic_Returns200()
    {
        // Arrange - calculate mean temperature (max+min)/2 for each forecast day
        var sampleData = GetHgBrasilSampleData();
        var outputSchema = JsonSerializer.Deserialize<object>("""
        {
            "type":"array",
            "items":{
                "type":"object",
                "properties":{
                    "city":{"type":"string"},
                    "avg_mean_temp":{"type":"number"}
                }
            }
        }
        """);

        var request = new
        {
            sampleInput = sampleData,
            dsl = new
            {
                profile = "jsonata",
                text = """{"city": results.city_name, "avg_mean_temp": $average(results.forecast.((max + min) / 2))}"""
            },
            outputSchema
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/preview/transform");
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);
        httpRequest.Content = JsonContent.Create(request);

        // Act
        var response = await _client.SendAsync(httpRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PreviewTransformResponseDto>();
        result.Should().NotBeNull();
        if (!result!.IsValid)
        {
            var errors = string.Join("; ", result.Errors);
            throw new Exception($"ComplexArithmetic DSL validation failed: {errors}");
        }
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task PreviewTransform_WithFilter_Returns200()
    {
        // Arrange - filter to get only rainy forecast days
        var sampleData = GetHgBrasilSampleData();
        var outputSchema = JsonSerializer.Deserialize<object>("""
        {
            "type":"array",
            "items":{
                "type":"object",
                "properties":{
                    "city":{"type":"string"},
                    "rainy_days":{"type":"array"}
                }
            }
        }
        """);

        var request = new
        {
            sampleInput = sampleData,
            dsl = new
            {
                profile = "jsonata",
                text = """{"city": results.city_name, "rainy_days": results.forecast[condition="rain"].{"date": date, "rain_mm": rain}}"""
            },
            outputSchema
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/preview/transform");
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);
        httpRequest.Content = JsonContent.Create(request);

        // Act
        var response = await _client.SendAsync(httpRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PreviewTransformResponseDto>();
        result.Should().NotBeNull();
        if (!result!.IsValid)
        {
            var errors = string.Join("; ", result.Errors);
            throw new Exception($"Filter DSL validation failed: {errors}");
        }
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task PreviewTransform_WithArrayMapping_Returns200()
    {
        // Arrange - map forecast array to detailed weather objects
        var sampleData = GetHgBrasilSampleData();
        var outputSchema = JsonSerializer.Deserialize<object>("""
        {
            "type":"array",
            "items":{
                "type":"object",
                "properties":{
                    "city":{"type":"string"},
                    "forecast":{"type":"array"}
                }
            }
        }
        """);

        var request = new
        {
            sampleInput = sampleData,
            dsl = new
            {
                profile = "jsonata",
                text = """{"city": results.city_name, "forecast": results.forecast.{"date": date, "mean_temp": (max + min) / 2}}"""
            },
            outputSchema
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/preview/transform");
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);
        httpRequest.Content = JsonContent.Create(request);

        // Act
        var response = await _client.SendAsync(httpRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PreviewTransformResponseDto>();
        result.Should().NotBeNull();
        if (!result!.IsValid)
        {
            var errors = string.Join("; ", result.Errors);
            throw new Exception($"ArrayMapping DSL validation failed: {errors}");
        }
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task PreviewTransform_InvalidDsl_ReturnsValidationError()
    {
        // Arrange - invalid Jsonata syntax
        var sampleData = GetHgBrasilSampleData();
        var outputSchema = JsonSerializer.Deserialize<object>("""
        {
            "type":"array",
            "items":{"type":"object"}
        }
        """);

        var request = new
        {
            sampleInput = sampleData,
            dsl = new
            {
                profile = "jsonata",
                text = "$()$()$("  // Invalid syntax - unclosed function calls
            },
            outputSchema
        };

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/preview/transform");
        httpRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _adminToken);
        httpRequest.Content = JsonContent.Create(request);

        // Act
        var response = await _client.SendAsync(httpRequest);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<PreviewTransformResponseDto>();
        result.Should().NotBeNull();
        // Invalid DSL should result in IsValid=false with error messages
        result!.IsValid.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
    }
}
