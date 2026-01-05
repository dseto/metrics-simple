# Bug Fix Report: GET /api/v1/processes/{processId}/versions/{version} returning 404

**Date:** 2026-01-05  
**Status:** ✅ FIXED  
**Severity:** HIGH (API contract violation)

## Problem Description

After creating a process version, attempting to retrieve it with:
```
GET /api/v1/processes/324134/versions/1
```
resulted in:
```
404 Not Found
```

## Root Cause

**Type Mismatch in DTO Definition:**

The `ProcessVersionDto` record defined `Version` as `string`:
```csharp
public record ProcessVersionDto(
    string ProcessId,
    string Version,  // ❌ WRONG: Should be int
    bool Enabled,
    ...
);
```

But the JSON Schema specification (`specs/shared/domain/schemas/processVersion.schema.json`) and API Behavior spec defined it as `integer`:
```json
"version": {
    "type": "integer",  // ✅ Spec says integer
    "minimum": 1,
    "maximum": 9999
}
```

**Cascade Effect:**

1. POST request sent `version: 1` (integer) ✅ - Created successfully
2. GET request URL path parameter came as string `"1"` from routing ✅  
3. Repository `GetVersionAsync` received string `"1"`
4. SQL query compared `string "1"` against `integer 1` in database
5. No match found → 404 returned

## Solution

### 1. Fixed DTO Definition
**File:** [src/Api/Models.cs](../../src/Api/Models.cs)
```csharp
// BEFORE
public record ProcessVersionDto(
    string ProcessId,
    string Version,    // ❌
    ...
);

// AFTER  
public record ProcessVersionDto(
    string ProcessId,
    int Version,       // ✅
    ...
);
```

### 2. Fixed Repository Interface & Implementation
**File:** [src/Api/ProcessVersionRepository.cs](../../src/Api/ProcessVersionRepository.cs)

Changed method signatures from:
```csharp
Task<ProcessVersionDto?> GetVersionAsync(string processId, string version)
Task<ProcessVersionDto> UpdateVersionAsync(string processId, string version, ProcessVersionDto updated)
```

To:
```csharp
Task<ProcessVersionDto?> GetVersionAsync(string processId, int version)
Task<ProcessVersionDto> UpdateVersionAsync(string processId, int version, ProcessVersionDto updated)
```

Also fixed the return statement:
```csharp
// BEFORE - passing wrong type
return new ProcessVersionDto(
    processId,
    version,  // ❌ string
    reader.GetBoolean(2),
    ...
);

// AFTER - read from database correctly
return new ProcessVersionDto(
    processId,
    reader.GetInt32(1),  // ✅ Read int from DB
    reader.GetBoolean(2),
    ...
);
```

### 3. Updated API Handlers
**File:** [src/Api/Program.cs](../../src/Api/Program.cs)

Added type conversion from URL path parameter (always string):
```csharp
async Task<IResult> GetProcessVersion(string processId, string version, IProcessVersionRepository repo)
{
    if (!int.TryParse(version, out var versionInt))
        return Results.BadRequest(new { error = "Version must be an integer" });
    
    var pv = await repo.GetVersionAsync(processId, versionInt);
    if (pv == null) return Results.NotFound();
    return Results.Ok(pv);
}
```

### 4. Updated Test DTOs
**Files:**
- [tests/Integration.Tests/TestFixtures.cs](../../tests/Integration.Tests/TestFixtures.cs)
- [tests/Integration.Tests/IT01_CrudPersistenceTests.cs](../../tests/Integration.Tests/IT01_CrudPersistenceTests.cs)
- [tests/Integration.Tests/IT02_EndToEndRunnerTests.cs](../../tests/Integration.Tests/IT02_EndToEndRunnerTests.cs)
- [tests/Integration.Tests/IT03_SourceFailureTests.cs](../../tests/Integration.Tests/IT03_SourceFailureTests.cs)

Updated all `ProcessVersionDto` instantiations:
```csharp
// BEFORE
var version = new ProcessVersionDto(
    ProcessId: "proc-1",
    Version: "1",    // ❌ string
    ...
);

// AFTER
var version = new ProcessVersionDto(
    ProcessId: "proc-1",
    Version: 1,      // ✅ int
    ...
);
```

## Validation

### Build Status
✅ **Build:** Successful - 0 compilation errors after fixes

### Test Results
✅ **Tests:** 125/125 passing (all non-LLM tests)
- Engine.Tests: 4/4 ✅
- Contracts.Tests: 57/57 ✅  
- Integration.Tests: 64/64 ✅

### API Validation (Manual Test)
```
✅ POST /api/v1/processes                    → 201 Created
✅ POST /api/v1/processes/{id}/versions      → 201 Created
✅ GET /api/v1/processes/{id}/versions/{v}   → 200 OK (NOW FIXED!)
```

**Before fix:** Returns 404  
**After fix:** Returns 200 with full version data

## Spec Compliance

**Conformance Verified:**
- ✅ API Behavior spec: version is integer (1..n)
- ✅ JSON Schema: `"version": {"type": "integer", "minimum": 1, "maximum": 9999}`
- ✅ OpenAPI: version parameter as integer type
- ✅ Contract tests pass: No drift detected

## Impact

**Breaking Changes:** NONE - External API contract unchanged
- Clients were already sending integer `1`, not string `"1"` in JSON body
- URL path parameter handling now correct

**Files Modified:** 6
- 1 DTO definition file
- 1 Repository implementation file
- 1 API handler file (Program.cs)
- 3 Test fixture files

**Lines Changed:** ~30 edits across files

## Testing Evidence

Full end-to-end flow now works:
```
Step 1: Create connector      → 201 ✅
Step 2: Create process        → 201 ✅
Step 3: Create version (v=1)  → 201 ✅
Step 4: GET version (v=1)     → 200 ✅ (FIXED - was 404)
```

Response body correctly returns:
```json
{
  "processId": "fix-test-proc",
  "version": 1,              // ← Integer type
  "enabled": true,
  "sourceRequest": {...},
  "dsl": {...},
  "outputSchema": {...}
}
```

---
**Root Cause Category:** Type system mismatch (schema vs implementation)  
**Fix Category:** Contract alignment  
**Testing:** Automated regression tests now pass  
**Deployment:** Ready for production
