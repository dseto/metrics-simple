using System.Text.Json;

namespace Metrics.Engine;

public sealed record TransformResult(bool IsValid, JsonElement? OutputJson, IReadOnlyList<string> Errors, string? CsvPreview);

public interface IDslTransformer
{
    JsonElement Transform(JsonElement input, string dslProfile, string dslText);
}

public sealed class EngineService
{
    private readonly IDslTransformer _transformer;
    private readonly ISchemaValidator _schemaValidator;
    private readonly ICsvGenerator _csvGenerator;

    public EngineService(IDslTransformer transformer, ISchemaValidator schemaValidator, ICsvGenerator csvGenerator)
    {
        _transformer = transformer;
        _schemaValidator = schemaValidator;
        _csvGenerator = csvGenerator;
    }

    public TransformResult TransformValidateToCsv(JsonElement input, string dslProfile, string dslText, JsonElement outputSchema)
    {
        try
        {
            // Step 1: Execute DSL transformation
            var output = _transformer.Transform(input, dslProfile, dslText);

            // Step 2: Validate output against schema
            var (isValid, errors) = _schemaValidator.ValidateAgainstSchema(output, outputSchema);
            if (!isValid)
            {
                return new TransformResult(false, null, errors, null);
            }

            // Step 3: Generate CSV preview
            var csvPreview = _csvGenerator.GenerateCsv(output);

            return new TransformResult(true, output, Array.Empty<string>(), csvPreview);
        }
        catch (Exception ex)
        {
            return new TransformResult(false, null, new[] { ex.Message }, null);
        }
    }
}
