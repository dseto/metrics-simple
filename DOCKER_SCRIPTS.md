# Docker Container Management - Quick Reference

## 📋 Available Scripts

All scripts are in the `scripts/` directory with both PowerShell (.ps1) and Bash (.sh) versions.

### 1. **docker-up.ps1 / docker-up.sh** — Start & Health Check
Builds, starts containers, and verifies all services are healthy.

**Windows:**
```powershell
# Basic start
.\scripts\docker-up.ps1

# With options
.\scripts\docker-up.ps1 -Rebuild -Logs

# Help
.\scripts\docker-up.ps1 -h
```

**Linux/macOS:**
```bash
# Basic start
./scripts/docker-up.sh

# With options
./scripts/docker-up.sh --rebuild --logs

# Help
./scripts/docker-up.sh --help
```

**Options:**
- `-Rebuild` / `--rebuild` — Rebuild without cache
- `-NoWait` / `--no-wait` — Skip health checks
- `-Logs` / `--logs` — Show live logs after startup
- `--timeout SECONDS` — Health check timeout (default: 60)

---

### 2. **docker-health.ps1 / docker-health.sh** — Check Container Health
Displays status of all running containers and API health.

**Windows:**
```powershell
# Single check
.\scripts\docker-health.ps1

# Watch mode (continuous refresh)
.\scripts\docker-health.ps1 -Watch

# Watch with custom interval
.\scripts\docker-health.ps1 -Watch -RefreshInterval 10
```

**Linux/macOS:**
```bash
# Single check
./scripts/docker-health.sh

# Watch mode
./scripts/docker-health.sh --watch

# Watch with custom interval
./scripts/docker-health.sh --watch --interval 10
```

**Output:**
- Container states
- API health status (http://localhost:8080/api/health)
- Docker healthcheck status

---

### 3. **docker-down.ps1 / docker-down.sh** — Stop & Cleanup
Stops containers and removes them (optionally removes volumes).

**Windows:**
```powershell
# Stop containers (keep data)
.\scripts\docker-down.ps1

# Stop and remove volumes (removes database!)
.\scripts\docker-down.ps1 -RemoveVolumes

# Skip confirmation
.\scripts\docker-down.ps1 -Confirm
```

**Linux/macOS:**
```bash
# Stop containers (keep data)
./scripts/docker-down.sh

# Stop and remove volumes (removes database!)
./scripts/docker-down.sh --volumes

# Skip confirmation
./scripts/docker-down.sh --yes
```

---

## 🚀 Common Workflows

### Fresh Start
```bash
# Windows
.\scripts\docker-up.ps1 -Rebuild -Logs

# Linux/macOS
./scripts/docker-up.sh --rebuild --logs
```

### Quick Start (already built)
```bash
# Windows
.\scripts\docker-up.ps1

# Linux/macOS
./scripts/docker-up.sh
```

### Monitor Running Containers
```bash
# Windows
.\scripts\docker-health.ps1 -Watch

# Linux/macOS
./scripts/docker-health.sh --watch
```

### Graceful Shutdown (preserves data)
```bash
# Windows
.\scripts\docker-down.ps1

# Linux/macOS
./scripts/docker-down.sh
```

### Full Reset (removes database)
```bash
# Windows
.\scripts\docker-down.ps1 -RemoveVolumes -Confirm
.\scripts\docker-up.ps1 -Rebuild

# Linux/macOS
./scripts/docker-down.sh --volumes --yes
./scripts/docker-up.sh --rebuild
```

---

## 🔍 Manual Docker Commands

If you prefer manual control:

```bash
# Build
docker compose build

# Start (detached)
docker compose up -d

# Stop
docker compose stop

# Remove containers
docker compose down

# Remove containers and volumes
docker compose down -v

# View logs (all services)
docker compose logs -f

# View logs (specific service)
docker compose logs -f csharp-api

# Check status
docker compose ps

# Execute command in container
docker compose exec csharp-api curl http://localhost:8080/api/health
```

---

## 📡 Service Endpoints

When containers are running:

| Service | Endpoint | Purpose |
|---------|----------|---------|
| **API** | http://localhost:8080 | REST API |
| **Swagger** | http://localhost:8080/swagger | API documentation & testing |
| **Health** | http://localhost:8080/api/health | Health check endpoint |
| **SQLite** | internal:3306 | Database (no external access) |

---

## 🐛 Troubleshooting

### Port Already in Use
```bash
# Find process using port 8080
netstat -ano | findstr :8080  # Windows
lsof -i :8080                  # macOS/Linux

# Stop conflicting container
docker compose down
```

### Health Check Timeout
```bash
# View detailed logs
docker compose logs csharp-api

# Rebuild everything
.\scripts\docker-up.ps1 -Rebuild

# Or with longer timeout
.\scripts\docker-up.ps1 --timeout 120
```

### Database Corruption
```bash
# Remove data and restart
.\scripts\docker-down.ps1 -RemoveVolumes -Confirm
.\scripts\docker-up.ps1 -Rebuild
```

### Container Won't Start
```bash
# Check logs
docker compose logs --tail 50

# Check container state
docker inspect csharp-api

# Full reset
docker compose down -v
docker system prune -a
.\scripts\docker-up.ps1 -Rebuild
```

---

## ⚙️ Configuration

### Environment Variables
Copy `.env.example` to `.env` and configure:

```bash
cp .env.example .env
```

Variables:
- `METRICS_OPENROUTER_API_KEY` — AI API key (if enabled)
- `METRICS_SQLITE_PATH` — Custom database path
- `DOTNET_ENVIRONMENT` — Runtime environment

### Docker Compose
Modify `compose.yaml` for:
- Port mappings (default: 8080)
- Volume mounts
- Environment variables
- Service dependencies

---

## 📊 Health Check Details

### API Health Endpoint
Returns `{"status":"ok"}` if API is running correctly.

```bash
curl http://localhost:8080/api/health
# HTTP 200 = healthy
# Any other = unhealthy
```

### Docker Native Healthcheck
Built into `src/Api/Dockerfile`:
- Interval: 30 seconds
- Timeout: 10 seconds
- Retries: 3
- Start period: 5 seconds

Check status:
```bash
docker inspect csharp-api --format "{{json .State.Health}}"
```

---

## 🔐 Security Notes

- ⚠️ **API Keys:** Never commit `.env` with real secrets to git
- ⚠️ **Database:** SQLite data is in `./src/Api/config/` — back it up!
- ⚠️ **Volumes:** Remove with `-RemoveVolumes` / `--volumes` flag only when needed
- ⚠️ **Network:** Services communicate via internal `backend` network

---

## 📝 Script Execution

### Windows PowerShell

If scripts won't execute:
```powershell
# Allow script execution for current user
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser

# Or run inline
powershell -ExecutionPolicy Bypass -File .\scripts\docker-up.ps1
```

### Linux/macOS

Make scripts executable:
```bash
chmod +x scripts/*.sh
./scripts/docker-up.sh
```

---

## 🚨 Emergency Commands

**Force stop all containers:**
```bash
docker compose kill
```

**Remove everything (containers, networks, volumes):**
```bash
docker compose down -v --remove-orphans
```

**Clear Docker cache:**
```bash
docker system prune -a --volumes
```

---

## 📚 For More Information

- Full documentation: [`DOCKER_CONFIGURATION.md`](../DOCKER_CONFIGURATION.md)
- Scripts details: [`scripts/README.md`](./README.md)
- Docker Compose reference: `compose.yaml`
- Dockerfile details: `src/Api/Dockerfile`, `src/Runner/Dockerfile`
