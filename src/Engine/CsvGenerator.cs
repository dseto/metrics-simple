using System.Text;
using System.Text.Json;

namespace Metrics.Engine;

public interface ICsvGenerator
{
    string GenerateCsv(JsonElement rows, IReadOnlyList<string> columns);
}

public sealed class CsvGenerator : ICsvGenerator
{
    private const string NL = "\n";

    public string GenerateCsv(JsonElement rows, IReadOnlyList<string> columns)
    {
        if (rows.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Input must be a JSON array");

        var items = rows.EnumerateArray().ToList();

        var sb = new StringBuilder();

        // Write header row
        sb.Append(string.Join(",", columns.Select(EscapeCsvValue)));
        sb.Append(NL);

        // Write data rows
        foreach (var item in items)
        {
            if (item.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException("Array items must be objects");

            var values = new List<string>();
            foreach (var column in columns)
            {
                if (item.TryGetProperty(column, out var value))
                {
                    values.Add(EscapeCsvValue(FormatCsvValue(value)));
                }
                else
                {
                    values.Add(string.Empty);
                }
            }
            sb.Append(string.Join(",", values));
            sb.Append(NL);
        }

        return sb.ToString();
    }

    private string FormatCsvValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() ?? string.Empty,
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null => string.Empty,
            _ => value.GetRawText()
        };
    }

    /// <summary>
    /// RFC4180: quote field if contains comma, quote, newline, or carriage return.
    /// Escape quotes by doubling: " -> ""
    /// </summary>
    private string EscapeCsvValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        // Check for characters that require quoting: comma, quote, newline, carriage return
        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r"))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
