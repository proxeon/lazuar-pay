#!/usr/bin/env bash
# Runs ON the hub VPS after configs are synced.
# Usage:
#   VERSION=sha-abc1234 /root/lazuar-hub-remote-deploy.sh
set -euo pipefail

DIR="${DIR:-/root/lazuar-hub-prod}"
HEALTH_TIMEOUT="${HEALTH_TIMEOUT:-180}"
VERSION="${VERSION:-}"

log() { echo "▶ $*"; }
die() { echo "❌ $*" >&2; exit 1; }

set_version() {
  local ver="${VERSION:-}"
  [ -z "$ver" ] && { log "VERSION unset — using .env / latest"; return 0; }
  if [[ "$ver" =~ ^[0-9a-fA-F]{7,40}$ ]]; then
    ver="sha-$(echo "$ver" | tr 'A-F' 'a-f' | cut -c1-7)"
  elif [[ "$ver" =~ ^sha-([0-9a-fA-F]{7,40})$ ]]; then
    ver="sha-$(echo "${BASH_REMATCH[1]}" | tr 'A-F' 'a-f' | cut -c1-7)"
  fi
  local envf="${DIR}/.env"
  mkdir -p "$DIR"
  if [ -f "$envf" ]; then
    if grep -q '^VERSION=' "$envf"; then
      sed -i "s|^VERSION=.*|VERSION=${ver}|" "$envf"
    else
      printf '\nVERSION=%s\n' "$ver" >> "$envf"
    fi
  else
    printf 'VERSION=%s\n' "$ver" > "$envf"
  fi
  chmod 600 "$envf"
  log "pinned VERSION=${ver}"
}

wait_healthy() {
  local name="$1" timeout="${2:-$HEALTH_TIMEOUT}" i status has health
  log "waiting healthy: ${name} (${timeout}s)"
  for i in $(seq 1 "$timeout"); do
    if ! docker inspect "$name" >/dev/null 2>&1; then sleep 1; continue; fi
    status=$(docker inspect -f '{{.State.Status}}' "$name" 2>/dev/null || echo missing)
    has=$(docker inspect -f '{{if .State.Health}}yes{{else}}no{{end}}' "$name" 2>/dev/null || echo no)
    if [ "$status" = "exited" ] || [ "$status" = "dead" ]; then
      docker logs --tail 50 "$name" 2>&1 || true
      die "${name} exited"
    fi
    if [ "$has" = "yes" ]; then
      health=$(docker inspect -f '{{.State.Health.Status}}' "$name")
      [ "$health" = "healthy" ] && { log "✓ ${name} healthy"; return 0; }
    else
      [ "$status" = "running" ] && { log "✓ ${name} running"; return 0; }
    fi
    sleep 1
  done
  docker logs --tail 80 "$name" 2>&1 || true
  die "${name} not healthy within ${timeout}s"
}

cd "$DIR"
[ -f docker-compose.yml ] || die "missing ${DIR}/docker-compose.yml"
[ -f .env ] || die "missing ${DIR}/.env — create from env.example or inject HUB_ENV_FILE"

set_version
# Compose interpolates ${VERSION} from project .env automatically
EFFECTIVE_VERSION=$(grep -E '^VERSION=' .env 2>/dev/null | head -1 | cut -d= -f2- || echo "${VERSION:-latest}")
log "compose pull (VERSION=${EFFECTIVE_VERSION})"
docker compose pull
docker compose up -d --remove-orphans

wait_healthy hub-api 180
wait_healthy hub-ops 60
wait_healthy hub-portal 90
wait_healthy hub-superadmin 60
wait_healthy hub-caddy 60

if docker compose exec -T caddy caddy reload --config /etc/caddy/Caddyfile 2>/dev/null; then
  log "Caddy reloaded"
fi

log "smoke (local Host header)"
curl -fsS -o /dev/null -w "health %{http_code}\n" -H "Host: hub.lazuar.com" http://127.0.0.1/health || true
# /health is on API — through caddy:
code=$(curl -sS -o /dev/null -w "%{http_code}" -m 15 -H "Host: hub.lazuar.com" http://127.0.0.1/health || echo 000)
echo "http /health → $code"
code=$(curl -sS -o /dev/null -w "%{http_code}" -m 15 -H "Host: hub.lazuar.com" http://127.0.0.1/ || echo 000)
echo "http / → $code"
code=$(curl -sS -o /dev/null -w "%{http_code}" -m 15 -H "Host: hub.lazuar.com" http://127.0.0.1/portal || echo 000)
echo "http /portal → $code"

docker ps --format 'table {{.Names}}\t{{.Status}}\t{{.Image}}'
log "done VERSION=${VERSION:-latest}"
