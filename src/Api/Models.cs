using System.Text.Json.Serialization;

namespace Metrics.Api;

// Process Models
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

// ProcessVersion Models
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

// Connector Models - per connector.schema.json spec (no authRef)
public record ConnectorDto(
    string Id,
    string Name,
    string BaseUrl,
    int TimeoutSeconds,
    bool Enabled = true,
    string AuthType = "NONE",  // NONE | BEARER | API_KEY | BASIC
    // API_KEY config (non-secret)
    string? ApiKeyLocation = null,  // HEADER | QUERY
    string? ApiKeyName = null,
    // BASIC config (non-secret)
    string? BasicUsername = null,
    // Request defaults
    RequestDefaultsDto? RequestDefaults = null,
    // Has* flags for secrets (read-only)
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
    string AuthType = "NONE",  // NONE | BEARER | API_KEY | BASIC
    // API_KEY config
    string? ApiKeyLocation = null,
    string? ApiKeyName = null,
    string? ApiKeyValue = null,  // Write-only secret
    // BASIC config
    string? BasicUsername = null,
    string? BasicPassword = null,  // Write-only secret
    // BEARER token
    string? ApiToken = null,  // Write-only: optional API token (1..4096 chars if provided)
    // Request defaults
    RequestDefaultsDto? RequestDefaults = null
);

public record ConnectorUpdateDto(
    string Name,
    string BaseUrl,
    int TimeoutSeconds,
    bool Enabled = true,
    string AuthType = "NONE",
    // API_KEY config
    string? ApiKeyLocation = null,
    string? ApiKeyName = null,
    string? ApiKeyValue = null,
    // BASIC config
    string? BasicUsername = null,
    string? BasicPassword = null,
    // BEARER token
    string? ApiToken = null,
    // Request defaults
    RequestDefaultsDto? RequestDefaults = null
)
{
    // Flags to distinguish between "omitted" (keep) vs "null" (remove)
    public bool ApiTokenSpecified { get; init; }
    public bool ApiKeySpecified { get; init; }
    public bool BasicPasswordSpecified { get; init; }
};

public record RequestDefaultsDto(
    string? Method = null,  // GET | POST
    Dictionary<string, string>? Headers = null,
    Dictionary<string, string>? QueryParams = null,
    object? Body = null,
    string? ContentType = null
);

// Preview Transform Models
public record PreviewTransformRequestDto(
    object SampleInput,
    DslDto Dsl,
    object? OutputSchema = null
);

public record PreviewTransformResponseDto(
    bool IsValid,
    List<string> Errors,
    object? PreviewOutput = null,
    string? PreviewCsv = null
);
