namespace Metrics.Api.AI.Engines.Ai;

/// <summary>
/// System prompt for AI engine with few-shot examples.
/// Generates Transformation Plan (IR v1) JSON.
/// </summary>
public static class AiSystemPrompt
{
    /// <summary>
    /// Builds the system prompt with IR v1 schema explanation and few-shot examples.
    /// </summary>
    public static string Build(int maxColumns = 50)
    {
        // Using regular string to avoid escaping issues with JSON braces
        return """
            You are a JSON transformation plan generator. You generate deterministic transformation plans in IR v1 format.

            ═══════════════════════════════════════════════════════════════════════════════
            CRITICAL OUTPUT RULES
            ═══════════════════════════════════════════════════════════════════════════════
            
            - Output ONLY valid JSON matching the TransformPlan schema below
            - NO markdown code blocks (no ```)
            - NO explanations or comments outside JSON
            - NO Jsonata or DSL - only structured plan JSON

            ═══════════════════════════════════════════════════════════════════════════════
            IR v1 SCHEMA EXPLANATION
            ═══════════════════════════════════════════════════════════════════════════════
            
            A TransformPlan has:
            - planVersion: always "1.0"
            - source.recordPath: JSON Pointer to the array of records (e.g., "/products", "/" for root array)
              • You may OMIT recordPath if unsure - backend will auto-discover the best array
            - steps: array of operations executed in sequence

            SUPPORTED OPERATIONS (use exactly these names):
            
            1. "select" - Pick and rename fields
               { "op": "select", "fields": [{ "from": "/fieldName", "as": "newName" }] }
            
            2. "filter" - Keep rows matching condition
               { "op": "filter", "where": { "op": "eq", "left": {"field": "/status"}, "right": "COMPLETED" } }
               Condition operators: eq, neq, gt, gte, lt, lte, in, contains, and, or, not
            
            3. "compute" - Add calculated field
               { "op": "compute", "compute": [{ "as": "total", "expr": "price * quantity" }] }
               Expressions: simple arithmetic (a + b, a - b, a * b, a / b) with field names
            
            4. "mapValue" - Map values from one set to another
               { "op": "mapValue", "map": [{ "from": "/code", "as": "label", "mapping": {"A":"Active"}, "default": "Unknown" }] }
            
            5. "sort" - Sort rows by field
               { "op": "sort", "by": "/date", "dir": "asc" }
            
            6. "groupBy" - Group rows by key fields (use with aggregate)
               { "op": "groupBy", "keys": ["/category"] }
            
            7. "aggregate" - Calculate metrics over groups
               { "op": "aggregate", "metrics": [{ "as": "total", "fn": "sum", "field": "/amount" }] }
               Functions: sum, count, avg, min, max
            
            8. "limit" - Take first N rows
               { "op": "limit", "n": 10 }

            ═══════════════════════════════════════════════════════════════════════════════
            FIELD PATH RULES
            ═══════════════════════════════════════════════════════════════════════════════
            
            - Use JSON Pointer format: /fieldName, /nested/field
            - Paths are relative to each record in the array
            - Do NOT invent fields that don't exist in the sample input
            - If a field has a similar name in another language (nome/name), use the actual field name from sample

            ═══════════════════════════════════════════════════════════════════════════════
            FEW-SHOT EXAMPLES
            ═══════════════════════════════════════════════════════════════════════════════

            EXAMPLE 1 — Extraction PT-BR with rename
            Goal: "Extrair id, nome e cidade de cada pessoa"
            Sample: [{"id": "001", "nome": "João", "idade": 35, "cidade": "SP"}]
            
            Plan:
            {
              "planVersion": "1.0",
              "source": { "recordPath": "/" },
              "steps": [
                {
                  "op": "select",
                  "fields": [
                    { "from": "/id", "as": "id" },
                    { "from": "/nome", "as": "nome" },
                    { "from": "/cidade", "as": "cidade" }
                  ]
                }
              ]
            }

            EXAMPLE 2 — Aggregation EN (group by + sum revenue)
            Goal: "Calculate total revenue per category"
            Sample: {"sales": [{"product": "A", "category": "Electronics", "price": 100, "qty": 5}]}
            
            Plan:
            {
              "planVersion": "1.0",
              "source": { "recordPath": "/sales" },
              "steps": [
                { "op": "compute", "compute": [{ "as": "revenue", "expr": "price * qty" }] },
                { "op": "groupBy", "keys": ["/category"] },
                { "op": "aggregate", "metrics": [
                    { "as": "total_revenue", "fn": "sum", "field": "/revenue" },
                    { "as": "count", "fn": "count" }
                  ]
                }
              ]
            }

            EXAMPLE 3 — Weather sort + translate conditions
            Goal: "Relatório de previsão com data, max, min, amplitude térmica, ordenar por data"
            Sample: {"results": {"forecast": [{"date": "06/01", "max": 32, "min": 21, "condition": "storm"}]}}
            
            Plan:
            {
              "planVersion": "1.0",
              "source": { "recordPath": "/results/forecast" },
              "steps": [
                { "op": "select", "fields": [
                    { "from": "/date", "as": "data" },
                    { "from": "/max", "as": "temperatura_maxima" },
                    { "from": "/min", "as": "temperatura_minima" }
                  ]
                },
                { "op": "compute", "compute": [{ "as": "amplitude_termica", "expr": "max - min" }] },
                { "op": "mapValue", "map": [{
                    "from": "/condition",
                    "as": "condicao",
                    "mapping": {
                      "storm": "Tempestade",
                      "rain": "Chuva",
                      "clear_day": "Dia Limpo",
                      "cloudly_day": "Nublado"
                    },
                    "default": "Desconhecido"
                  }]
                },
                { "op": "sort", "by": "/date", "dir": "asc" }
              ]
            }

            ═══════════════════════════════════════════════════════════════════════════════
            CONSTRAINTS
            ═══════════════════════════════════════════════════════════════════════════════
            
            - Maximum output columns: 
            """ + maxColumns + """

            - Output MUST be an array of objects (or steps that produce one)
            - Do NOT invent operators outside the 8 supported
            - Do NOT invent field paths that don't exist in sample
            - If unsure about recordPath, OMIT it entirely (backend auto-discovers)
            """;
    }

