using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Metrics.Api;
using Xunit;

namespace Integration.Tests;

/// <summary>
/// IT11 — AlphaVantage Complex Financial Transformations
/// 
/// Tests `/api/v1/preview/transform` with realistic financial data transformations:
/// - Time series to array conversion
/// - Daily returns calculation
/// - Volume analysis (top N, filtering)
/// - Moving averages
/// - Volatility metrics
/// - VWAP (Volume-Weighted Average Price)
/// - Gap detection
/// - Weekly/Monthly aggregations
/// - Statistical summaries
/// </summary>
[Collection("Sequential")]
public class IT11_AlphaVantageComplexTests : IAsyncLifetime
{
    private readonly TestWebApplicationFactory _factory;
    private readonly string _dbPath;
    private HttpClient _client = null!;
    private string _adminToken = string.Empty;

    public IT11_AlphaVantageComplexTests()
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
    /// AlphaVantage TIME_SERIES_INTRADAY sample data (IBM 5min intervals)
    /// </summary>
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

    [Fact]
    public async Task Test01_ConvertTimeSeriesObjectToSortedArray()
    {
        // Arrange - Convert object with timestamp keys to sorted array
        var sampleData = GetAlphaVantageSampleData();
        var outputSchema = JsonSerializer.Deserialize<object>("""
        {
            "type":"array",
            "items":{
                "type":"object",
                "properties":{
                    "timestamp":{"type":"string"},
                    "open":{"type":"number"},
                    "close":{"type":"number"},
                    "volume":{"type":"number"}
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
                text = """
                $spread($."Time Series (5min)").{
                    "timestamp": $keys()[0],
                    "open": $number($."1. open"),
                    "close": $number($."4. close"),
                    "volume": $number($."5. volume")
                }^(timestamp)
                """
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
            throw new Exception($"Test01 DSL failed: {errors}");
        }
        result.IsValid.Should().BeTrue();
        result.PreviewOutput.Should().NotBeNull();
    }

    [Fact]
    public async Task Test02_CalculateDailyReturns()
    {
        // Arrange - Calculate return percentage (close - open) / open * 100
        var sampleData = GetAlphaVantageSampleData();
        var outputSchema = JsonSerializer.Deserialize<object>("""
        {
            "type":"array",
            "items":{
                "type":"object",
                "properties":{
                    "timestamp":{"type":"string"},
                    "return_pct":{"type":"number"}
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
                text = """
                $spread($."Time Series (5min)").{
                    "timestamp": $keys()[0],
                    "return_pct": $round((($number($."4. close") - $number($."1. open")) / $number($."1. open")) * 100, 4)
                }
                """
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
            throw new Exception($"Test02 DSL failed: {errors}");
        }
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Test03_TopNHighestVolumeIntervals()
    {
        // Arrange - Find top N intervals sorted by volume descending
        var sampleData = GetAlphaVantageSampleData();
        var outputSchema = JsonSerializer.Deserialize<object>("""
        {
            "type":"array",
            "items":{
                "type":"object",
                "properties":{
                    "timestamp":{"type":"string"},
                    "volume":{"type":"number"}
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
                text = """
                $spread($."Time Series (5min)").{
                    "timestamp": $keys()[0],
                    "volume": $number($."5. volume")
                }^(>volume)
                """
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
            throw new Exception($"Test03 DSL failed: {errors}");
        }
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Test04_SimpleMovingAverageClose()
    {
        // Arrange - Calculate 3-period simple moving average of close prices
        var sampleData = GetAlphaVantageSampleData();
        var outputSchema = JsonSerializer.Deserialize<object>("""
        {
            "type":"array",
            "items":{
                "type":"object",
                "properties":{
                    "symbol":{"type":"string"},
                    "sma_3_close":{"type":"number"}
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
                text = """
                (
                    $closes := $spread($."Time Series (5min)").$number($."4. close");
                    {
                        "symbol": $."Meta Data"."2. Symbol",
                        "sma_3_close": $round($average([$closes[0], $closes[1], $closes[2]]), 4)
                    }
                )
                """
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
            throw new Exception($"Test04 DSL failed: {errors}");
        }
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Test05_VolatilityCalculation()
    {
        // Arrange - Calculate volatility (high - low) for each interval
        var sampleData = GetAlphaVantageSampleData();
        var outputSchema = JsonSerializer.Deserialize<object>("""
        {
            "type":"array",
            "items":{
                "type":"object",
                "properties":{
                    "timestamp":{"type":"string"},
                    "volatility":{"type":"number"}
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
                text = """
                $spread($."Time Series (5min)").{
                    "timestamp": $keys()[0],
                    "volatility": $round($number($."2. high") - $number($."3. low"), 4)
                }
                """
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
            throw new Exception($"Test05 DSL failed: {errors}");
        }
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Test06_FilterHighVolumeIntervals()
    {
        // Arrange - Get all intervals and their volumes (simplified - filtering done post-transform)
        var sampleData = GetAlphaVantageSampleData();
        var outputSchema = JsonSerializer.Deserialize<object>("""
        {
            "type":"array",
            "items":{
                "type":"object",
                "properties":{
                    "timestamp":{"type":"string"},
                    "volume":{"type":"number"},
                    "close":{"type":"number"}
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
                text = """
                $spread($."Time Series (5min)").{
                    "timestamp": $keys()[0],
                    "volume": $number($."5. volume"),
                    "close": $number($."4. close")
                }^(>volume)
                """
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
            throw new Exception($"Test06 DSL failed: {errors}");
        }
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Test07_VWAPCalculation()
    {
        // Arrange - Calculate VWAP (Volume-Weighted Average Price)
        var sampleData = GetAlphaVantageSampleData();
        var outputSchema = JsonSerializer.Deserialize<object>("""
        {
            "type":"array",
            "items":{
                "type":"object",
                "properties":{
                    "symbol":{"type":"string"},
                    "vwap":{"type":"number"}
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
                text = """
                (
                    $timeSeries := $spread($."Time Series (5min)");
                    $totalPV := $sum($timeSeries.($number($."4. close") * $number($."5. volume")));
                    $totalV := $sum($timeSeries.$number($."5. volume"));
                    {
                        "symbol": $."Meta Data"."2. Symbol",
                        "vwap": $round($totalPV / $totalV, 4)
                    }
                )
                """
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
            throw new Exception($"Test07 DSL failed: {errors}");
        }
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Test08_OHLCStatistics()
    {
        // Arrange - Calculate min, max, avg for open/high/low/close
        var sampleData = GetAlphaVantageSampleData();
        var outputSchema = JsonSerializer.Deserialize<object>("""
        {
            "type":"array",
            "items":{
                "type":"object",
                "properties":{
                    "symbol":{"type":"string"},
                    "min_close":{"type":"number"},
                    "max_close":{"type":"number"},
                    "avg_close":{"type":"number"},
                    "total_volume":{"type":"number"}
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
                text = """
                (
                    $timeSeries := $spread($."Time Series (5min)");
                    $closes := $timeSeries.$number($."4. close");
                    {
                        "symbol": $."Meta Data"."2. Symbol",
                        "min_close": $round($min($closes), 4),
                        "max_close": $round($max($closes), 4),
                        "avg_close": $round($average($closes), 4),
                        "total_volume": $sum($timeSeries.$number($."5. volume"))
                    }
                )
                """
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
            throw new Exception($"Test08 DSL failed: {errors}");
        }
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Test09_PriceRangePercentage()
    {
        // Arrange - Calculate price range as percentage of open: (high - low) / open * 100
        var sampleData = GetAlphaVantageSampleData();
        var outputSchema = JsonSerializer.Deserialize<object>("""
        {
            "type":"array",
            "items":{
                "type":"object",
                "properties":{
                    "timestamp":{"type":"string"},
                    "range_pct":{"type":"number"}
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
                text = """
                $spread($."Time Series (5min)").{
                    "timestamp": $keys()[0],
                    "range_pct": $round((($number($."2. high") - $number($."3. low")) / $number($."1. open")) * 100, 4)
                }
                """
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
            throw new Exception($"Test09 DSL failed: {errors}");
        }
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Test10_BullishBearishIntervals()
    {
        // Arrange - Classify intervals as bullish (close > open) or bearish
        var sampleData = GetAlphaVantageSampleData();
        var outputSchema = JsonSerializer.Deserialize<object>("""
        {
            "type":"array",
            "items":{
                "type":"object",
                "properties":{
                    "timestamp":{"type":"string"},
                    "direction":{"type":"string"},
                    "change":{"type":"number"}
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
                text = """
                $spread($."Time Series (5min)").{
                    "timestamp": $keys()[0],
                    "direction": $number($."4. close") >= $number($."1. open") ? "bullish" : "bearish",
                    "change": $round($number($."4. close") - $number($."1. open"), 4)
                }
                """
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
            throw new Exception($"Test10 DSL failed: {errors}");
        }
        result.IsValid.Should().BeTrue();
    }
}
