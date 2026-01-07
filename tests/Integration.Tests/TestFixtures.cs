using System.Text.Json;
using System.Text.Json.Serialization;

namespace Integration.Tests;

/// <summary>
/// Test fixtures and helpers for integration tests.
/// </summary>
public static class TestFixtures
{
    /// <summary>
    /// Reads the hosts-cpu-input.json fixture file.
    /// </summary>
    public static string GetHostsCpuInputJson()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "hosts-cpu-input.json");
        return File.ReadAllText(path);
    }

    /// <summary>
    /// Reads the hosts-cpu-output.schema.json fixture file.
    /// </summary>
    public static string GetHostsCpuOutputSchemaJson()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "hosts-cpu-output.schema.json");
        return File.ReadAllText(path);
    }

    /// <summary>
    /// Reads the hosts-cpu-expected.csv fixture file.
    /// </summary>
    public static string GetHostsCpuExpectedCsv()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "fixtures", "hosts-cpu-expected.csv");
        return File.ReadAllText(path);
    }

    /// <summary>
    /// Normalizes CSV newlines to LF (\n) as per csv-format.md spec.
    /// </summary>
    public static string NormalizeCsvNewlines(string csv)
    {
        // Replace CRLF with LF, then ensure trailing newline
        var normalized = csv.Replace("\r\n", "\n").Replace("\r", "\n");
        if (!normalized.EndsWith("\n"))
        {
            normalized += "\n";
        }
        return normalized;
    }

    /// <summary>
    /// Creates a temporary directory for test outputs and returns its path.
    /// </summary>
    public static string CreateTempOutputDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"metrics-simple-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// Creates a temporary SQLite database file path.
    /// </summary>
    public static string CreateTempDbPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"metrics-simple-test-{Guid.NewGuid():N}.db");
        return path;
    }

    /// <summary>
    /// Cleans up a temporary directory.
    /// </summary>
    public static void CleanupTempDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    /// <summary>
    /// Cleans up a temporary file.
    /// </summary>
    public static void CleanupTempFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }
}

/// <summary>
/// API DTOs for integration tests - matching the API Models.
/// </summary>
public record ConnectorDto(
    string Id,
    string Name,
    string BaseUrl,
    int TimeoutSeconds,
    bool Enabled = true,
    string AuthType = "NONE",
    string? ApiKeyLocation = null,
    string? ApiKeyName = null,
    string? BasicUsername = null,
    RequestDefaultsDto? RequestDefaults = null,
    bool? HasApiToken = null,
    bool? HasApiKey = null,
    bool? HasBasicPassword = null
);

public record ConnectorCreateDto(
    string Id,
    string Name,
    string BaseUrl,
    int TimeoutSeconds,
    bool Enabled = true,
    string AuthType = "NONE",
    string? ApiKeyLocation = null,
    string? ApiKeyName = null,
    string? ApiKeyValue = null,
    string? BasicUsername = null,
    string? BasicPassword = null,
    string? ApiToken = null,
    RequestDefaultsDto? RequestDefaults = null
);

public record ConnectorUpdateDto(
    string Name,
    string BaseUrl,
    int TimeoutSeconds,
    bool Enabled = true,
    string AuthType = "NONE",
    string? ApiKeyLocation = null,
    string? ApiKeyName = null,
    string? ApiKeyValue = null,
    string? BasicUsername = null,
    string? BasicPassword = null,
    string? ApiToken = null,
    RequestDefaultsDto? RequestDefaults = null,
    bool ApiTokenSpecified = false,
    bool ApiKeySpecified = false,
    bool BasicPasswordSpecified = false
);

public record RequestDefaultsDto(
    string? Method = null,
    Dictionary<string, string>? Headers = null,
    Dictionary<string, string>? QueryParams = null,
    object? Body = null,
    string? ContentType = null
);

public record ProcessDto(
    string Id,
    string Name,
    string Status,
    string ConnectorId,
    List<OutputDestinationDto> OutputDestinations
);

public record OutputDestinationDto(
    string Type,
    LocalFileSystemDto? Local = null,
    AzureBlobStorageDto? Blob = null
);

public record LocalFileSystemDto(string BasePath);

public record AzureBlobStorageDto(
    string ConnectionStringRef,
    string Container,
    string PathPrefix
);

public record ProcessVersionDto(
    string ProcessId,
    int Version,
    bool Enabled,
    SourceRequestDto SourceRequest,
    DslDto Dsl,
    object OutputSchema,
    object? SampleInput = null
);

public record SourceRequestDto(
    string Method,
    string Path,
    Dictionary<string, string>? Headers = null,
    Dictionary<string, string>? QueryParams = null,
    object? Body = null,
    string? ContentType = null
);

public record DslDto(
    string Profile,
    string Text
);
