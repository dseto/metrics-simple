# Commit 2 Status: Server-Side OutputSchema Inference

**Date**: 2025-01-05  
**Status**: 🟡 IN PROGRESS - Partial success  
**Test Results**: IT13 still 0/4 passing (but different failures now)

## Changes Implemented

### 1. System Prompt Updated
- **Old Contract**: LLM asked to return {dsl, outputSchema, rationale, warnings}
- **New Contract**: LLM asked to return {dsl, notes, warnings}
- **Goal**: Remove unreliable outputSchema from LLM responsibility

### 2. OutputSchemaInferer.cs Created (NEW - 140 lines)
- Infers JSON schema from actual transformation output
- Handles: objects, arrays, primitives, nested structures
- Uses `JsonElement` for deterministic schema generation
- **Status**: ✅ Compiled and integrated

### 3. ParseChatCompletionResponse Updated
- Now accepts BOTH old (with outputSchema) and new (without) contracts
- Backward compatible with existing LLM responses
- **Status**: ✅ Flexible parsing implemented

### 4. GenerateDsl Endpoint Refactored
- Injected `IDslTransformer` to directly validate DSL
- **Old Flow**: Call `TransformValidateToCsv()` (validates against schema)
- **New Flow**: 
  1. Call transformer.Transform() directly (validate DSL compiles)
  2. Get actual output
  3. Infer schema from output
  4. Return result with inferred schema
- **Status**: 🟡 Partially working

## Test Results Analysis

**Current IT13 Status**: 0/4 PASSING (regression from 1/4)

### IT13_LLM_SimpleExtraction_PortuguesePrompt ❌ FAILING
- **Status Code**: 200 OK (DSL generation succeeded!)
- **Error**: Transform validation failing (IsValid=False)
- **Issue**: After getting DSL with inferred schema, the Transform endpoint validation still fails
- **Root Cause**: Likely schema mismatch or Transform validator issue

### IT13_LLM_ComplexTransformation_MixedLanguage ❌ FAILING  
- **Previous**: ✅ PASSING (was the only one working)
- **Status Code**: 200 OK (DSL generation succeeded!)
- **Error**: Transform validation failing (IsValid=False)
- **Issue**: Same as above - regression
- **Root Cause**: Our changes broke the previously-working test

### IT13_LLM_Aggregation_EnglishPrompt ❌ FAILING
- **Status Code**: 502 Bad Gateway (DSL generation failed)
- **Issue**: Backend returned error during DSL generation
- **Root Cause**: DSL transform is throwing, likely due to invalid Jsonata

### IT13_LLM_WeatherForecast_RealWorldPrompt ❌ FAILING
- **Status Code**: 502 Bad Gateway (DSL generation failed)
- **Issue**: Backend returned error during DSL generation
- **Root Cause**: Same as Aggregation - invalid Jsonata from LLM

## Critical Discovery: Regression!

**Before Commit 2**: 1/4 tests passing (ComplexTransformation)
**After Commit 2**: 0/4 tests passing

The changes to the GenerateDsl endpoint broke the previously-working test!

**Why?** The new code calls `transformer.Transform()` directly and throws on exception. But the old code had the repair loop that would retry. We lost that mechanism.

## What Went Wrong

The issue is in the new endpoint flow:
```csharp
try {
    transformOutput = transformer.Transform(inputJson, result.Dsl.Profile, result.Dsl.Text);
} catch (Exception transformEx) {
    // This catches DSL syntax errors
    // But now we throw immediately instead of attempting repair
    if (attempt >= maxRepairAttempts) throw;
    continue; // This retry doesn't help because LLM returns same DSL
}
```

The LLM returns invalid DSL (like `sales.{$group: category, ...}`), and we immediately fail with 502. The repair attempt would call the LLM again with error hints, but it's not happening consistently.

## Immediate Path Forward

**Option A: Revert Commit 2**
- Roll back to before outputSchema changes  
- Get back to 1/4 passing
- Focus on Commit 3 (Templates) instead

**Option B: Fix the Regression**
- Keep the schema inference logic (it's good)
- Restore the repair loop
- Make sure both old and new test paths work

**Option C: Accept 0/4 and Continue**
- Implement Template fallback (Commit 3)
- Hope templates fix the LLM DSL generation issues

## Recommendation

**Option B** is best. The schema inference logic is sound, but we broke the repair mechanism. We need to:

1. Restore exception handling and repair loop for DSL transform failures
2. Keep the OutputSchemaInferer logic
3. Validate that the previously-passing test works again

Then move to Commit 3 if still not ≥3/4.

## Metrics

| Test | Previous | Current | Change |
|------|----------|---------|--------|
| IT13_SimpleExtraction | ❌ FAIL | ❌ FAIL (200 OK but IsValid=False) | Changed error type |
| IT13_ComplexTransformation | ✅ PASS | ❌ FAIL | REGRESSION |
| IT13_Aggregation | ❌ FAIL | ❌ FAIL (502) | No change |
| IT13_WeatherForecast | ❌ FAIL | ❌ FAIL (502) | No change |
| **TOTAL** | **1/4** | **0/4** | **REGRESSION** |

---

**Decision Point**: Need to immediately fix the regression by restoring repair mechanism
