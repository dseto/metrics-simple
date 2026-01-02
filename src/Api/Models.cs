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
    string Version,
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
    Dictionary<string, string>? QueryParams = null
);

public record DslDto(
    string Profile,
    string Text
);

// Connector Models
public record ConnectorDto(
    string Id,
    string Name,
    string BaseUrl,
    string AuthRef,
    int TimeoutSeconds
);

// Preview Transform Models
public record PreviewTransformRequestDto(
    DslDto Dsl,
    object OutputSchema,
    object SampleInput
);

public record PreviewTransformResponseDto(
    bool IsValid,
    List<string> Errors,
    object? PreviewOutput = null,
    string? PreviewCsv = null
);
