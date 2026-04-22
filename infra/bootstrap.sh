#!/usr/bin/env bash
# Stop on errors, unset vars, or failed piped commands.
set -euo pipefail

# Optional args:
#   1 = deploy directory on the server
#   2 = compose file name/path inside that directory
DEPLOY_PATH="${1:-/var/www/harpyx}"
COMPOSE_FILE="${2:-docker-compose.prod.yml}"

# Run docker compose from the directory that contains .env and the real prod compose file.
cd "$DEPLOY_PATH"

# Difference vs deploy.sh:
# this script operates on the entire compose stack, not only on webapp/worker.
# It is useful for the first bootstrap of a server or when you intentionally
# want to refresh backing services too.
docker compose -f "$COMPOSE_FILE" pull

# Recreate the full stack and remove stale orphaned containers.
# This may restart infrastructure containers as well, so it is broader and
# potentially more disruptive than deploy.sh.
docker compose -f "$COMPOSE_FILE" up -d --remove-orphans
