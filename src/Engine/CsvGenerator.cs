using System.Text;
using System.Text.Json;

namespace Metrics.Engine;

public interface ICsvGenerator
{
    string GenerateCsv(JsonElement jsonArray);
}

public sealed class CsvGenerator : ICsvGenerator
{
    public string GenerateCsv(JsonElement jsonArray)
    {
        if (jsonArray.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Input must be a JSON array");

        var items = jsonArray.EnumerateArray().ToList();
        if (items.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();

        // Extract headers from the first object
        var firstItem = items[0];
        if (firstItem.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Array items must be objects");

        var headers = firstItem.EnumerateObject()
            .Select(p => p.Name)
            .ToList();

        // Write header row
        sb.AppendLine(string.Join(",", headers.Select(EscapeCsvValue)));

        // Write data rows
        foreach (var item in items)
        {
            var values = new List<string>();
            foreach (var header in headers)
            {
                if (item.TryGetProperty(header, out var value))
                {
                    values.Add(EscapeCsvValue(FormatCsvValue(value)));
                }
                else
                {
                    values.Add(string.Empty);
                }
            }
            sb.AppendLine(string.Join(",", values));
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

    private string EscapeCsvValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
