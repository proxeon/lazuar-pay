# #!/usr/bin/env bash
# scripts/seed-dev-tenant.sh

# Resolve directory path
DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
ROOT_DIR="$DIR/.."

# Load local environment variables from root .env if present
if [ -f "$ROOT_DIR/.env" ]; then
  export $(grep -v '^#' "$ROOT_DIR/.env" | sed 's/\r$//' | xargs)
fi

DB_CONTAINER=${DB_CONTAINER:-"lazuar-db"}
DB_USER=${DB_USER:-"postgres"}
DB_NAME=${DB_NAME:-"lazuar_mvp"}

echo "Seeding development tenant 'lazuar-hq' into container '${DB_CONTAINER}'..."

docker exec -i "$DB_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" <<EOF

-- 0. Seed Global Users and Memberships
INSERT INTO one."GlobalUsers" ("Id", "Email", "PasswordHash", "IsSystemAdmin", "IsActive", "CreatedAt")
VALUES 
('018f3a3f-3610-73bf-baef-c07a3c3df9ee', 'sysadmin@lazuars.io', '\$2a\$11\$0nBIfG06U2sZ8D072kE1lOQ4w3k.VzT0f8.j2N.jN4j5.PZ.nL3vC', true, true, NOW()),
('018f3a3f-3610-73bf-baef-c07a3c3df9ff', 'founder@lazuar-hq.com', '\$2a\$11\$0nBIfG06U2sZ8D072kE1lOQ4w3k.VzT0f8.j2N.jN4j5.PZ.nL3vC', false, true, NOW())
ON CONFLICT ("Email") DO NOTHING;

-- 1. Seed the main One Organization schema
INSERT INTO one."Organizations" ("Id", "Name", "Slug", "IsActive", "CreatedAt")
VALUES ('7d97963c-063c-4598-86cc-9ddd9d47d9b1', 'Lazuar HQ', 'lazuar-hq', true, NOW())
ON CONFLICT ("Id") DO NOTHING;

-- Link the founder to the organization as an ADMIN
INSERT INTO one."TenantMemberships" ("Id", "GlobalUserId", "OrganizationId", "Role", "CreatedAt")
VALUES ('018f3a3f-3610-73bf-baef-c07a3c3df9aa', '018f3a3f-3610-73bf-baef-c07a3c3df9ff', '7d97963c-063c-4598-86cc-9ddd9d47d9b1', 'ADMIN', NOW())
ON CONFLICT ("GlobalUserId", "OrganizationId") DO NOTHING;

-- 2. Seed the Messaging schema replica
INSERT INTO messaging."TenantReplicas" ("Id", "Name", "Slug", "IsActive")
VALUES ('7d97963c-063c-4598-86cc-9ddd9d47d9b1', 'Lazuar HQ', 'lazuar-hq', true)
ON CONFLICT ("Id") DO NOTHING;

-- 3. Seed Default Message Templates
INSERT INTO messaging."MessageTemplates" ("Id", "OrganizationId", "Name", "Channel", "Subject", "Body", "IsDefault", "CreatedAt", "UpdatedAt", "RequiredVariables", "OptionalVariables")
VALUES 
('018f3a3f-3610-73bf-baef-c07a3c3df901', '7d97963c-063c-4598-86cc-9ddd9d47d9b1', 'Community Welcome', 'ALL', 'Welcome to {{plan_name}}! 🎉', 'Hi {{customer_name}},\n\nWelcome to {{plan_name}}!\n\nHere is your private group link:\n{{group_link}}\n\nWeekly session link:\n{{meeting_link}}\n\nSee you there! 🙏\n\n— {{business_name}}', true, NOW(), NOW(), '["{{group_link}}"]', '["{{customer_name}}", "{{business_name}}", "{{plan_name}}", "{{meeting_link}}"]')
ON CONFLICT ("Id") DO NOTHING;

-- (You can leave the rest of your templates here exactly as they were in the original file)
EOF

echo "Dev Seeding Complete."
