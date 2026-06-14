#!/bin/bash

# Configuration
LAZUAR_API="http://localhost:8080/api/v1"
TIMESTAMP=$(date +%s)
EMAIL="sysadmin@lazuars.io"
PASSWORD="Password123!"
TENANT_SLUG="test-org-${TIMESTAMP}"

echo "========================================="
echo " 0. Provisioning Isolated Test Tenant..."
echo "========================================="

USER_ID=$(docker exec -i lazuar-db psql -U postgres -d lazuar_mvp -t -c "SELECT \"Id\" FROM one.\"GlobalUsers\" WHERE \"Email\" = '$EMAIL';" | xargs)

if [ -z "$USER_ID" ]; then
    echo "❌ Sysadmin user not found. Ensure your .NET backend is running."
    exit 1
fi

# Updated to include SupplierTin, IdType, IdValue, Environment, and MsicCode
docker exec -i lazuar-db psql -U postgres -d lazuar_mvp <<EOF > /dev/null
DO \$\$
DECLARE
    v_org_id uuid := gen_random_uuid();
BEGIN
    INSERT INTO one."Organizations" ("Id", "Name", "Slug", "IsActive", "CreatedAt", "UpdatedAt")
    VALUES (v_org_id, 'Test Org $TIMESTAMP', '$TENANT_SLUG', true, NOW(), NOW());

    INSERT INTO one."TenantMemberships" ("Id", "GlobalUserId", "OrganizationId", "Role", "CreatedAt")
    VALUES (gen_random_uuid(), '$USER_ID', v_org_id, 'ADMIN', NOW());

    -- Seed the extended LHDN Configuration
    INSERT INTO lhdn."TenantConfigs" ("Id", "OrganizationId", "IntermediaryMode", "SupplierTin", "IdType", "IdValue", "Environment", "MsicCode", "CreatedAt", "UpdatedAt")
    VALUES (gen_random_uuid(), v_org_id, true, 'C1234567890', 'BRN', '202401234567', 'SANDBOX', '62010', NOW(), NOW());
END \$\$;
EOF

echo "✅ Created Workspace: $TENANT_SLUG"
echo "✅ Granted access to: $EMAIL"
echo "✅ Seeded extended LHDN Tenant Configuration."
echo ""

echo "========================================="
echo " 1. Authenticating with Lazuar API..."
echo "========================================="

LAZUAR_TOKEN=$(curl -s -X POST "$LAZUAR_API/one/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\": \"$EMAIL\", \"password\": \"$PASSWORD\"}" \
  -c cookies.txt | jq -r '.user.email')

echo "✅ Authenticated as: $LAZUAR_TOKEN"
echo ""

echo "========================================="
echo " 2. Submitting Invoice to Lazuar LHDN Module..."
echo "========================================="

INTERNAL_INV_ID="INV-$TIMESTAMP"
CURRENT_UTC=$(date -u +"%Y-%m-%dT%H:%M:%SZ")

# Updated JSON Payload to match new TypeSpec DTOs
PAYLOAD=$(cat <<EOF
{
  "internal_id": "$INTERNAL_INV_ID",
  "document_type": "01",
  "issue_date": "$CURRENT_UTC",
  "buyer_name": "AXXX_XXXXRI",
  "buyer_tin": "IG56848407100",
  "buyer_id_type": "NRIC",
  "buyer_id_value": "990806086487",
  "buyer_address": {
    "line1": "NO 16, HALA KLEBANG RESTU 18",
    "city": "CHEMOR",
    "postal_code": "31200",
    "state_code": "08",
    "country_code": "MYS"
  },
  "items": [
    {
      "description": "Software Development Service",
      "classification_code": "022",
      "quantity": 1,
      "unit_price": 1000.0,
      "tax_rate": 0,
      "tax_amount": 0,
      "subtotal": 1000.0,
      "tax_type_code": "06"
    }
  ],
  "total_excluding_tax": 1000.0,
  "total_tax": 0.0,
  "total_including_tax": 1000.0
}
EOF
)

SUBMIT_RES=$(curl -s -X POST "$LAZUAR_API/lhdn/documents" \
  -b cookies.txt \
  -H "X-Tenant-Slug: $TENANT_SLUG" \
  -H "Content-Type: application/json" \
  -d "$PAYLOAD")

echo "$SUBMIT_RES" | jq

if echo "$SUBMIT_RES" | grep -q '"status": 40'; then
    echo "❌ Submission rejected by Lazuar API."
    exit 1
fi

echo "✅ Invoice queued in Lazuar DB as PENDING."
echo ""

echo "========================================="
echo " 3. Polling Lazuar API for LHDN Validation..."
echo "========================================="

for i in {1..15}; do
    echo "⏳ Check $i: Fetching status for $INTERNAL_INV_ID..."
    
    STATUS_RES=$(curl -s -X GET "$LAZUAR_API/lhdn/documents/$INTERNAL_INV_ID" \
      -b cookies.txt \
      -H "X-Tenant-Slug: $TENANT_SLUG")
      
    STATUS=$(echo "$STATUS_RES" | jq -r '.status')
    
    if [ "$STATUS" == "VALID" ]; then
        echo ""
        echo "🎉 SUCCESS! Document validated by LHDN."
        echo "LHDN UUID: $(echo "$STATUS_RES" | jq -r '.lhdn_uuid')"
        echo "QR Link: $(echo "$STATUS_RES" | jq -r '.qr_link')"
        exit 0
    elif [ "$STATUS" == "INVALID" ] || [ "$STATUS" == "FAILED" ]; then
        echo ""
        echo "❌ FAILED! LHDN rejected the document or gateway error occurred."
        echo "Error: $(echo "$STATUS_RES" | jq -r '.error_message')"
        exit 1
    fi
    
    sleep 3
done

echo "⚠️ Timeout waiting for background workers to process the document."
