#!/bin/bash

# =========================================================================
# LHDN SANDBOX CREDENTIALS
# =========================================================================
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
CURRENT_UTC=$(date -u +"%Y-%m-%dT%H:%M:%SZ")

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
echo ""

# =========================================================================
# TEST 1: B2B STANDARD INVOICE (01)
# =========================================================================
echo "========================================="
echo " TEST 1: Submitting B2B Standard Invoice"
echo "========================================="
B2B_INTERNAL_ID="INV-B2B-$TIMESTAMP"

B2B_PAYLOAD=$(cat <<EOF
{
  "internal_id": "$B2B_INTERNAL_ID",
  "document_type": "01",
  "issue_date": "$CURRENT_UTC",
  "buyer_name": "Corporate Client Sdn Bhd",
  "buyer_tin": "C1234567890",
  "buyer_id_type": "BRN",
  "buyer_id_value": "202001012345",
  "buyer_address": {
    "line1": "Level 1, Menara Test",
    "city": "Kuala Lumpur",
    "postal_code": "50000",
    "state_code": "14",
    "country_code": "MYS"
  },
  "items": [{
    "description": "Software Subscription",
    "classification_code": "022",
    "quantity": 1,
    "unit_price": 5000.0,
    "tax_rate": 0,
    "tax_amount": 0,
    "subtotal": 5000.0,
    "tax_type_code": "06"
  }],
  "total_excluding_tax": 5000.0,
  "total_tax": 0.0,
  "total_including_tax": 5000.0
}
EOF
)

curl -s -X POST "$LAZUAR_API/lhdn/documents" -b cookies.txt -H "X-Tenant-Slug: $TENANT_SLUG" -H "Content-Type: application/json" -d "$B2B_PAYLOAD" > /dev/null

ORIGINAL_UUID=""
for i in {1..20}; do
    STATUS_RES=$(curl -s -X GET "$LAZUAR_API/lhdn/documents/$B2B_INTERNAL_ID" -b cookies.txt -H "X-Tenant-Slug: $TENANT_SLUG")
    STATUS=$(echo "$STATUS_RES" | jq -r '.status')
    
    if [ "$STATUS" == "VALID" ]; then
        ORIGINAL_UUID=$(echo "$STATUS_RES" | jq -r '.lhdn_uuid')
        echo "✅ B2B Invoice VALIDATED. UUID: $ORIGINAL_UUID"
        break
    elif [ "$STATUS" == "INVALID" ] || [ "$STATUS" == "FAILED" ]; then
        echo "❌ B2B Validation Failed: $(echo "$STATUS_RES" | jq -r '.error_message')"
        exit 1
    fi
    sleep 3
done

if [ -z "$ORIGINAL_UUID" ]; then
    echo "❌ Timeout waiting for B2B validation."
    exit 1
fi

echo ""

# =========================================================================
# TEST 2: CREDIT NOTE (02)
# =========================================================================
echo "========================================="
echo " TEST 2: Submitting Credit Note for B2B Invoice"
echo "========================================="
CN_INTERNAL_ID="CN-$TIMESTAMP"

CN_PAYLOAD=$(cat <<EOF
{
  "internal_id": "$CN_INTERNAL_ID",
  "document_type": "02",
  "issue_date": "$CURRENT_UTC",
  "original_lhdn_uuid": "$ORIGINAL_UUID",
  "adjustment_reason": "Refund for unused service period",
  "buyer_name": "Corporate Client Sdn Bhd",
  "buyer_tin": "C1234567890",
  "buyer_id_type": "BRN",
  "buyer_id_value": "202001012345",
  "buyer_address": {
    "line1": "Level 1, Menara Test",
    "city": "Kuala Lumpur",
    "postal_code": "50000",
    "state_code": "14",
    "country_code": "MYS"
  },
  "items": [{
    "description": "Refund Adjustment",
    "classification_code": "022",
    "quantity": 1,
    "unit_price": 1000.0,
    "tax_rate": 0,
    "tax_amount": 0,
    "subtotal": 1000.0,
    "tax_type_code": "06"
  }],
  "total_excluding_tax": 1000.0,
  "total_tax": 0.0,
  "total_including_tax": 1000.0
}
EOF
)

