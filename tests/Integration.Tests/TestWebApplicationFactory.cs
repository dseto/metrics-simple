using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using System.Text.Json;

namespace Integration.Tests;

/// <summary>
/// Custom WebApplicationFactory for integration tests.
/// Configures API to use a specific SQLite database via METRICS_SQLITE_PATH env var.
/// </summary>
public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath;

    public TestWebApplicationFactory(string dbPath)
    {
        _dbPath = dbPath;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Set the METRICS_SQLITE_PATH env var before the host builds
        Environment.SetEnvironmentVariable("METRICS_SQLITE_PATH", _dbPath);

        // Load API key from appsettings.Development.json and set as env var
        // so Program.cs can use it when initializing the AI provider
        try
        {
            var basePath = Path.Combine(Directory.GetCurrentDirectory(), "src", "Api");
            var devConfigPath = Path.Combine(basePath, "appsettings.Development.json");
            if (File.Exists(devConfigPath))
            {
                var jsonText = File.ReadAllText(devConfigPath);
                var doc = System.Text.Json.JsonDocument.Parse(jsonText);
                if (doc.RootElement.TryGetProperty("AI", out var aiSection) &&
                    aiSection.TryGetProperty("ApiKey", out var apiKeyElement) &&
                    apiKeyElement.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    var apiKey = apiKeyElement.GetString();
                    if (!string.IsNullOrWhiteSpace(apiKey))
                    {
                        Environment.SetEnvironmentVariable("METRICS_OPENROUTER_API_KEY", apiKey);
                    }
                }
            }
        }
        catch
        {
            // Silently fail - tests will skip if no API key
        }

        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Override database path in configuration
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Path"] = _dbPath
            });
        });

        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        
        // Clean up env var
        Environment.SetEnvironmentVariable("METRICS_SQLITE_PATH", null);
    }
}
