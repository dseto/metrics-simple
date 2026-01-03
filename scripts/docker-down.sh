#!/bin/bash

# ============================================================================
# Docker Cleanup Script (Bash/Linux/macOS)
# Stops and removes containers, optionally removes volumes
# ============================================================================

set -e

REMOVE_VOLUMES=false
CONFIRM=false

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -v|--volumes)
            REMOVE_VOLUMES=true
            shift
            ;;
        -y|--yes)
            CONFIRM=true
            shift
            ;;
        -h|--help)
            echo "Usage: $0 [options]"
            echo ""
            echo "Options:"
            echo "  -v, --volumes            Also remove volumes (including database!)"
            echo "  -y, --yes                Skip confirmation prompt"
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
NC='\033[0m' # No Color

echo "════════════════════════════════════════════════════════════════════════════════"
printf "${CYAN}Docker Cleanup${NC}\n"
echo "════════════════════════════════════════════════════════════════════════════════"
echo ""

printf "${YELLOW}The following will be removed:${NC}\n"
if [ "$REMOVE_VOLUMES" = true ]; then
    printf "${RED}  • Containers${NC}\n"
    printf "${RED}  • Networks${NC}\n"
    printf "${RED}  • Volumes (including database!)${NC}\n"
else
    printf "${YELLOW}  • Containers${NC}\n"
    printf "${YELLOW}  • Networks${NC}\n"
    printf "${GREEN}  • Data will be preserved${NC}\n"
fi

echo ""

# Confirm
if [ "$CONFIRM" = false ]; then
    read -p "Continue? (yes/no) " response
    if [ "$response" != "yes" ]; then
        printf "${YELLOW}Cancelled${NC}\n"
        exit 0
    fi
fi

echo ""
printf "${CYAN}Stopping containers...${NC}\n"

if [ "$REMOVE_VOLUMES" = true ]; then
    docker compose down -v
else
    docker compose down
fi

printf "${GREEN}✓ Cleanup complete${NC}\n"

echo ""
printf "${YELLOW}Status:${NC}\n"
docker compose ps || true

echo ""
printf "${GREEN}Done!${NC}\n"
