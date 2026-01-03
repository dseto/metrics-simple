#!/bin/bash

# ============================================================================
# Docker Health Check Script (Bash/Linux/macOS)
# Monitors health of running containers
# ============================================================================

set +e

WATCH=false
REFRESH_INTERVAL=5

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -w|--watch)
            WATCH=true
            shift
            ;;
        --interval)
            REFRESH_INTERVAL="$2"
            shift 2
            ;;
        -h|--help)
            echo "Usage: $0 [options]"
            echo ""
            echo "Options:"
            echo "  -w, --watch              Watch mode (continuous refresh)"
            echo "  --interval SECONDS       Refresh interval in seconds (default: 5)"
            echo "  -h, --help               Show this help message"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
GRAY='\033[0;37m'
NC='\033[0m' # No Color

get_container_status() {
    docker compose ps --format "table {{.Names}}\t{{.State}}\t{{.Ports}}"
}

get_api_health() {
    if curl -sf http://localhost:8080/api/health &> /dev/null; then
        return 0
    else
        return 1
    fi
}

show_status() {
    clear
    echo "════════════════════════════════════════════════════════════════════════════════"
    printf "${CYAN}Docker Containers Health Status${NC}\n"
    echo "════════════════════════════════════════════════════════════════════════════════"
    echo ""

    printf "${YELLOW}Container Status:${NC}\n"
    echo ""
    
    if docker compose ps &> /dev/null; then
        docker compose ps
    else
        printf "${RED}No containers running${NC}\n"
        return
    fi

    echo ""
    printf "${YELLOW}API Health:${NC}\n"
    
    if get_api_health; then
        printf "${GREEN}✓ http://localhost:8080/api/health - OK (HTTP 200)${NC}\n"
        printf "${GREEN}  • Swagger UI: http://localhost:8080/swagger${NC}\n"
    else
        printf "${RED}✗ http://localhost:8080/api/health - NOT RESPONDING${NC}\n"
    fi

    echo ""
    printf "${YELLOW}Docker Healthcheck Status:${NC}\n"
    
    API_HEALTH=$(docker inspect csharp-api --format "{{json .State.Health.Status}}" 2>/dev/null | tr -d '"')
    if [ -n "$API_HEALTH" ]; then
        if [ "$API_HEALTH" = "healthy" ]; then
            printf "${GREEN}  • csharp-api: $API_HEALTH${NC}\n"
        else
            printf "${YELLOW}  • csharp-api: $API_HEALTH${NC}\n"
        fi
    fi
    
    if docker ps --filter "name=sqlite" --format "{{.State}}" 2>/dev/null | grep -q "running"; then
        printf "${GREEN}  • sqlite: running${NC}\n"
    fi

    echo ""
    printf "${GRAY}Last Update: $(date '+%H:%M:%S')${NC}\n"
    
    if [ "$WATCH" = true ]; then
        printf "${GRAY}Refreshing in $REFRESH_INTERVAL seconds (Ctrl+C to stop)...${NC}\n"
    fi
}

# Main loop
if [ "$WATCH" = true ]; then
    while true; do
        show_status
        sleep "$REFRESH_INTERVAL"
    done
else
    show_status
fi
