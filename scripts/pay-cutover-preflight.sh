#!/usr/bin/env bash
# Automated Phase 8 code gates. Does not swap :8081 or pause charges.
set -euo pipefail
root="$(cd "$(dirname "$0")/.." && pwd)"
app="$root/apps/lazuar-api/src/app.rs"
fail=0

need() {
  if ! grep -qF "$1" "$2"; then
    echo "missing: $1  ($2)" >&2
    fail=1
  fi
}

forbid() {
  if grep -qF "$1" "$2"; then
    echo "forbidden: $1  ($2)" >&2
    fail=1
  fi
}

need '["v1", "pay", token]' "$app"
need '["v1", "pay", token, "start"]' "$app"
need '["v1", "pay", token, "confirm"]' "$app"
forbid '["v1", "public", "pay"' "$app"

need 'VaultedRail' "$app"
need 'LiveRefunder::load' "$app"
if grep -n 'NoopRefunder' "$app" >/dev/null; then
  echo "forbidden: NoopRefunder in app.rs (refunds/webhooks must use LiveRefunder)" >&2
  fail=1
fi

need 'captured:{payment_id}' "$root/apps/lazuar-api/src/rails/razorpay_webhook.rs"

df="$root/apps/lazuar-api/Dockerfile"
need 'curl -fsS http://127.0.0.1:8081/ready' "$df"
if grep -q 'curl -fsS http://127.0.0.1:8081/health' "$df"; then
  echo "forbidden: Dockerfile HEALTHCHECK uses /health" >&2
  fail=1
fi

ex="$root/apps/lazuar-api/.env.example"
need 'ConnectionStrings__Pay' "$ex"
if grep -E '^[[:space:]]*Pay__ConnectionString=' "$ex" >/dev/null; then
  echo "forbidden: Pay__ConnectionString= in .env.example" >&2
  fail=1
fi

node "$root/scripts/check-rust-routes.mjs"
node "$root/scripts/check-pay-openapi-honesty.mjs"

if [[ "$fail" -ne 0 ]]; then
  echo "pay-cutover-preflight: FAIL" >&2
  exit 1
fi
echo "pay-cutover-preflight: ok (code gates only — human pre-flight still required)"
