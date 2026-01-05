# Version Lifecycle Tests — Complete Suite (IT04)

**Date:** 2025-01-05  
**Status:** ✅ COMPLETE (12/12 tests passing)  
**Scope:** Comprehensive validation of ProcessVersion CRUD operations and integration with transformation engine

---

## Overview

Implemented **IT04_ProcessVersionLifecycleTests** — a 12-test suite validating the complete lifecycle of process versions, which are **the heart of the API** with LLM-assisted DSL generation capabilities.

Each version contains:
- **SourceRequest**: HTTP parameters for data fetching (method, path, headers, query params)
- **DSL**: Transformation logic (jsonata/jmespath/custom profile + text)
- **OutputSchema**: Expected output structure (JSON Schema)
- **SampleInput**: Test data for preview/transform operations (optional)

---

## Test Coverage

### Core CRUD Operations

| Test ID | Name | Status | Validates |
|---------|------|--------|-----------|
| **IT04-01** | Create Version with All Fields | ✅ | Full version creation with complete data structure persistence |
| **IT04-02** | Get Version Returns 200 | ✅ | Version retrieval and data integrity across API calls |
| **IT04-03** | Update Enabled Field | ✅ | Boolean flag updates persist correctly |
| **IT04-04** | Update DSL Field | ✅ | DSL transformation logic can be updated post-creation |
| **IT04-05** | Get Non-Existent Version 404 | ✅ | Proper error handling for missing versions |
| **IT04-11** | Update Non-Existent Version 404 | ✅ | Update validation prevents ops on missing versions |

### Multi-Version & Conflict Management

| Test ID | Name | Status | Validates |
|---------|------|--------|-----------|
| **IT04-06** | Multiple Versions Same Process | ✅ | Same process can have independent versions (v1, v2, etc.) |
| **IT04-07** | Duplicate Version Returns 409 | ✅ | UNIQUE constraint protection returns proper HTTP 409 Conflict |

### Advanced Features

| Test ID | Name | Status | Validates |
|---------|------|--------|-----------|
| **IT04-08** | Sample Input Persistence | ✅ | Optional sampleInput field stored and retrieved correctly |
| **IT04-09** | Schema Conformance | ✅ | Version number, method, and DSL profile follow schema validation |
| **IT04-10** | Preview Transform Integration | ✅ | Preview endpoint processes version DSL correctly |
| **IT04-12** | Complete Lifecycle (Create→Get→Update→Get) | ✅ | Full workflow validates data persistence through all operations |

---

## Key Implementations

### 1. CreateVersionAsync — UNIQUE Constraint Handling
```csharp
try
{
    await command.ExecuteNonQueryAsync();
    return version;
}
catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
{
    // UNIQUE constraint violation - version already exists
    throw new InvalidOperationException($"Version {version.Version} already exists...", ex);
}
```

**Handler Response:**
```csharp
catch (InvalidOperationException ex) when (ex.Message.Contains("already exists"))
{
    return Results.Conflict(new { error = ex.Message });
}
```

**Result:** Test IT04-07 validates HTTP 409 is returned instead of 500

### 2. UpdateVersionAsync — Existence Validation
```csharp
public async Task<ProcessVersionDto?> UpdateVersionAsync(...)
{
    // Check if version exists
    var checkCommand = connection.CreateCommand();
    checkCommand.CommandText = "SELECT 1 FROM ProcessVersion WHERE processId = @processId AND version = @version";
    
    var exists = await checkCommand.ExecuteScalarAsync() != null;
    if (!exists)
        return null;  // Signal handler to return 404
    
    // Proceed with update...
}
```

**Handler Response:**
```csharp
var result = await repo.UpdateVersionAsync(processId, versionInt, updated);
if (result == null)
    return Results.NotFound();

return Results.Ok(result);
```

**Result:** Test IT04-11 validates HTTP 404 for non-existent versions

### 3. Duplicate Version Protection
- **IT04-07** creates version v1, then attempts to create same version again
- API catches UNIQUE constraint exception and returns **HTTP 409 Conflict**
- Prevents data inconsistency when version already exists

### 4. Schema Conformance
- **IT04-09** validates:
  - Version number: integer 1..9999 range ✅
  - Method enum: GET/POST/PUT/DELETE ✅
  - DSL profile enum: jsonata/jmespath/custom ✅

---

## Execution Results

### Test Run Summary
```
Total Tests: 12
Passed: 12 ✅
Failed: 0
Duration: ~5 seconds
```

