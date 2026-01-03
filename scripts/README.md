# Docker Container Startup Scripts

## Overview

Two startup scripts are provided for building, starting, and health-checking Docker containers:

- **Windows/PowerShell:** `scripts/docker-up.ps1`
- **Linux/macOS/Bash:** `scripts/docker-up.sh`

Both scripts perform the same operations:
1. Verify Docker is installed and running
2. Verify Docker Compose is available
3. Check Dockerfiles exist
4. Build containers (optional rebuild)
5. Start all services via `docker compose up`
6. Perform health checks on API and database

---

## Quick Start

### Windows PowerShell

```powershell
# Simple start
.\scripts\docker-up.ps1

# Rebuild without cache
.\scripts\docker-up.ps1 -Rebuild

# Skip health checks
.\scripts\docker-up.ps1 -NoWait

# Show live logs after startup
.\scripts\docker-up.ps1 -Logs

# Combine options
.\scripts\docker-up.ps1 -Rebuild -Logs
```

### Linux/macOS Bash

```bash
# Simple start
./scripts/docker-up.sh

# Rebuild without cache
./scripts/docker-up.sh --rebuild

# Skip health checks
./scripts/docker-up.sh --no-wait

# Show live logs after startup
./scripts/docker-up.sh --logs

# Combine options
./scripts/docker-up.sh --rebuild --logs
```

---

## Script Options

### Both Platforms

| Option | Long Form | Purpose | Default |
|--------|-----------|---------|---------|
| `-Rebuild` | `--rebuild` | Rebuild containers without cache | false |
| `-NoWait` | `--no-wait` | Skip health checks | false |
| `--timeout SECONDS` | — | Health check timeout in seconds | 60 |
| `-Logs` | `--logs` | Show live container logs after startup | false |
| `-h` | `--help` | Show help message | — |

---

## What Each Script Does

### Phase 1: Check Docker Desktop (1/5)
- Verifies Docker daemon is running
- Displays Docker version
- **Fails if:** Docker not installed or not responding

### Phase 2: Check Docker Compose (2/5)
- Verifies `docker compose` command is available
- Displays Compose version
- **Fails if:** Compose not installed

### Phase 3: Check Dockerfiles (3/5)
- Verifies both Dockerfiles exist:
  - `src/Api/Dockerfile`
  - `src/Runner/Dockerfile`
- **Fails if:** Any Dockerfile missing

### Phase 4: Build & Start (4/5)
- Runs `docker compose build` (unless already built)
- Optionally rebuilds with `--no-cache` if `-Rebuild` flag used
- Runs `docker compose up -d` to start all services
- **Fails if:** Build or start fails

### Phase 5: Health Checks (5/5)
- **API Health Check:**
  - Calls `GET http://localhost:8080/api/health`
  - Repeats every 500ms until successful or timeout
  - **Success:** Returns HTTP 200

- **SQLite Health Check:**
  - Checks if `sqlite` container is running
  - **Success:** Container status is "Up"

- **Timeout:** If health checks don't pass within 60s (default), fails
- **Skippable:** Use `-NoWait` flag to skip this phase

---

## Health Check Details

### API Health Endpoint

```bash
curl http://localhost:8080/api/health
# Returns: {"status":"ok"}
```

### Docker Healthcheck (embedded in container)

The API Dockerfile includes a native Docker healthcheck:

```dockerfile
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD curl -f http://localhost:8080/api/health || exit 1
```

This allows `docker compose` to monitor container health independently.

### Multi-Service Dependencies

The health checks respect service ordering:

```yaml
# API waits for SQLite to start
csharp-api:
  depends_on:
    sqlite:
      condition: service_started

# Runner waits for API to be healthy
csharp-runner:
  depends_on:
    csharp-api:
      condition: service_healthy
```

---

## Output Examples

### Successful Startup

```
════════════════════════════════════════════════════════════════════════════════
Metrics Simple - Docker Compose Startup
════════════════════════════════════════════════════════════════════════════════

[1/5] Checking Docker Desktop...
✓ Docker is running (v26.1.0)

[2/5] Checking Docker Compose...
✓ Docker Compose is available (v2.24.6)

[3/5] Checking Dockerfiles...
✓ API Dockerfile found
✓ Runner Dockerfile found

[4/5] Building and starting containers...
Building containers (--rebuild flag enabled)...
✓ Containers built successfully
Starting services...
✓ Containers started

[5/5] Performing health checks...
✓ API is healthy (port 8080)
✓ SQLite database is ready

════════════════════════════════════════════════════════════════════════════════
All services are running and healthy! ✓
════════════════════════════════════════════════════════════════════════════════

API Status:
  • API: http://localhost:8080
  • Swagger UI: http://localhost:8080/swagger
  • Health Check: http://localhost:8080/api/health

Useful commands:
  • View logs: docker compose logs -f
  • View specific service: docker compose logs -f csharp-api
  • Stop containers: docker compose down
```

### Failed Startup

```
[5/5] Performing health checks...
✗ Health check timeout after 60s

════════════════════════════════════════════════════════════════════════════════
Health check failed!
════════════════════════════════════════════════════════════════════════════════

Troubleshooting:
  • Check logs: docker compose logs
  • Stop containers: docker compose down
  • Rebuild: .\scripts\docker-up.ps1 -Rebuild

Recent logs:
sqlite       | ...
csharp-api   | ...
```

---

## Common Issues & Solutions

### Issue: "Docker is not running"
**Solution:** Start Docker Desktop application

### Issue: "docker compose command not found"
**Solution:** Update Docker Desktop to version 2.0+ (includes Compose v2)

### Issue: "Port 8080 already in use"
**Solution:**
```bash
# Find and stop existing container
docker ps | grep 8080
docker stop <container_id>

# Or change port in compose.yaml:
# ports:
#   - "8081:8080"
```

### Issue: "Health check timeout"
**Solution:**
```bash
# Check logs for error details
docker compose logs csharp-api

# Rebuild without cache
.\scripts\docker-up.ps1 -Rebuild

# Or increase timeout
.\scripts\docker-up.ps1 --timeout 120
```

### Issue: "SQLite database errors"
**Solution:**
```bash
# Check volume mounts
docker inspect sqlite

# Verify config directory permissions
ls -la ./src/Api/config/

# Clean up and restart
docker compose down -v
.\scripts\docker-up.ps1 -Rebuild
```

---

## Manual Docker Commands

If you prefer to run commands manually instead of using the scripts:

```bash
# Build images
docker compose build

# Start services (detached)
docker compose up -d

# View logs
docker compose logs -f

# Stop services
docker compose down

# Stop and remove volumes
docker compose down -v

# Check specific service health
docker inspect sqlite --format='{{json .State.Health}}'
```

---

## Environment Configuration

Scripts respect environment variables from `.env` file:

```bash
# Copy example
cp .env.example .env

# Edit with your settings
# - METRICS_OPENROUTER_API_KEY (if AI enabled)
# - METRICS_SQLITE_PATH (custom database path)
```

---

## Notes for CI/CD

For automated deployments, use the `-NoWait` flag to avoid timeouts:

```bash
# GitHub Actions / GitLab CI
./scripts/docker-up.sh --no-wait
docker compose logs -f --tail 50
```

This allows your CI/CD pipeline to continue while monitoring logs separately.
