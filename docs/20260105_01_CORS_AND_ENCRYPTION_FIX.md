# CORS and Token Encryption Fix — 2026-01-05

## Problem Statement

### Issue 1: HTTP 500 on Connector Creation
When attempting to create a connector via `POST /api/v1/connectors`, the API returned HTTP 500 with error:
```
System.InvalidOperationException: METRICS_SECRET_KEY environment variable not configured. 
Required for token encryption. Set to a 32-byte base64 key.
```

**Root Cause**: The `TokenEncryptionService` requires a `METRICS_SECRET_KEY` environment variable for AES-256-GCM encryption of connector API tokens. This variable was not configured in the `.env` file or container environment.

### Issue 2: CORS Blocking Frontend Requests
The frontend (`http://localhost:4200`) was receiving CORS policy failures:
```
Access to XMLHttpRequest at 'http://localhost:8080/api/v1/connectors' from origin 'http://localhost:4200' 
has been blocked by CORS policy: No 'Access-Control-Allow-Origin' header is present on the requested resource.
```

Additionally, the logs showed the API itself (`http://localhost:8080`) was not whitelisted in `AllowedOrigins`.

## Solution

### 1. Generate and Configure `METRICS_SECRET_KEY`

**File Modified**: `.env`

- Generated a secure 32-byte base64 key for AES-256-GCM encryption
- Added to `.env`:
  ```
  # Token Encryption Key (required for connector API token encryption)
  # 32-byte base64 encoded key for AES-256-GCM
  METRICS_SECRET_KEY=OAOpJLUztC0jfxFmz7/myR7zME6Vdfhvyfky1p6i0mM=
  ```

- Set in local Windows user environment variables for immediate availability

### 2. Expand CORS AllowedOrigins

**Files Modified**: 
- `src/Api/appsettings.json`
- `src/Api/appsettings.Development.json`

**Changes**:
- Added `http://localhost:8080` (HTTP — used in development)
- Added `https://localhost:8080` (HTTPS — future-proofing)

**Updated configuration**:
```json
"AllowedOrigins": [
  "http://localhost:4200",    // UI (Angular/TypeScript)
  "https://localhost:4200",   // UI (HTTPS)
  "http://localhost:8080",    // API itself (development/swagger)
  "https://localhost:8080"    // API (HTTPS)
]
```

(Development also includes `http://localhost:3000` for alternative frontend ports)

## Validation

### Build Status
✅ `dotnet build` passes with no critical errors

### Runtime Test
✅ Created connector successfully:
```bash
POST /api/v1/connectors
Authorization: Bearer <token>
Content-Type: application/json

{
  "id": "hgbrasil",
  "name": "HGBrasil Weather",
  "baseUrl": "https://api.hgbrasil.com/weather",
  "authRef": "hgbrasil",
  "timeoutSeconds": 60,
  "apiToken": "f110205d"
}

Response: 201 Created (with encrypted token stored in database)
```

### API Logs
```
2026-01-05T01:47:24 [INF] POST /api/v1/connectors 201 42ms
ApiRequestCompleted: ... POST /api/v1/connectors 201 42ms
```

## Next Steps

1. **Frontend Testing**: Verify that `http://localhost:4200` can now make requests to `http://localhost:8080` without CORS errors
2. **Docker Deployment**: Container automatically uses `.env` for `METRICS_SECRET_KEY`
3. **Production**: Replace the key with a production-grade secret (never commit real keys)

## Security Notes

- The `METRICS_SECRET_KEY` is used **only** for encrypting/decrypting connector API tokens at rest
- Tokens are encrypted using AES-256-GCM (authenticated encryption)
- Never commit real keys to version control; use environment-specific `.env` files
- Docker Compose reads from `.env` via `env_file` directive

## Related Specs

- `specs/backend/06-storage/blob-and-local-storage.md` — token encryption requirements
- `specs/shared/domain/schemas/connector.schema.json` — connector contract

