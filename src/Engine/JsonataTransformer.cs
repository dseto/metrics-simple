using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Jsonata.Net.Native;

namespace Metrics.Engine;

public sealed class JsonataTransformer : IDslTransformer
{
    private readonly ConcurrentDictionary<string, JsonataQuery> _compiledQueries = new();

    public JsonElement Transform(JsonElement input, string dslProfile, string dslText)
    {
        if (!string.Equals(dslProfile, "jsonata", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException($"DSL profile '{dslProfile}' is not supported. Only 'jsonata' is implemented.");

        if (string.IsNullOrWhiteSpace(dslText))
            throw new InvalidOperationException("DSL text cannot be empty.");

        // Garantir determinismo em formatação numérica onde aplicável
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUICulture = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;

        try
        {
            // Compila e cacheia (JsonataQuery é imutável e thread-safe) :contentReference[oaicite:5]{index=5}
            JsonataQuery query;
            try
            {
                query = _compiledQueries.GetOrAdd(dslText, expr => new JsonataQuery(expr));
            }
            catch (Exception ex)
            {
                // Parse/compile error => DSL_INVALID
                throw CreateDslInvalid(ex, dslText);
            }

            var inputJson = input.GetRawText();

            string resultJson;
            try
            {
                // Eval(string) retorna string com JSON do resultado :contentReference[oaicite:6]{index=6}
                resultJson = query.Eval(inputJson);
            }
            catch (Exception ex)
            {
                // Runtime error => TRANSFORM_FAILED
                throw CreateTransformFailed(ex, dslText);
            }

            // Trata "undefined/nothing" como null (evita parse falho) :contentReference[oaicite:7]{index=7}
            if (string.IsNullOrWhiteSpace(resultJson))
                return JsonSerializer.SerializeToElement<object?>(null);

            // Evita lifetime issues do JsonDocument
            using var doc = JsonDocument.Parse(resultJson);
            return doc.RootElement.Clone();
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUICulture;
        }
    }

    private static Exception CreateDslInvalid(Exception ex, string dslText)
        => new InvalidOperationException(
            $"DSL_INVALID: Failed to parse/compile Jsonata expression. dslHash={Hash(dslText)} dslPreview={Preview(dslText)}",
            ex);

    private static Exception CreateTransformFailed(Exception ex, string dslText)
        => new InvalidOperationException(
            $"TRANSFORM_FAILED: Jsonata evaluation failed. dslHash={Hash(dslText)} dslPreview={Preview(dslText)}",
            ex);

    private static string Preview(string text, int max = 200)
        => text.Length <= max ? text : text[..max] + "...";

    private static string Hash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
