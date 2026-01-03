#!/bin/bash

# ============================================================================
# Docker Compose Up Script for Metrics Simple (Bash/Linux/macOS)
# Builds, starts containers, and performs health checks
# ============================================================================

set -e

# Configuration
REBUILD=false
NO_WAIT=false
HEALTH_CHECK_TIMEOUT=60
SHOW_LOGS=false
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# ============================================================================
# Parse command line arguments
# ============================================================================
while [[ $# -gt 0 ]]; do
    case $1 in
        -Rebuild|--rebuild)
            REBUILD=true
            shift
            ;;
        -NoWait|--no-wait)
            NO_WAIT=true
            shift
            ;;
        --timeout)
            HEALTH_CHECK_TIMEOUT="$2"
            shift 2
            ;;
        -Logs|--logs)
            SHOW_LOGS=true
            shift
            ;;
        -h|--help)
            echo "Usage: $0 [options]"
            echo ""
            echo "Options:"
            echo "  -Rebuild, --rebuild          Rebuild containers without cache"
            echo "  -NoWait, --no-wait           Skip health checks"
            echo "  --timeout SECONDS            Health check timeout (default: 60)"
            echo "  -Logs, --logs                Show live logs after startup"
            echo "  -h, --help                   Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

# ============================================================================
# Helper functions
# ============================================================================
print_section() {
    echo ""
    printf "${YELLOW}[%s] %s${NC}\n" "$1" "$2"
}

success() {
    printf "${GREEN}✓ %s${NC}\n" "$1"
}

error() {
    printf "${RED}✗ %s${NC}\n" "$1"
}

info() {
    printf "  ${CYAN}• %s${NC}\n" "$1"
}

# ============================================================================
# 1. Check Docker is installed
# ============================================================================
echo ""
echo "═══════════════════════════════════════════════════════════════════════════════"
printf "${CYAN}Metrics Simple - Docker Compose Startup${NC}\n"
echo "═══════════════════════════════════════════════════════════════════════════════"

print_section "1/5" "Checking Docker Desktop..."

if ! command -v docker &> /dev/null; then
    error "Docker is not installed"
    echo "Please install Docker Desktop and try again"
    exit 1
fi

DOCKER_VERSION=$(docker version --format "{{.Server.Version}}" 2>/dev/null || echo "")
if [ -z "$DOCKER_VERSION" ]; then
    error "Docker is not running or not responding"
    exit 1
fi

success "Docker is running (v$DOCKER_VERSION)"

# ============================================================================
# 2. Check Docker Compose
# ============================================================================
print_section "2/5" "Checking Docker Compose..."

if ! command -v docker &> /dev/null; then
    error "docker compose command not found"
    exit 1
fi

COMPOSE_VERSION=$(docker compose version --format "{{.Version}}" 2>/dev/null || echo "")
if [ -z "$COMPOSE_VERSION" ]; then
    error "Docker Compose is not available"
    exit 1
fi

success "Docker Compose is available (v$COMPOSE_VERSION)"

# ============================================================================
# 3. Check Dockerfiles
# ============================================================================
print_section "3/5" "Checking Dockerfiles..."

if [ ! -f "$PROJECT_ROOT/src/Api/Dockerfile" ]; then
    error "API Dockerfile not found at $PROJECT_ROOT/src/Api/Dockerfile"
    exit 1
fi
success "API Dockerfile found"

if [ ! -f "$PROJECT_ROOT/src/Runner/Dockerfile" ]; then
    error "Runner Dockerfile not found at $PROJECT_ROOT/src/Runner/Dockerfile"
    exit 1
fi
success "Runner Dockerfile found"

# ============================================================================
# 4. Build and Start Containers
# ============================================================================
print_section "4/5" "Building and starting containers..."

cd "$PROJECT_ROOT"

if [ "$REBUILD" = true ]; then
    printf "${CYAN}Building containers (--rebuild flag enabled)...${NC}\n"
    docker compose build --no-cache || {
        error "docker compose build failed"
        exit 1
    }
    success "Containers built successfully"
else
    printf "${CYAN}Starting containers (use -Rebuild to rebuild)...${NC}\n"
    docker compose build 2>&1 | grep -E "DONE|Starting" || true
    if [ ${PIPESTATUS[0]} -ne 0 ]; then
        error "docker compose build failed"
        exit 1
    fi
    success "Containers ready"
fi

printf "${CYAN}Starting services...${NC}\n"
docker compose up -d || {
    error "docker compose up failed"
    exit 1
}
success "Containers started"

# ============================================================================
# 5. Health Check
# ============================================================================
print_section "5/5" "Performing health checks..."

if [ "$NO_WAIT" = true ]; then
    printf "${YELLOW}⊙ Skipping health checks (--no-wait flag enabled)${NC}\n"
    echo ""
    echo "═══════════════════════════════════════════════════════════════════════════════"
    success "Containers started successfully!"
    echo "═══════════════════════════════════════════════════════════════════════════════"
    echo ""
    info "API available at: http://localhost:8080"
    info "Swagger UI: http://localhost:8080/swagger"
    echo ""
    exit 0
fi

API_HEALTHY=false
SQLITE_READY=false
START_TIME=$(date +%s)

while [ $(($(date +%s) - START_TIME)) -lt $HEALTH_CHECK_TIMEOUT ]; do
    ELAPSED=$(($(date +%s) - START_TIME))
    REMAINING=$((HEALTH_CHECK_TIMEOUT - ELAPSED))

    # Check API health
    if [ "$API_HEALTHY" = false ]; then
        if curl -sf http://localhost:8080/api/health &> /dev/null; then
            success "API is healthy (port 8080)"
            API_HEALTHY=true
        else
            printf "\r  ${YELLOW}${ELAPSED}s - Waiting for API...${NC}"
        fi
    fi

    # Check SQLite container
    if [ "$SQLITE_READY" = false ]; then
        if docker ps --filter "name=sqlite" --format "{{.Status}}" 2>/dev/null | grep -q "Up"; then
            success "SQLite database is ready"
            SQLITE_READY=true
        fi
    fi

    # Both services ready?
    if [ "$API_HEALTHY" = true ] && [ "$SQLITE_READY" = true ]; then
        break
    fi

    sleep 0.5
done

echo ""
echo ""
echo "═══════════════════════════════════════════════════════════════════════════════"

if [ "$API_HEALTHY" = true ] && [ "$SQLITE_READY" = true ]; then
    success "All services are running and healthy!"
    echo "═══════════════════════════════════════════════════════════════════════════════"
    echo ""
    printf "${CYAN}API Status:${NC}\n"
    info "API: http://localhost:8080"
    info "Swagger UI: http://localhost:8080/swagger"
    info "Health Check: http://localhost:8080/api/health"
    echo ""

    if [ "$SHOW_LOGS" = true ]; then
        printf "${YELLOW}Showing live logs (Ctrl+C to stop):${NC}\n"
        echo ""
        docker compose logs -f
    else
        printf "${CYAN}Useful commands:${NC}\n"
        info "View logs: docker compose logs -f"
        info "View specific service: docker compose logs -f csharp-api"
        info "Stop containers: docker compose down"
        echo ""
    fi
else
    error "Health check failed!"
    echo "═══════════════════════════════════════════════════════════════════════════════"
    echo ""
    printf "${YELLOW}Troubleshooting:${NC}\n"
    info "Check logs: docker compose logs"
    info "Stop containers: docker compose down"
    info "Rebuild: $0 -Rebuild"
    echo ""
    printf "${YELLOW}Recent logs:${NC}\n"
    docker compose logs --tail 20
    echo ""
    exit 1
fi
