#!/bin/bash

# =========================================================================
# LHDN SANDBOX CREDENTIALS (FROM YOUR PORTAL)
# =========================================================================
LHDN_CLIENT_ID="f832d052-174d-4a40-8391-f4500b0e9d44"
LHDN_CLIENT_SECRET="c5701fef-8016-4c62-b88a-28cda9246c5b"
LHDN_SUPPLIER_TIN="IG56848407100"
LHDN_SUPPLIER_ID_TYPE="NRIC"
LHDN_SUPPLIER_ID_VALUE="990806086487"
# =========================================================================

# Configuration
LAZUAR_API="http://localhost:8080/api/v1"
TIMESTAMP=$(date +%s)
EMAIL="sysadmin@lazuars.io"
PASSWORD="Password123!"
TENANT_SLUG="test-org-${TIMESTAMP}"

echo "========================================="
echo " 0. Provisioning Isolated Test Tenant..."
echo "========================================="

# Fetch the exact User ID for the sysadmin
USER_ID=$(docker exec -i lazuar-db psql -U postgres -d lazuar_mvp -t -c "SELECT \"Id\" FROM one.\"GlobalUsers\" WHERE \"Email\" = '$EMAIL';" | xargs)

if [ -z "$USER_ID" ]; then
    echo "❌ Sysadmin user not found. Ensure your .NET backend is running."
    exit 1
fi

# We explicitly inject MyInvoisClientId, Secret, TIN, and NRIC into the new TenantConfig
docker exec -i lazuar-db psql -U postgres -d lazuar_mvp <<EOF > /dev/null
DO \$\$
DECLARE
    v_org_id uuid := gen_random_uuid();
BEGIN
    INSERT INTO one."Organizations" ("Id", "Name", "Slug", "IsActive", "CreatedAt", "UpdatedAt")
    VALUES (v_org_id, 'Test Org $TIMESTAMP', '$TENANT_SLUG', true, NOW(), NOW());

    INSERT INTO one."TenantMemberships" ("Id", "GlobalUserId", "OrganizationId", "Role", "CreatedAt")
    VALUES (gen_random_uuid(), '$USER_ID', v_org_id, 'ADMIN', NOW());

    -- Seed the extended LHDN Configuration with real Sandbox API Keys and Identity
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
echo "✅ Granted access to: $EMAIL"
echo "✅ Seeded LHDN Tenant Configuration with NRIC Profile & API Keys."
echo ""

echo "========================================="
echo " 1. Authenticating with Lazuar API..."
echo "========================================="

LAZUAR_TOKEN=$(curl -s -X POST "$LAZUAR_API/one/auth/login" \
  -H "Content-Type: application/json" \
  -d "{\"email\": \"$EMAIL\", \"password\": \"$PASSWORD\"}" \
  -c cookies.txt | jq -r '.user.email')

if [ "$LAZUAR_TOKEN" == "null" ] || [ -z "$LAZUAR_TOKEN" ]; then
    echo "❌ Failed to authenticate with Lazuar API. Check credentials."
    exit 1
fi

echo "✅ Authenticated as: $LAZUAR_TOKEN"
echo ""

echo "========================================="
echo " 2. Submitting Invoice to Lazuar LHDN Module..."
echo "========================================="

INTERNAL_INV_ID="INV-$TIMESTAMP"
CURRENT_UTC=$(date -u +"%Y-%m-%dT%H:%M:%SZ")

PAYLOAD=$(cat <<EOF
{
  "internal_id": "$INTERNAL_INV_ID",
  "document_type": "01",
  "issue_date": "$CURRENT_UTC",
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

for i in {1..20}; do
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
