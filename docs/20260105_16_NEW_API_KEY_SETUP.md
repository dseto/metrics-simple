# 🔐 Getting and Configuring New OpenRouter API Key

**After Security Incident of 2026-01-05**

## Step 1: Create New API Key at OpenRouter

1. Go to https://openrouter.ai/keys
2. Sign in with your OpenRouter account
3. Click "**Create New Key**"
4. Give it a name: `metrics-simple-dev` (or similar)
5. Copy the key (starts with `*********-...`)
6. **Keep it safe** - you'll only see it once!

## Step 2: Configure Locally

### Option A: Environment Variable (RECOMMENDED)

Set in your PowerShell terminal:
```powershell
$env:METRICS_OPENROUTER_API_KEY = "*"
```

Then run tests:
```powershell
dotnet test --settings .runsettings
```

### Option B: .runsettings File (Alternative)

Edit `.runsettings`:
```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <RunConfiguration>
    <EnvironmentVariables>
      <METRICS_OPENROUTER_API_KEY>*</METRICS_OPENROUTER_API_KEY>
      <ASPNETCORE_ENVIRONMENT>Development</ASPNETCORE_ENVIRONMENT>
      <DOTNET_ENVIRONMENT>Development</DOTNET_ENVIRONMENT>
    </EnvironmentVariables>
  </RunConfiguration>
</RunSettings>
```

**IMPORTANT**: Do NOT commit `.runsettings` if it contains a real key!

### Option C: .env File (Alternative)

Edit `.env` (already in .gitignore):
```bash
METRICS_OPENROUTER_API_KEY=*
```

## Step 3: Verify Configuration

```powershell
# Check if environment variable is set
Write-Host "Config Status: $env:METRICS_OPENROUTER_API_KEY"

# Run LLM tests to verify API works
dotnet test --filter "IT05" --settings .runsettings
```

Expected output:
```
Aprovado!  ÔÇô Com falha:     0, Aprovado:     4, Ignorado:     0
Total:     4, Dura├º├úo: ~2m - IT05 LLM Tests
```

## Step 4: Secure Configuration

### .gitignore is Already Configured
These files are already ignored:
- ✅ `.env`
- ✅ `.runsettings`
- ✅ `src/Api/appsettings.Development.json`

### Verify Files Won't Be Committed
```bash
git status

# Should show:
# On branch main
# nothing to commit, working tree clean
#
# If you see .runsettings, .env, or appsettings.Development.json:
# - Stop immediately
# - Remove the secrets
# - Ensure .gitignore is correct
```

## CI/CD Setup (GitHub Actions)

If using GitHub Actions, add API key as Secret:

1. Go to GitHub repo Settings → Secrets and variables → Actions
2. Click "New repository secret"
3. Name: `OPENROUTER_API_KEY`
4. Value: Your actual key from OpenRouter
5. Click "Add secret"

Then in `.github/workflows/test.yml`:
```yaml
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '10.0.x'
      
      - name: Run Tests
        env:
          METRICS_OPENROUTER_API_KEY: ${{ secrets.OPENROUTER_API_KEY }}
        run: dotnet test --settings .runsettings
```

## Troubleshooting

### "Real LLM tests require API key" (Tests Still Skipped)

**Problem**: Tests are still being skipped  
**Solution**: Environment variable not being read

1. Verify variable is set:
   ```powershell
   $env:METRICS_OPENROUTER_API_KEY | Should -Not -BeNullOrEmpty
   ```

2. Try explicit variable setting:
   ```powershell
   # Close and reopen terminal
   # Terminal inherits environment from parent process
   ```

3. Use .runsettings instead:
   ```powershell
   dotnet test --settings .runsettings
   ```

### "401 Unauthorized" from OpenRouter API

**Problem**: API calls are failing with 401  
**Solution**: Invalid or wrong key

1. Verify key format:
   - Should start with: `*********-`
   - Should be ~80 characters long

2. Check key is active:
   - Go to https://openrouter.ai/keys
   - Verify key shows as "Active"

3. Try with fresh key:
   - Revoke old key
   - Create new key
   - Update configuration

### "502 Bad Gateway" from LLM Tests

**Problem**: Occasional test failure in IT05-03  
**Solution**: Normal for LLM tests (they can be flaky)

The test is designed to accept:
- ✅ 200 OK - LLM generated valid DSL
- ✅ 502 Bad Gateway - LLM generated invalid DSL (acceptable)

Just re-run tests if a single LLM test fails.

## Monitoring Key Usage

Check OpenRouter dashboard for:
- API calls being made
- Usage costs
- Rate limits
- Error logs

## Revoking Old Key

If you need to revoke the previous key:

1. Go to https://openrouter.ai/keys
2. Find `metrics-simple` key (or whatever it's named)
3. Click "Revoke"
4. Confirm revocation
5. API calls using that key will immediately fail

## Security Reminders

- ✅ Environment variables are safest
- ✅ .runsettings and .env for local use only
- ✅ Never commit secrets to git
- ✅ Use GitHub Secrets for CI/CD
- ✅ Rotate keys every 3-6 months
- ✅ Immediately revoke if exposed

## Questions?

- **OpenRouter API Issues**: support@openrouter.ai
- **Metrics Simple Issues**: [Project Lead]
- **Security Concerns**: [Security Team]

---

**Created**: 2026-01-05  
**Updated**: After API Key Exposure Incident
