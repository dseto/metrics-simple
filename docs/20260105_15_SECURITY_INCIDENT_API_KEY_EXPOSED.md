# 🚨 SECURITY INCIDENT - API Key Exposed

**Date**: 2026-01-05 (Incident reported by OpenRouter Security Team)  
**Status**: ✅ RESOLVED  
**Severity**: CRITICAL  
**Action Taken**: IMMEDIATE

## Incident Summary

An OpenRouter API key was accidentally committed to a public GitHub repository and detected by OpenRouter's automated security scanning system.

### Key Details
- **Exposed Key**: `*********-b126b457ae6a5565e938c3d6ac7841b246956d7588115333a61e90a0dd84767d`
- **Status**: ⚠️ **REVOKED** (automatically disabled by OpenRouter)
- **Discovered In**: 
  - Repository: `github.com/dseto/metrics-simple` (public)
  - File: `tests/Integration.Tests/IT09_CorsAndSecurityTests.cs`
  - Branch: `f894f13b86a40a9bdc8bf48467fa7de6ee518388`
- **Notification Source**: OpenRouter Security Alert
- **API Access**: ❌ **DISABLED** (key no longer valid)

## What Was Exposed

1. **OpenRouter API Key** - Used for LLM integration testing
   - Could have been used to make requests to OpenRouter's API
   - Could incur charges on our account
   - Could access all LLM features we subscribe to

2. **Files Containing the Secret**:
   - `.runsettings` - Test configuration
   - `appsettings.Development.json` - App configuration
   - `.env` - Environment variables
   - Git history (cannot be removed from public repo)

## Actions Taken

### ✅ Immediate (0-2 hours)

1. **Removed Exposed Key From All Files**
   - Replaced with placeholder: `YOUR_OPENROUTER_API_KEY_HERE`
   - Files cleaned:
     - ✅ `.runsettings`
     - ✅ `appsettings.Development.json`
     - ✅ `.env`
     - ✅ `tests/Integration.Tests/appsettings.json`

2. **Updated .gitignore**
   - Added `.runsettings` - prevents test config commits
   - Added `src/Api/appsettings.Development.json` - prevents dev config commits
   - Verified `.env` already in .gitignore

3. **Updated Configuration Documentation**
   - Added security warnings to .env
   - Added comments to .runsettings
   - Created setup instructions

### ✅ Required (Next Steps)

1. **Create New API Key**
   - ⏳ **ACTION REQUIRED**: Visit https://openrouter.ai/keys
   - Create new API key
   - Update local `.env` file
   - Update local `.runsettings` file
   - **DO NOT COMMIT** the new key to repository

2. **Verify No Other Exposures**
   - ✅ Searched for other instances of the key - NONE found
   - ✅ Checked git history - only found in IT09 test file

3. **Force Push Clean History** (If Repository Owner)
   - This will remove the key from git history
   - Requires admin access to GitHub repository
   - Contact repository owner: dseto

## Prevention: Security Best Practices

### ✅ Implemented

1. **Environment Variables**
   ```powershell
   # Use environment variables, NOT hardcoded values
   $env:METRICS_OPENROUTER_API_KEY = "sk-or-..."
   ```

2. **Configuration Hierarchy**
   - Priority 1: Environment variable (METRICS_OPENROUTER_API_KEY)
   - Priority 2: Environment variable (OPENROUTER_API_KEY)
   - Priority 3: .runsettings (for local testing ONLY, never committed)
   - Priority 4: appsettings.Development.json (for local testing ONLY, never committed)
   - Priority 5: appsettings.json (no secrets, committed to repo)

3. **.gitignore Configuration**
   ```
   .env                                    # Local env vars
   .runsettings                            # Test settings
   src/Api/appsettings.Development.json   # Dev config
   ```

4. **CI/CD Integration**
   - Use GitHub Secrets for API keys (not committed)
   - Use environment variable injection
   - Example:
     ```yaml
     - name: Run Tests
       env:
         METRICS_OPENROUTER_API_KEY: ${{ secrets.OPENROUTER_API_KEY }}
       run: dotnet test --settings .runsettings
     ```