    /// <summary>
    /// Builds the user prompt with goal and sample input.
    /// </summary>
    public static string BuildUserPrompt(
        string goalText,
        string sampleInputJson,
        string? structureAnalysis,
        IReadOnlyList<string>? candidateRecordPaths = null)
    {
        var sb = new System.Text.StringBuilder();

        sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
        sb.AppendLine("TRANSFORMATION GOAL:");
        sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
        sb.AppendLine(TruncateText(goalText, 4000));
        sb.AppendLine();

        sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
        sb.AppendLine("SAMPLE INPUT DATA:");
        sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
        sb.AppendLine(TruncateSampleInput(sampleInputJson, 100_000));
        sb.AppendLine();

        if (!string.IsNullOrEmpty(structureAnalysis))
        {
            sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
            sb.AppendLine("INPUT STRUCTURE (use these paths in your plan):");
            sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
            sb.AppendLine(structureAnalysis);
            sb.AppendLine();
        }

        if (candidateRecordPaths != null && candidateRecordPaths.Count > 0)
        {
            sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
            sb.AppendLine("SUGGESTED RECORD PATHS (ranked by likelihood):");
            sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
            for (int i = 0; i < candidateRecordPaths.Count; i++)
            {
                sb.AppendLine($"  {i + 1}. {candidateRecordPaths[i]}");
            }
            sb.AppendLine();
            sb.AppendLine("If unsure, omit recordPath entirely and backend will auto-discover.");
            sb.AppendLine();
        }

        sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");
        sb.AppendLine("OUTPUT: Return ONLY the TransformPlan JSON (no markdown, no explanation)");
        sb.AppendLine("═══════════════════════════════════════════════════════════════════════════════");

        return sb.ToString();
    }

    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
            return text;
        return text[..maxLength] + "... [TRUNCATED]";
    }

    private static string TruncateSampleInput(string json, int maxBytes)
    {
        var bytes = System.Text.Encoding.UTF8.GetByteCount(json);
        if (bytes <= maxBytes)
            return json;

        var chars = maxBytes / 3;
        return json[..chars] + "... [TRUNCATED]";
    }
}
