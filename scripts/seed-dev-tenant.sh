#!/usr/bin/env bash
# scripts/seed-dev-tenant.sh

DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
ROOT_DIR="$DIR/.."

if [ -f "$ROOT_DIR/.env" ]; then
  export $(grep -v '^#' "$ROOT_DIR/.env" | sed 's/\r$//' | xargs)
fi

DB_CONTAINER=${DB_CONTAINER:-"lazuar-db"}
DB_USER=${DB_USER:-"postgres"}
DB_NAME=${DB_NAME:-"lazuar_mvp"}

echo "Seeding development tenant 'lazuar-hq' into container '${DB_CONTAINER}'..."

docker exec -i "$DB_CONTAINER" psql -U "$DB_USER" -d "$DB_NAME" <<EOF

INSERT INTO one."GlobalUsers" ("Id", "Email", "PasswordHash", "IsSystemAdmin", "IsActive", "CreatedAt")
VALUES 
('018f3a3f-3610-73bf-baef-c07a3c3df9ee', 'sysadmin@lazuars.io', '\$2a\$11\$0nBIfG06U2sZ8D072kE1lOQ4w3k.VzT0f8.j2N.jN4j5.PZ.nL3vC', true, true, NOW()),
('018f3a3f-3610-73bf-baef-c07a3c3df9ff', 'founder@lazuar-hq.com', '\$2a\$11\$0nBIfG06U2sZ8D072kE1lOQ4w3k.VzT0f8.j2N.jN4j5.PZ.nL3vC', false, true, NOW())
ON CONFLICT ("Email") DO NOTHING;

INSERT INTO one."Organizations" ("Id", "Name", "Slug", "IsActive", "CreatedAt")
VALUES ('7d97963c-063c-4598-86cc-9ddd9d47d9b1', 'Lazuar HQ', 'lazuar-hq', true, NOW())
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO one."TenantMemberships" ("Id", "GlobalUserId", "OrganizationId", "Role", "CreatedAt")
VALUES ('018f3a3f-3610-73bf-baef-c07a3c3df9aa', '018f3a3f-3610-73bf-baef-c07a3c3df9ff', '7d97963c-063c-4598-86cc-9ddd9d47d9b1', 'ADMIN', NOW())
ON CONFLICT ("GlobalUserId", "OrganizationId") DO NOTHING;

INSERT INTO one."TenantAppEntitlements" ("Id", "OrganizationId", "AppId", "IsActive", "CreatedAt", "UpdatedAt")
VALUES 
('018f3a3f-3610-73bf-baef-c07a3c3df9cc', '7d97963c-063c-4598-86cc-9ddd9d47d9b1', 'COMMUNITY', true, NOW(), NOW()),
('018f3a3f-3610-73bf-baef-c07a3c3df9cd', '7d97963c-063c-4598-86cc-9ddd9d47d9b1', 'OPS', true, NOW(), NOW())
ON CONFLICT ("OrganizationId", "AppId") DO NOTHING;

