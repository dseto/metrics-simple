using System.Text.RegularExpressions;

namespace Metrics.Api.AI;

/// <summary>
/// Detects known invalid Jsonata patterns that should NOT be retried,
/// but rather fallback to templates immediately.
/// </summary>
public static class DslBadPatternDetector
{
    /// <summary>
    /// Known bad pattern in DSL that should trigger immediate template fallback.
    /// </summary>
    public enum BadPatternType
    {
        /// <summary>
        /// No bad pattern detected
        /// </summary>
        None,
        
        /// <summary>
        /// Pattern: $group or $group(...) - NOT a valid Jsonata operator
        /// Should use group-by or $group(...) is not available
        /// </summary>
        InvalidGroupFunction,
        
        /// <summary>
        /// Pattern: [field] for sorting (e.g., items[date])
        /// Should use ^(field) for ascending or ~(field) for descending
        /// </summary>
        InvalidSortArrayNotation,
        
        /// <summary>
        /// Pattern: [!condition] for filtering - ! operator doesn't exist in Jsonata
        /// Should use [not condition] or [condition=false]
        /// </summary>
        InvalidNotOperator
    }

    /// <summary>
    /// Detect if DSL contains known bad patterns.
    /// </summary>
    public static BadPatternType Detect(string dslText)
    {
        if (string.IsNullOrWhiteSpace(dslText))
            return BadPatternType.None;

        // Pattern 1: $group (LLM confusion with other JSON libraries)
        // Match: $group or $group(...) but NOT $$group or within a comment
        if (Regex.IsMatch(dslText, @"(?<!\$)\$group\s*(?:\(|:)", RegexOptions.IgnoreCase))
        {
            return BadPatternType.InvalidGroupFunction;
        }

        // Pattern 2: [field] for sorting (mistaking array indexing for field sorting)
        // Match: ...items[date] or ...sales[date] where it looks like field sort
        // Conservative: only flag if it's clearly wrong (has ~ or ^ elsewhere = knows correct syntax)
        if (Regex.IsMatch(dslText, @"\w+\[\w+\]") && 
            !Regex.IsMatch(dslText, @"[\^\~]\("))  // if has correct syntax, don't flag
        {
            // Check if this looks like a sort attempt (goal context would help here)
            // For now, only flag if it's CLEARLY wrong: multiple fields with [notation]
            var bracketMatches = Regex.Matches(dslText, @"\w+\[\w+\]");
            if (bracketMatches.Count > 1)
            {
                return BadPatternType.InvalidSortArrayNotation;
            }
        }

        // Pattern 3: [!condition] - NOT operator doesn't exist in Jsonata
        if (Regex.IsMatch(dslText, @"\[!\s*\w+", RegexOptions.IgnoreCase))
        {
            return BadPatternType.InvalidNotOperator;
        }

        return BadPatternType.None;
    }

    /// <summary>
    /// Describe the bad pattern in human-readable form for logging.
    /// </summary>
    public static string Describe(BadPatternType pattern)
    {
        return pattern switch
        {
            BadPatternType.InvalidGroupFunction =>
                "$group is not a Jsonata function (it's from other JSON libraries). Use group-by(...) instead.",
            
            BadPatternType.InvalidSortArrayNotation =>
                "[field] notation for sorting is invalid. Use ^(field) for ascending or ~(field) for descending.",
            
            BadPatternType.InvalidNotOperator =>
                "[!condition] is invalid (! operator doesn't exist). Use [not condition] or [condition=false].",
            
            _ => "Unknown pattern"
        };
    }

    /// <summary>
    /// Determine if this bad pattern should skip repair and go directly to template.
    /// </summary>
    public static bool ShouldSkipRepairAndFallback(BadPatternType pattern)
    {
        return pattern switch
        {
            BadPatternType.InvalidGroupFunction => true,  // LLM won't fix this in context
            BadPatternType.InvalidSortArrayNotation => true,  // Different operator syntax
            BadPatternType.InvalidNotOperator => true,  // Different operator syntax
            _ => false
        };
    }
}
