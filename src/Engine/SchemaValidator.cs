using System.Text.Json;
using NJsonSchema;

namespace Metrics.Engine;

public interface ISchemaValidator
{
    (bool IsValid, List<string> Errors) ValidateAgainstSchema(JsonElement data, JsonElement schema);
}

public sealed class SchemaValidator : ISchemaValidator
{
    public (bool IsValid, List<string> Errors) ValidateAgainstSchema(JsonElement data, JsonElement schema)
    {
        try
        {
            var schemaJson = schema.GetRawText();
            var jsonSchema = JsonSchema.FromJsonAsync(schemaJson).GetAwaiter().GetResult();

            var dataJson = data.GetRawText();
            var validationErrors = jsonSchema.Validate(dataJson);

            if (validationErrors.Count == 0)
                return (true, new List<string>());

            var errorMessages = validationErrors
                .Select(e => $"{e.Path}: {e.Kind}")
                .ToList();

            return (false, errorMessages);
        }
        catch (Exception ex)
        {
            return (false, new List<string> { $"Schema validation error: {ex.Message}" });
        }
    }
}
