#!/usr/bin/env bash
# Register lazuar-pay-merchant as a public OIDC SPA via One apps API.
# Not Zitadel Console. Pay never holds ZITADEL_PAT — use Ada's access_token.
#
#   export ACCESS_TOKEN='…'   # JWT access_token, not id_token
#   export TENANT_ID='…'      # One tenant id (Pay org_id)
#   ./apps/lazuar-pay-merchant/scripts/register-spa.sh
#   WRITE_ENV=1 ./apps/lazuar-pay-merchant/scripts/register-spa.sh
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../../.." && pwd)"
APP_DIR="$ROOT/apps/lazuar-pay-merchant"
API_BASE="${ONE_API_BASE:-http://localhost:8080/api/v1}"
API_BASE="${API_BASE%/}"
WRITE_ENV="${WRITE_ENV:-0}"
NAME="${SPA_NAME:-lazuar-pay-merchant}"
REDIRECT_URI="${REDIRECT_URI:-http://localhost:5178/callback}"
POST_LOGOUT_URI="${POST_LOGOUT_URI:-http://localhost:5178/}"

if [[ -z "${ACCESS_TOKEN:-}" || -z "${TENANT_ID:-}" ]]; then
  cat >&2 <<'EOF'
error: ACCESS_TOKEN and TENANT_ID are required.

  export ACCESS_TOKEN='…'  # from :5175 / lazuar-app — access_token, never id_token
  export TENANT_ID='…'     # One tenant id
  ./apps/lazuar-pay-merchant/scripts/register-spa.sh

Do not export ZITADEL_PAT here. That is One ops, not Pay.
EOF
  exit 1
fi

command -v curl >/dev/null
command -v jq >/dev/null

body="$(jq -n \
  --arg name "$NAME" \
  --arg redir "$REDIRECT_URI" \
  --arg post "$POST_LOGOUT_URI" \
  '{name:$name, type:"spa", redirect_uris:[$redir], post_logout_redirect_uris:[$post]}')"

resp="$(curl -sS -X POST "$API_BASE/tenants/$TENANT_ID/apps" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -H "Accept: application/json" \
  -d "$body")"

echo "$resp" | jq '{id, name, type, client_id, issuer, redirect_uris, post_logout_redirect_uris, client_secret}'

client_id="$(echo "$resp" | jq -r '.client_id // empty')"
if [[ -z "$client_id" || "$client_id" == "null" ]]; then
  echo "error: no client_id in response" >&2
  exit 1
fi

secret="$(echo "$resp" | jq -r '.client_secret // empty')"
if [[ -n "$secret" && "$secret" != "null" ]]; then
  echo "error: spa create returned client_secret; expected public PKCE (type=spa)" >&2
  exit 1
fi

if [[ "$WRITE_ENV" == "1" ]]; then
  envfile="$APP_DIR/.env"
  touch "$envfile"
  if grep -qE '^[# ]*VITE_ZITADEL_CLIENT_ID=' "$envfile" 2>/dev/null; then
    tmp="$(mktemp)"
    grep -vE '^[# ]*VITE_ZITADEL_CLIENT_ID=' "$envfile" >"$tmp" || true
    mv "$tmp" "$envfile"
  fi
  printf 'VITE_ZITADEL_CLIENT_ID=%s\n' "$client_id" >>"$envfile"
  echo "wrote VITE_ZITADEL_CLIENT_ID to $envfile (gitignored)" >&2
fi
