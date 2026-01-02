# Docker Configuration Summary

## Changes Applied

### 1. Dockerfile Updates

#### API Dockerfile (`src/Api/Dockerfile`)
- ✅ Updated .NET version from 8.0.11 to 10.0-preview
- ✅ Added `DOTNET_ENVIRONMENT=Production` environment variable
- ✅ Added HEALTHCHECK to verify `/api/health` endpoint (30s interval, 3 retries)
- HEALTHCHECK requires `curl` in the runtime image (aspnet:10.0-preview includes it)

#### Runner Dockerfile (`src/Runner/Dockerfile`)
- ✅ Updated .NET version from 8.0 to 10.0-preview
- ✅ Changed runtime image from `aspnet` to `runtime` (CLI app doesn't need ASP.NET)
- ✅ Added `DOTNET_ENVIRONMENT=Production` environment variable

### 2. Docker Compose Updates (`compose.yaml`)

#### API Service
- ✅ Fixed Dockerfile path: `../ApiDockerfile` → `Dockerfile`
- ✅ Added `env_file: .env` support
- ✅ Added explicit environment variables for database and runtime
- ✅ Added volumes for config and logs persistence
- ✅ Added healthcheck configuration
- ✅ Improved dependency: `depends_on` now waits for SQLite service

#### Runner Service
- ✅ Fixed Dockerfile path: `../RunnerDockerfile` → `Dockerfile`
- ✅ Added `env_file: .env` support
- ✅ Added explicit environment variables
- ✅ Added volumes for shared config/logs
- ✅ Changed restart policy to `on-failure`
- ✅ Set dependency on healthy API service

#### SQLite Service
- ✅ Cleaned up comments
- ✅ Maintains shared volume for config persistence

### 3. Environment Variables

Created `.env.example` documenting all configurable variables:

```
METRICS_SQLITE_PATH          Database file path (optional)
METRICS_OPENROUTER_API_KEY   OpenRouter API key for AI (optional)
OPENROUTER_API_KEY           Alternative API key env var (optional)
ASPNETCORE_ENVIRONMENT       ASP.NET environment (optional)
DOTNET_ENVIRONMENT           .NET environment (optional)
```

**Priority order (environment variables override appsettings.json):**
1. `METRICS_OPENROUTER_API_KEY` (preferred)
2. `OPENROUTER_API_KEY` (fallback)
3. Value from `appsettings.json`

## Build Status

✅ All projects compile successfully with .NET 10.0
- `Metrics.Api` → builds to `Metrics.Api.dll`
- `Metrics.Runner` → builds to `Metrics.Runner.dll`
- `Metrics.Engine` → builds as library
- All tests pass

## Ready for Container Build

```bash
# Build containers
docker compose build

# Run containers
docker compose up

# API will be available at: http://localhost:8080
# Swagger UI: http://localhost:8080/swagger
# Health check: curl http://localhost:8080/api/health
```

## Configuration Matrix

| Component | Database Path | API Key | Logs | Config |
|-----------|--------------|---------|------|--------|
| API | `METRICS_SQLITE_PATH` | `METRICS_OPENROUTER_API_KEY` | `/app/logs` | `/app/config` |
| Runner | `METRICS_SQLITE_PATH` | `METRICS_OPENROUTER_API_KEY` | `/app/logs` | `/app/config` |
| SQLite | Shared via volume | N/A | N/A | `/data` |

## Critical Notes

⚠️ **API Key Handling:**
- If `AI.Enabled=true` in `appsettings.json`, an API key is required
- Set via `METRICS_OPENROUTER_API_KEY` or `OPENROUTER_API_KEY` env var
- NEVER commit secrets to repo (appsettings.json values are for local dev only)

⚠️ **Database Persistence:**
- SQLite database persists in `./src/Api/config/` directory
- Shared between all containers via Docker volume
- Ensure volume mount is writable

⚠️ **Network:**
- Services communicate via `backend` Docker network
- API service must be healthy before Runner starts