curl -s -X POST "$LAZUAR_API/lhdn/documents" -b cookies.txt -H "X-Tenant-Slug: $TENANT_SLUG" -H "Content-Type: application/json" -d "$CN_PAYLOAD" > /dev/null

for i in {1..20}; do
    STATUS_RES=$(curl -s -X GET "$LAZUAR_API/lhdn/documents/$CN_INTERNAL_ID" -b cookies.txt -H "X-Tenant-Slug: $TENANT_SLUG")
    STATUS=$(echo "$STATUS_RES" | jq -r '.status')
    
    if [ "$STATUS" == "VALID" ]; then
        echo "✅ Credit Note VALIDATED. UUID: $(echo "$STATUS_RES" | jq -r '.lhdn_uuid')"
        break
    elif [ "$STATUS" == "INVALID" ] || [ "$STATUS" == "FAILED" ]; then
        echo "❌ Credit Note Validation Failed: $(echo "$STATUS_RES" | jq -r '.error_message')"
        exit 1
    fi
    sleep 3
done

echo ""

# =========================================================================
# TEST 3: B2C CONSOLIDATED INVOICE (01)
# =========================================================================
echo "========================================="
echo " TEST 3: Submitting B2C Consolidated Invoice"
echo "========================================="
B2C_INTERNAL_ID="B2C-$TIMESTAMP"
START_DATE=$(date -v-30d -u +"%Y-%m-%dT%H:%M:%SZ" 2>/dev/null || date -d "30 days ago" -u +"%Y-%m-%dT%H:%M:%SZ")

B2C_PAYLOAD=$(cat <<EOF
{
  "internal_id": "$B2C_INTERNAL_ID",
  "document_type": "01",
  "issue_date": "$CURRENT_UTC",
  "billing_period_start": "$START_DATE",
  "billing_period_end": "$CURRENT_UTC",
  "buyer_name": "General Public",
  "buyer_tin": "EI00000000010",
  "buyer_id_type": "BRN",
  "buyer_id_value": "NA",
  "buyer_address": {
    "line1": "NA",
    "city": "NA",
    "postal_code": "00000",
    "state_code": "17",
    "country_code": "MYS"
  },
  "items": [{
    "description": "Consolidated Receipts",
    "classification_code": "022",
    "quantity": 1,
    "unit_price": 3000.0,
    "tax_rate": 0,
    "tax_amount": 0,
    "subtotal": 3000.0,
    "tax_type_code": "06"
  }],
  "total_excluding_tax": 3000.0,
  "total_tax": 0.0,
  "total_including_tax": 3000.0
}
EOF
)

curl -s -X POST "$LAZUAR_API/lhdn/documents" -b cookies.txt -H "X-Tenant-Slug: $TENANT_SLUG" -H "Content-Type: application/json" -d "$B2C_PAYLOAD" > /dev/null

for i in {1..20}; do
    STATUS_RES=$(curl -s -X GET "$LAZUAR_API/lhdn/documents/$B2C_INTERNAL_ID" -b cookies.txt -H "X-Tenant-Slug: $TENANT_SLUG")
    STATUS=$(echo "$STATUS_RES" | jq -r '.status')
    
    if [ "$STATUS" == "VALID" ]; then
        echo "✅ B2C Consolidated Invoice VALIDATED. UUID: $(echo "$STATUS_RES" | jq -r '.lhdn_uuid')"
        echo "🎉 ALL TESTS PASSED SUCCESSFULLY!"
        exit 0
    elif [ "$STATUS" == "INVALID" ] || [ "$STATUS" == "FAILED" ]; then
        echo "❌ B2C Validation Failed: $(echo "$STATUS_RES" | jq -r '.error_message')"
        exit 1
    fi
    sleep 3
done

echo "⚠️ Timeout waiting for B2C validation."
exit 1
