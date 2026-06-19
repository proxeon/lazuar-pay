#!/bin/bash

LHDN_CLIENT_ID="f832d052-174d-4a40-8391-f4500b0e9d44"
LHDN_CLIENT_SECRET="c5701fef-8016-4c62-b88a-28cda9246c5b"
LHDN_SUPPLIER_TIN="IG56848407100"
LHDN_SUPPLIER_ID_TYPE="NRIC"
LHDN_SUPPLIER_ID_VALUE="990806086487"

LAZUAR_API="http://localhost:8080/api/v1"
TIMESTAMP=$(date +%s)
EMAIL="sysadmin@lazuars.io"
PASSWORD="Password123!"
TENANT_SLUG="test-org-${TIMESTAMP}"

echo "========================================="
echo " 0. Provisioning Isolated Test Tenant..."
echo "========================================="

USER_ID=$(docker exec -i lazuar-db psql -U postgres -d lazuar_mvp -t -c "SELECT \"Id\" FROM one.\"GlobalUsers\" WHERE \"Email\" = '$EMAIL';" | xargs)

docker exec -i lazuar-db psql -U postgres -d lazuar_mvp <<EOF > /dev/null
DO \$\$
DECLARE
    v_org_id uuid := gen_random_uuid();
BEGIN
    INSERT INTO one."Organizations" ("Id", "Name", "Slug", "IsActive", "CreatedAt", "UpdatedAt")
    VALUES (v_org_id, 'Test Org $TIMESTAMP', '$TENANT_SLUG', true, NOW(), NOW());

    INSERT INTO one."TenantMemberships" ("Id", "GlobalUserId", "OrganizationId", "Role", "CreatedAt")
    VALUES (gen_random_uuid(), '$USER_ID', v_org_id, 'ADMIN', NOW());

    INSERT INTO lhdn."TenantConfigs" (
        "Id", "OrganizationId", "IntermediaryMode", "SupplierTin", "IdType", 
        "IdValue", "Environment", "MsicCode", "MyInvoisClientId", "MyInvoisClientSecret", 
        "CreatedAt", "UpdatedAt"
    )
    VALUES (
        gen_random_uuid(), v_org_id, false, '$LHDN_SUPPLIER_TIN', '$LHDN_SUPPLIER_ID_TYPE', 
        '$LHDN_SUPPLIER_ID_VALUE', 'SANDBOX', '62010', '$LHDN_CLIENT_ID', '$LHDN_CLIENT_SECRET', 
        NOW(), NOW()
    );
END \$\$;
EOF

echo "✅ Created Workspace: $TENANT_SLUG"

LAZUAR_TOKEN=$(curl -s -X POST "$LAZUAR_API/one/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\": \"$EMAIL\", \"password\": \"$PASSWORD\"}" \
  -c cookies.txt | jq -r '.user.email')

echo "✅ Authenticated as: $LAZUAR_TOKEN"

echo "TENANT_SLUG=$TENANT_SLUG" > .env.test
echo "LAZUAR_API=$LAZUAR_API" >> .env.test
echo "Saved session state to .env.test"
echo ""