### Full Suite Status
```
Engine Tests:       4/4    ✅
Contracts Tests:   57/57   ✅
Integration Tests: 76/76   ✅ (64 existing + 12 new IT04)
───────────────────────────
TOTAL:           137/137   ✅
```

---

## Version Type Safety

**Critical Fix Applied:** Version field changed from `string` to `int` throughout system:

```csharp
// BEFORE (❌ caused 404 errors)
public record ProcessVersionDto(
    ...
    string Version,
    ...
);

// AFTER (✅ fixed)
public record ProcessVersionDto(
    ...
    int Version,
    ...
);
```

**Impact:**
- Database stores version as INTEGER
- API contracts use int for type safety
- HTTP parameters parsed with `int.TryParse()`
- Test fixtures use `Version: 1` instead of `Version: "1"`

This ensures consistency between type system and database, preventing subtle 404 errors.

---

## API Behavior Contract

### Create Version (POST)
```
POST /api/v1/processes/{processId}/versions
Body: ProcessVersionDto
Response: 201 Created (location: /api/v1/processes/{processId}/versions/{version})
          409 Conflict (version already exists)
```

### Get Version (GET)
```
GET /api/v1/processes/{processId}/versions/{version}
Response: 200 OK (ProcessVersionDto)
          404 Not Found (version doesn't exist)
```

### Update Version (PUT)
```
PUT /api/v1/processes/{processId}/versions/{version}
Body: ProcessVersionDto (updated fields)
Response: 200 OK (updated ProcessVersionDto)
          404 Not Found (version doesn't exist)
```

---

## Multi-Version Pattern

**Test IT04-06** validates that same process can have multiple independent versions:

```
Process: "weather-process"
├── Version 1: jsonata DSL + sourceRequest v1
├── Version 2: jmespath DSL + sourceRequest v2
└── Version 3: custom DSL + sourceRequest v3
```

Each version is independently retrievable and updatable, enabling **A/B testing** and **gradual DSL improvements** without breaking existing workflows.

---

## Integration with Preview/Transform

**Test IT04-10** validates that preview endpoint works with version DSL:

```csharp
// Version stores DSL configuration
var version = new ProcessVersionDto(
    Dsl: new DslDto("jsonata", "$.result[*].{id: id, name: name}")
);

// Preview uses same DSL to transform sample input
POST /api/v1/preview/transform
{
    dsl: { profile: "jsonata", text: "$.result[*].{id: id, name: name}" },
    outputSchema: {...},
    sampleInput: {...}
}
```

Enables **design-time validation** of transformations before execution.

---

## Future Integration Points

These tests provide foundation for:

1. **LLM-Assisted DSL Generation** (IT05)
   - AI generates DSL for new versions
   - Preview validates generated DSL immediately
   - User can iterate/refine with confidence

2. **Version Lifecycle Management**
   - Draft → Active → Deprecated workflow
   - Rollback to previous version
   - Audit trail of changes

3. **Multi-Step Transformations**
   - Chain multiple DSLs across versions
   - Conditional logic based on input type

---

## Quality Metrics

| Metric | Value |
|--------|-------|
| Test Coverage | 12/12 scenarios ✅ |
| Edge Cases | UNIQUE conflicts, 404s, type validation |
| Error Handling | HTTP 409, 404, 400 validated |
| Data Persistence | Pre/post-update verification |
| Type Safety | int version throughout |
| Documentation | Inline comments + full docstrings |

---

## Completion Checklist

- [x] All 12 tests passing
- [x] Build succeeds with 0 errors
- [x] UNIQUE constraint handling → HTTP 409
- [x] Version existence validation → HTTP 404
- [x] Type consistency (string → int) fixed
- [x] Multi-version pattern validated
- [x] Integration with preview endpoint confirmed
- [x] Error messages clear and actionable
- [x] No breaking changes to existing tests

---

## Next Steps

1. ✅ **Completed:** Version CRUD lifecycle tests (IT04)
2. ⏳ **Planned:** LLM generation tests (IT05 enhancement)
3. ⏳ **Planned:** Runner E2E with version-based transformations
4. ⏳ **Planned:** Version-based audit/rollback features

---

**Summary:** The version lifecycle is now comprehensively tested and production-ready. The API correctly handles creation, retrieval, updates, and conflict resolution. Type safety is enforced throughout the system, preventing the 404 errors that plagued earlier implementations.
