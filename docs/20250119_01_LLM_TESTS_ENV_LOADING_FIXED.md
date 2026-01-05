# LLM Tests Environment Loading - Fixed ✅

**Date**: 2025-01-19  
**Status**: Fixed environment variable loading; some LLM tests now have JSON parsing issues  
**Tests**: 4 IT05 integration tests (Real LLM)

## Problem Summary
- User requested: **Remove Skip logic from LLM tests** (don't ignore them)
- User clarified: **Use REAL OpenRouter API** (not mock)
- Issue: Environment variables were not being loaded into the test process before `Program.cs` initialization

## Solution Implemented

### 1. Removed Skip Logic ✅
- Changed `[SkippableFact]` → `[Fact]` in all 4 IT05 tests
- Removed `Skip.If()` conditions
- Removed `Xunit.SkippableFact` NuGet package

### 2. Fixed Environment Variable Loading ✅
**File**: `tests/Integration.Tests/TestWebApplicationFactory.cs`

Added `LoadEnvFile()` method that:
- Reads `.env` file before `WebApplicationFactory` initializes `Program.cs`
- Parses `KEY=VALUE` lines with quote handling
- Sets environment variables into `[EnvironmentVariableTarget]::Process`
- **Critical**: This loads `METRICS_OPENROUTER_API_KEY` at the right time

```csharp
private void LoadEnvFile()
{
    // Look for .env file in the root directory
    var envPath = Path.Combine(
        Directory.GetCurrentDirectory(),
        "..\\..\\..",  // From tests/Integration.Tests/ -> project root
        ".env"
    );
    
    // Parse KEY=VALUE and set environment variables
    foreach (var line in File.ReadLines(envPath))
    {
        // Skip empty lines and comments
        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
            continue;
        
        // Parse and set
        var parts = trimmed.Split('=', 2);
        if (parts.Length == 2)
        {
            var key = parts[0].Trim();
            var value = parts[1].Trim();
            
            // Remove quotes
            if (value.StartsWith('"') && value.EndsWith('"'))
                value = value.Substring(1, value.Length - 2);
            
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}
```

### 3. Updated Instructions ✅
**File**: `copilot-prompts/backend.txt`

Added instruction:
```
## Environment Variables for LLM Tests

CRITICAL: The .env file at project root contains:
- METRICS_OPENROUTER_API_KEY: Real API key for OpenRouter
- METRICS_SECRET_KEY: Encryption key for JWT tokens

TestWebApplicationFactory now:
1. Automatically loads .env on initialization
2. Sets METRICS_OPENROUTER_API_KEY before Program.cs startup
3. Uses REAL OpenRouter API for all LLM tests (never mock)

Always ensure .env exists at project root with these variables.
```

## Current Test Status

### Test Results: 1 Passed, 2 Failed, 1 Passed (from 4 IT05 tests)

```
✅ IT05_01_RealLlmGenerateValidCpuDsl - PASSED
   - Generated valid DSL for CPU metrics
   - Latency: 2706ms
   
❌ IT05_02_RealLlmExtractFromText - FAILED
   - API returned 502 Bad Gateway
   - outputSchema has malformed JSON (double quotes escaping)
   - Error: 't' is invalid after a property name at position 101
   
❌ IT05_03_RealLlmRenameAndFilter - FAILED
   - API returned 502 Bad Gateway
   - outputSchema has malformed JSON (double quotes escaping)
   - Error: '"' is invalid after property name at position 100
   
✅ IT05_04_RealLlmMathAggregation - PASSED
   - Generated valid DSL for math aggregation
   - Latency: 21405ms
```

**Summary**: 2 passed, 2 failed

## Issue: LLM outputSchema JSON Corruption

The OpenRouter API (model: `openai/gpt-oss-120b`) is occasionally returning corrupted `outputSchema`:

### Example of Corrupted JSON:
```json
{
  "type": "array",
  "items": {
    "type": "object",
    "properties": {
      "fullName": {"type": "string"},
      "email": {"type": """""""""""""""""""""""""""""""""""""""""""...
    }
  }
}
```

### Root Cause Analysis:
1. LLM is escaping JSON incorrectly in the response
2. When parsing with `System.Text.Json`, it fails due to invalid quote sequences
3. This appears to be a model-specific issue with `openai/gpt-oss-120b`

## Next Steps

### Option A: Use Different LLM Model
- Current: `openai/gpt-oss-120b`
- Try: `openai/gpt-4o`, `anthropic/claude-3-5-sonnet` (more reliable JSON)
- File to update: `src/Api/AI/HttpOpenAiCompatibleProvider.cs` (line ~50)

### Option B: Add JSON Repair Logic
- Detect and fix common JSON corruption patterns
- Escape internal quotes properly before parsing
- File to update: `src/Api/AI/HttpOpenAiCompatibleProvider.cs` (ParseChatCompletionResponse method)

### Option C: Adjust LLM Prompt
- Add explicit JSON formatting instructions
- Use JSON Schema validation on LLM side
- Ensure proper escaping in prompt

## Files Modified

| File | Change |
|------|--------|
| `tests/Integration.Tests/TestWebApplicationFactory.cs` | Added LoadEnvFile() method |
| `copilot-prompts/backend.txt` | Added environment variable instructions |
| `.github/copilot-instructions.md` | Added note about .env loading |

## Proof of Environment Loading

When running tests with real OpenRouter API:
- ✅ API key is found and set correctly
- ✅ HTTPS requests to `https://openrouter.ai/api/v1/chat/completions` succeed
- ✅ Logs show "Sending request to AI provider: https://openrouter.ai/..."
- ✅ LLM inference happens in real-time (2700ms+ latency)

## Recommendations

1. **Keep current implementation** - Environment loading is now working correctly
2. **Investigate LLM model quality** - Consider using GPT-4o instead of gpt-oss-120b
3. **Add JSON validation** - Add pre-validation of LLM responses in `HttpOpenAiCompatibleProvider`
4. **Document trade-offs** - LLM tests are slower (2-25 seconds per test) but test real API integration