INSERT INTO messaging."TenantReplicas" ("Id", "Name", "Slug", "IsActive")
VALUES ('7d97963c-063c-4598-86cc-9ddd9d47d9b1', 'Lazuar HQ', 'lazuar-hq', true)
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO messaging."MessageTemplates" ("Id", "OrganizationId", "Name", "Channel", "Subject", "Body", "IsDefault", "CreatedAt", "UpdatedAt", "RequiredVariables", "OptionalVariables")
VALUES 
('018f3a3f-3610-73bf-baef-c07a3c3df901', '7d97963c-063c-4598-86cc-9ddd9d47d9b1', 'Community Welcome', 'ALL', 'Welcome to {{plan_name}}! 🎉', 'Hi {{customer_name}},\n\nWelcome to {{plan_name}}!\n\nHere is your private group link:\n{{group_link}}\n\nWeekly session link:\n{{meeting_link}}\n\nSee you there! 🙏\n\n— {{business_name}}', true, NOW(), NOW(), '["{{group_link}}"]', '["{{customer_name}}", "{{business_name}}", "{{plan_name}}", "{{meeting_link}}"]')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO messaging."MessageTemplates" ("Id", "OrganizationId", "Name", "Channel", "Subject", "Body", "IsDefault", "CreatedAt", "UpdatedAt", "RequiredVariables", "OptionalVariables")
VALUES 
('018f3a3f-3610-73bf-baef-c07a3c3df902', '7d97963c-063c-4598-86cc-9ddd9d47d9b1', 'Community Payment Success', 'ALL', 'Payment Received: {{plan_name}}', 'Hi {{customer_name}},\n\nThank you! We have successfully received your payment of RM {{total_price}} for your {{plan_name}} membership.\n\n— {{business_name}}', true, NOW(), NOW(), '["{{total_price}}"]', '["{{customer_name}}", "{{business_name}}", "{{plan_name}}"]')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO messaging."MessageTemplates" ("Id", "OrganizationId", "Name", "Channel", "Subject", "Body", "IsDefault", "CreatedAt", "UpdatedAt", "RequiredVariables", "OptionalVariables")
VALUES 
('018f3a3f-3610-73bf-baef-c07a3c3df903', '7d97963c-063c-4598-86cc-9ddd9d47d9b1', 'Community Payment Failed', 'ALL', 'Payment Failed: {{plan_name}}', 'Hi {{customer_name}},\n\nWe were unable to process your renewal payment for {{plan_name}}.\n\nPlease complete your payment to avoid losing access to the community:\n{{renewal_link}}\n\n— {{business_name}}', true, NOW(), NOW(), '["{{renewal_link}}"]', '["{{customer_name}}", "{{business_name}}", "{{plan_name}}"]')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO messaging."MessageTemplates" ("Id", "OrganizationId", "Name", "Channel", "Subject", "Body", "IsDefault", "CreatedAt", "UpdatedAt", "RequiredVariables", "OptionalVariables")
VALUES 
('018f3a3f-3610-73bf-baef-c07a3c3df904', '7d97963c-063c-4598-86cc-9ddd9d47d9b1', 'Community Renewal (3 Days)', 'ALL', 'Your {{plan_name}} subscription renews in 3 days', 'Hi {{customer_name}},\n\nYour {{plan_name}} membership is expiring in 3 days. To ensure you don''t lose access to the community and weekly sessions, please renew your subscription here:\n{{renewal_link}}\n\n— {{business_name}}', true, NOW(), NOW(), '["{{renewal_link}}"]', '["{{customer_name}}", "{{business_name}}", "{{plan_name}}"]')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO messaging."MessageTemplates" ("Id", "OrganizationId", "Name", "Channel", "Subject", "Body", "IsDefault", "CreatedAt", "UpdatedAt", "RequiredVariables", "OptionalVariables")
VALUES 
('018f3a3f-3610-73bf-baef-c07a3c3df905', '7d97963c-063c-4598-86cc-9ddd9d47d9b1', 'Community Renewal Due Today', 'ALL', 'Action Required: {{plan_name}} renewal due today', 'Hi {{customer_name}},\n\nThis is a reminder that your {{plan_name}} membership is due for renewal today. Please renew your subscription to maintain your access:\n{{renewal_link}}\n\n— {{business_name}}', true, NOW(), NOW(), '["{{renewal_link}}"]', '["{{customer_name}}", "{{business_name}}", "{{plan_name}}"]')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO messaging."MessageTemplates" ("Id", "OrganizationId", "Name", "Channel", "Subject", "Body", "IsDefault", "CreatedAt", "UpdatedAt", "RequiredVariables", "OptionalVariables")
VALUES 
('018f3a3f-3610-73bf-baef-c07a3c3df906', '7d97963c-063c-4598-86cc-9ddd9d47d9b1', 'Community Renewal Overdue', 'ALL', 'Final Notice: {{plan_name}} is overdue', 'Hi {{customer_name}},\n\nYour {{plan_name}} membership is currently past due. If not resolved, your access to the community will be suspended soon. Please renew your subscription immediately:\n{{renewal_link}}\n\n— {{business_name}}', true, NOW(), NOW(), '["{{renewal_link}}"]', '["{{customer_name}}", "{{business_name}}", "{{plan_name}}"]')
ON CONFLICT ("Id") DO NOTHING;

INSERT INTO messaging."MessageTemplates" ("Id", "OrganizationId", "Name", "Channel", "Subject", "Body", "IsDefault", "CreatedAt", "UpdatedAt", "RequiredVariables", "OptionalVariables")
VALUES 
('018f3a3f-3610-73bf-baef-c07a3c3df907', '7d97963c-063c-4598-86cc-9ddd9d47d9b1', 'Community Subscription Cancelled', 'ALL', 'Your {{plan_name}} membership has ended', 'Hi {{customer_name}},\n\nYour {{plan_name}} membership has been cancelled.\n\nYou will retain access to your resources until {{current_period_end}}. After this date, you will no longer receive weekly session links.\n\nWe hope to see you again! 🙏\n\n— {{business_name}}', true, NOW(), NOW(), '[]', '["{{customer_name}}", "{{business_name}}", "{{plan_name}}", "{{current_period_end}}"]')
ON CONFLICT ("Id") DO NOTHING;
EOF

echo "Dev Seeding Complete."
