using System.Text.Json;

namespace Metrics.Engine;

public sealed class JsonataTransformerSkeleton : IDslTransformer
{
    public JsonElement Transform(JsonElement input, string dslProfile, string dslText)
    {
        if (!string.Equals(dslProfile, "jsonata", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"DSL profile not supported: {dslProfile}");

        // TODO: integrate Jsonata for .NET; for now returns input unchanged.
        return input;
    }
}
