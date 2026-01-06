using System.Text.Json;
using System.Text.Json.Serialization;

namespace Metrics.Api.AI.Engines.PlanV1;

/// <summary>
/// IR v1 Plan - represents a deterministic transformation plan.
/// Matches plan-schema-v1.json
/// </summary>
public record TransformPlan
{
    [JsonPropertyName("planVersion")]
    public string PlanVersion { get; init; } = "1.0";

    [JsonPropertyName("source")]
    public required PlanSource Source { get; init; }

    [JsonPropertyName("policy")]
    public PlanPolicy? Policy { get; init; }

    [JsonPropertyName("steps")]
    public required List<PlanStep> Steps { get; init; }
}

public record PlanSource
{
    /// <summary>
    /// JSON Pointer to the array of records (e.g., "/products", "/" for root array)
    /// </summary>
    [JsonPropertyName("recordPath")]
    public required string RecordPath { get; init; }
}

public record PlanPolicy
{
    [JsonPropertyName("renamePolicy")]
    public string RenamePolicy { get; init; } = "ExplicitOnly";

    [JsonPropertyName("shapePolicy")]
    public string ShapePolicy { get; init; } = "ArrayOfObjects";

    [JsonPropertyName("schemaMode")]
    public string SchemaMode { get; init; } = "Permissive";

    [JsonPropertyName("locale")]
    public string Locale { get; init; } = "auto";
}

/// <summary>
/// A single step/operation in the plan
/// </summary>
public record PlanStep
{
    [JsonPropertyName("op")]
    public required string Op { get; init; }

    // For "select" operation
    [JsonPropertyName("fields")]
    public List<FieldSpec>? Fields { get; init; }

    // For "filter" operation
    [JsonPropertyName("where")]
    public Condition? Where { get; init; }

    // For "compute" operation
    [JsonPropertyName("compute")]
    public List<ComputeSpec>? Compute { get; init; }

    // For "mapValue" operation
    [JsonPropertyName("map")]
    public List<MapSpec>? Map { get; init; }

    // For "groupBy" operation
    [JsonPropertyName("keys")]
    public List<string>? Keys { get; init; }

    // For "aggregate" operation
    [JsonPropertyName("metrics")]
    public List<MetricSpec>? Metrics { get; init; }

    // For "sort" operation
    [JsonPropertyName("by")]
    public string? By { get; init; }

    [JsonPropertyName("dir")]
    public string Dir { get; init; } = "asc";

    // For "limit" operation
    [JsonPropertyName("n")]
    public int? N { get; init; }
}

public record FieldSpec
{
    [JsonPropertyName("from")]
    public required string From { get; init; }

    [JsonPropertyName("as")]
    public required string As { get; init; }

    [JsonPropertyName("typeHint")]
    public string TypeHint { get; init; } = "unknown";
}

public record ComputeSpec
{
    [JsonPropertyName("as")]
    public required string As { get; init; }

    [JsonPropertyName("expr")]
    public required string Expr { get; init; }

    [JsonPropertyName("typeHint")]
    public string TypeHint { get; init; } = "unknown";
}

public record MapSpec
{
    [JsonPropertyName("from")]
    public required string From { get; init; }

    [JsonPropertyName("as")]
    public required string As { get; init; }

    [JsonPropertyName("mapping")]
    public required Dictionary<string, JsonElement> Mapping { get; init; }

    [JsonPropertyName("default")]
    public JsonElement? Default { get; init; }
}

public record MetricSpec
{
    [JsonPropertyName("as")]
    public required string As { get; init; }

    [JsonPropertyName("fn")]
    public required string Fn { get; init; }

    [JsonPropertyName("expr")]
    public string? Expr { get; init; }

    [JsonPropertyName("field")]
    public string? Field { get; init; }
}

/// <summary>
/// Filter condition (supports nested and/or/not)
/// </summary>
public record Condition
{
    [JsonPropertyName("op")]
    public required string Op { get; init; }

    [JsonPropertyName("left")]
    public JsonElement? Left { get; init; }

    [JsonPropertyName("right")]
    public JsonElement? Right { get; init; }

    [JsonPropertyName("items")]
    public List<Condition>? Items { get; init; }
}

/// <summary>
/// Known operation types
/// </summary>
public static class PlanOps
{
    public const string Select = "select";
    public const string Filter = "filter";
    public const string Compute = "compute";
    public const string MapValue = "mapValue";
    public const string GroupBy = "groupBy";
    public const string Aggregate = "aggregate";
    public const string Sort = "sort";
    public const string Limit = "limit";
}

/// <summary>
/// Known aggregate functions
/// </summary>
public static class AggregateFunctions
{
    public const string Sum = "sum";
    public const string Count = "count";
    public const string Avg = "avg";
    public const string Min = "min";
    public const string Max = "max";
}

/// <summary>
/// Known condition operators
/// </summary>
public static class ConditionOps
{
    public const string Eq = "eq";
    public const string Neq = "neq";
    public const string Gt = "gt";
    public const string Gte = "gte";
    public const string Lt = "lt";
    public const string Lte = "lte";
    public const string In = "in";
    public const string Contains = "contains";
    public const string And = "and";
    public const string Or = "or";
    public const string Not = "not";
}