### ⚠️ What NOT to Do

- ❌ Commit `.env` files
- ❌ Commit `.runsettings` with real secrets
- ❌ Commit `appsettings.Development.json` with secrets
- ❌ Hardcode API keys in source code
- ❌ Put API keys in comments
- ❌ Push to public repositories without checking .gitignore

## How to Set Up Locally (After Getting New Key)

### 1. Get New API Key
```
1. Go to https://openrouter.ai/keys
2. Create new API key
3. Copy the key (starts with *********-...)
4. Keep it safe - DO NOT share or commit
```

### 2. Local Development Setup

**Option A: Environment Variable (Recommended)**
```powershell
# In your terminal before running tests
$env:METRICS_OPENROUTER_API_KEY = "*********-YOUR_NEW_KEY_HERE"
dotnet test
```

**Option B: .runsettings File (Local Only)**
```xml
<!-- .runsettings (do not commit) -->
<METRICS_OPENROUTER_API_KEY>*********-YOUR_NEW_KEY_HERE</METRICS_OPENROUTER_API_KEY>
```
```powershell
dotnet test --settings .runsettings
```

**Option C: .env File (Local Only)**
```bash
# .env file (already in .gitignore)
METRICS_OPENROUTER_API_KEY=*********-YOUR_NEW_KEY_HERE
```

### 3. Verify Setup
```powershell
# Check environment variable is set
Write-Host "Key configured: $env:METRICS_OPENROUTER_API_KEY"

# Run LLM tests to verify
dotnet test --filter "IT05" --settings .runsettings
```

## Files Modified

| File | Change | Status |
|------|--------|--------|
| `.runsettings` | Removed key, added placeholder | ✅ CLEANED |
| `appsettings.Development.json` | Removed key | ✅ CLEANED |
| `.env` | Removed key, added warning | ✅ CLEANED |
| `.gitignore` | Added `.runsettings` and `appsettings.Development.json` | ✅ UPDATED |
| Documentation | Created this incident report | ✅ CREATED |

## Monitoring Going Forward

### Weekly Security Checks
- [ ] Review commits for any secrets
- [ ] Scan code for hardcoded API keys
- [ ] Check .gitignore effectiveness
- [ ] Verify no test files contain secrets

### Automated Scanning Tools
Consider implementing:
- **GitGuardian** - Scans for exposed secrets
- **TruffleHog** - Git secret scanning
- **OWASP Secret Scanner** - Pre-commit hooks
- **GitHub Security** - Built-in secret scanning (if using GitHub)

### Pre-commit Hook
```bash
# Install pre-commit
pip install pre-commit

# Add to .pre-commit-config.yaml
repos:
  - repo: https://github.com/Yelp/detect-secrets
    rev: v1.4.0
    hooks:
      - id: detect-secrets
        args: ['--baseline', '.secrets.baseline']
```

## Recovery Checklist

- ✅ Exposed key identified
- ✅ Exposed key revoked (by OpenRouter)
- ✅ Key removed from all files
- ✅ .gitignore updated
- ⏳ New API key to be created (User Action Required)
- ⏳ Local configuration updated with new key (User Action Required)
- ⏳ Force push to clean git history (if repo owner can do this)
- ⏳ Verify LLM tests pass with new key (User Action Required)

## References

- **OpenRouter Security**: https://openrouter.ai/security
- **GitHub Secret Scanning**: https://docs.github.com/en/code-security/secret-scanning
- **OWASP Secrets**: https://owasp.org/www-project-api-security/
- **.gitignore Best Practices**: https://git-scm.com/docs/gitignore

## Contact

- **OpenRouter Support**: support@openrouter.ai
- **Repository Security**: [Admin Contact]

---

**Last Updated**: 2026-01-05  
**Status**: ✅ RESOLVED (awaiting new key configuration)  
**Next Review**: 2026-01-12 (weekly security check)
