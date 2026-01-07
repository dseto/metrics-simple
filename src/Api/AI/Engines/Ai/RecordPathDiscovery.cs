using System.Text.Json;

namespace Metrics.Api.AI.Engines.Ai;

/// <summary>
/// Discovers the best record path (array of objects) in a JSON input.
/// </summary>
public static class RecordPathDiscovery
{
    /// <summary>
    /// Well-known property names that commonly contain recordsets
    /// </summary>
    private static readonly HashSet<string> WellKnownArrayNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "items", "results", "data", "records", "rows", "entries",
        "products", "sales", "orders", "customers", "users",
        "forecast", "values", "list", "elements", "nodes"
    };

    /// <summary>
    /// Result of record path discovery
    /// </summary>
    public record DiscoveryResult(
        bool Success,
        string? RecordPath,
        int Score,
        List<CandidatePath> Candidates,
        string? Error
    );

    /// <summary>
    /// A candidate path with its score
    /// </summary>
    public record CandidatePath(string Path, int Score, int ArrayLength, bool HasObjectItems);

    /// <summary>
    /// Discovers the best recordPath in the given JSON element.
    /// </summary>
    /// <param name="input">The sample input JSON</param>
    /// <param name="goalText">Optional goal text for keyword matching</param>
    /// <returns>Discovery result with best path and candidates</returns>
    public static DiscoveryResult Discover(JsonElement input, string? goalText = null)
    {
        var candidates = new List<CandidatePath>();
        var goalWords = ExtractGoalWords(goalText);

        // Case 1: Root is already an array
        if (input.ValueKind == JsonValueKind.Array)
        {
            var score = ScoreArray(input, "/", goalWords);
            var hasObjects = HasObjectItems(input);
            candidates.Add(new CandidatePath("/", score, input.GetArrayLength(), hasObjects));
        }
        // Case 2: Root is object - search for array properties
        else if (input.ValueKind == JsonValueKind.Object)
        {
            DiscoverArraysRecursive(input, "", candidates, goalWords, maxDepth: 3);
        }
        else
        {
            return new DiscoveryResult(false, null, 0, candidates, "NoRecordsetFound: Input is not an object or array");
        }

        if (candidates.Count == 0)
        {
            return new DiscoveryResult(false, null, 0, candidates, "NoRecordsetFound: No arrays found in input");
        }

        // Sort by score descending, then by path length (prefer shallower)
        var sorted = candidates
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.Path.Count(ch => ch == '/'))
            .ToList();

        var best = sorted[0];

        // Reject if best candidate doesn't have object items (not a proper recordset)
        if (!best.HasObjectItems && best.ArrayLength > 0)
        {
            // Check if any candidate has object items
            var bestWithObjects = sorted.FirstOrDefault(c => c.HasObjectItems);
            if (bestWithObjects != null)
            {
                best = bestWithObjects;
            }
            else
            {
                return new DiscoveryResult(
                    false, null, 0, sorted,
                    "WrongShape: Found arrays but none contain objects (recordsets must be array<object>)");
            }
        }

        return new DiscoveryResult(true, best.Path, best.Score, sorted, null);
    }

    private static void DiscoverArraysRecursive(
        JsonElement obj,
        string currentPath,
        List<CandidatePath> candidates,
        HashSet<string> goalWords,
        int maxDepth,
        int currentDepth = 0)
    {
        if (currentDepth > maxDepth) return;
        if (obj.ValueKind != JsonValueKind.Object) return;

        foreach (var prop in obj.EnumerateObject())
        {
            var propPath = $"{currentPath}/{prop.Name}";

            if (prop.Value.ValueKind == JsonValueKind.Array)
            {
                var score = ScoreArray(prop.Value, propPath, goalWords);
                var hasObjects = HasObjectItems(prop.Value);
                candidates.Add(new CandidatePath(propPath, score, prop.Value.GetArrayLength(), hasObjects));
            }
            else if (prop.Value.ValueKind == JsonValueKind.Object)
            {
                // Recurse into nested objects
                DiscoverArraysRecursive(prop.Value, propPath, candidates, goalWords, maxDepth, currentDepth + 1);
            }
        }
    }

    private static int ScoreArray(JsonElement array, string path, HashSet<string> goalWords)
    {
        var score = 0;

        // Base score for being an array
        score += 10;

        // Score for array length (more items = more likely a recordset)
        var length = array.GetArrayLength();
        if (length > 0) score += Math.Min(length, 20); // Cap at 20 points
        if (length >= 3) score += 10; // Bonus for meaningful size

        // Score for having object items (critical for recordset)
        if (HasObjectItems(array))
        {
            score += 50; // Strong bonus for object items
        }

        // Score for well-known property names
        var propName = ExtractPropertyName(path);
        if (!string.IsNullOrEmpty(propName) && WellKnownArrayNames.Contains(propName))
        {
            score += 30;
        }

        // Score for goal word overlap
        if (goalWords.Count > 0 && !string.IsNullOrEmpty(propName))
        {
            if (goalWords.Contains(propName.ToLowerInvariant()))
            {
                score += 25;
            }
            // Partial match (propName contains goal word or vice versa)
            foreach (var word in goalWords)
            {
                if (propName.Contains(word, StringComparison.OrdinalIgnoreCase) ||
                    word.Contains(propName, StringComparison.OrdinalIgnoreCase))
                {
                    score += 10;
                    break;
                }
            }
        }

        // Penalize deeply nested paths
        var depth = path.Count(c => c == '/') - 1; // -1 because root starts with /
        score -= depth * 5;

        return score;
    }

    private static bool HasObjectItems(JsonElement array)
    {
        if (array.ValueKind != JsonValueKind.Array) return false;
        if (array.GetArrayLength() == 0) return false;

        // Check first few items
        var count = 0;
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Object) return true;
            if (++count >= 3) break;
        }
        return false;
    }

    private static string? ExtractPropertyName(string path)
    {
        if (string.IsNullOrEmpty(path) || path == "/") return null;
        var lastSlash = path.LastIndexOf('/');
        return lastSlash >= 0 ? path[(lastSlash + 1)..] : path;
    }

    private static HashSet<string> ExtractGoalWords(string? goalText)
    {
        if (string.IsNullOrWhiteSpace(goalText)) return new HashSet<string>();

        // Extract meaningful words (lowercase, min 3 chars)
        var words = goalText
            .ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', ':', ';', '-', '_', '(', ')', '[', ']' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 3)
            .Where(w => !StopWords.Contains(w))
            .ToHashSet();

        return words;
    }

    private static readonly HashSet<string> StopWords = new()
    {
        "the", "and", "for", "from", "with", "into", "that", "this",
        "each", "all", "any", "csv", "json", "create", "make", "get",
        "extract", "transform", "convert", "array", "object", "field"
    };
}
