#!/usr/bin/env bash
# scripts/seed-dev-tenant.sh

# Resolve directory path
DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
ROOT_DIR="$DIR/.."

# Load local environment variables from root .env if present
if [ -f "$ROOT_DIR/.env" ]; then
  # Read .env, sanitize line endings, and export variables
  export $(grep -v '^#' "$ROOT_DIR/.env" | sed 's/\r$//' | xargs)
fi

# Fallback values from environment, default to standard development config
DB_CONTAINER=${DB_CONTAINER:-"lazuar-db"}
DB_USER=${DB_USER:-"postgres"}
DB_NAME=${DB_NAME:-"lazuar_mvp"}

echo "Seeding development tenant 'lazuar-hq' into container '${DB_CONTAINER}'..."

# Run the seeding block
docker exec -i "$DB_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" <<EOF
-- 1. Seed the main Tenant schema
INSERT INTO tenant."Organizations" ("Id", "Name", "Slug", "IsActive", "CreatedAt")
VALUES ('7d97963c-063c-4598-86cc-9ddd9d47d9b1', 'Lazuar HQ', 'lazuar-hq', true, NOW())
ON CONFLICT ("Id") DO NOTHING;

-- 2. Seed the Messaging schema replica
INSERT INTO messaging."TenantReplicas" ("Id", "Name", "Slug", "IsActive")
VALUES ('7d97963c-063c-4598-86cc-9ddd9d47d9b1', 'Lazuar HQ', 'lazuar-hq', true)
ON CONFLICT ("Id") DO NOTHING;
EOF

echo "Dev Seeding Complete."
