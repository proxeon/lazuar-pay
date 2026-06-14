#!/bin/bash

# Configuration
LAZUAR_API="http://localhost:8080/api/v1"
TENANT_SLUG="lazuar-hq" # The default seeded tenant from your genesis script
EMAIL="sysadmin@lazuars.io"
PASSWORD="admin" # Assuming this is the password for your dev seed

echo "========================================="
echo " 1. Authenticating with Lazuar API..."
echo "========================================="

# Login and extract the JWT token using jq
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

INTERNAL_INV_ID="INV-$(date +%s)"
CURRENT_UTC=$(date -u +"%Y-%m-%dT%H:%M:%SZ")

# JSON Payload matching your TypeSpec SubmitDocumentRequestDto
PAYLOAD=$(cat <<EOF
{
  "internal_id": "$INTERNAL_INV_ID",
  "document_type": "01",
  "issue_date": "$CURRENT_UTC",
  "buyer_name": "Hebat Group",
  "buyer_tin": "C2584563200",
  "buyer_id_type": "BRN",
  "buyer_id_value": "201901234567",
  "buyer_address": {
    "line1": "Lot 66, Bangunan Merdeka",
    "city": "Kuala Lumpur",
    "postal_code": "50480",
    "state_code": "14",
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
      "subtotal": 1000.0
    }
  ],
  "total_excluding_tax": 1000.0,
  "total_tax": 0.0,
  "total_including_tax": 1000.0
}
EOF
)

# Submit to Lazuar API (Using cookie for auth, X-Tenant-Slug for tenant resolution)
SUBMIT_RES=$(curl -s -X POST "$LAZUAR_API/lhdn/documents" \
  -b cookies.txt \
  -H "X-Tenant-Slug: $TENANT_SLUG" \
  -H "Content-Type: application/json" \
  -d "$PAYLOAD")

echo "$SUBMIT_RES" | jq
echo "✅ Invoice queued in Lazuar DB as PENDING."
echo ""

echo "========================================="
echo " 3. Polling Lazuar API for LHDN Validation..."
echo "========================================="

# Poll the Lazuar backend every 3 seconds to check if the background workers have finished
for i in {1..15}; do
    echo "⏳ Check $i: Fetching status for $INTERNAL_INV_ID..."
    
    STATUS_RES=$(curl -s -X GET "$LAZUAR_API/lhdn/documents/$INTERNAL_INV_ID" \
      -b cookies.txt \
      -H "X-Tenant-Slug: $TENANT_SLUG")
      
    STATUS=$(echo "$STATUS_RES" | jq -r '.status')
    
    if [ "$STATUS" == "VALID" ]; then
        echo "✅ SUCCESS! Document validated by LHDN."
        echo "LHDN UUID: $(echo "$STATUS_RES" | jq -r '.lhdn_uuid')"
        echo "QR Link: $(echo "$STATUS_RES" | jq -r '.qr_link')"
        exit 0
    elif [ "$STATUS" == "INVALID" ] || [ "$STATUS" == "FAILED" ]; then
        echo "❌ FAILED! LHDN rejected the document or gateway error occurred."
        echo "Error: $(echo "$STATUS_RES" | jq -r '.error_message')"
        exit 1
    fi
    
    sleep 3
done

echo "⚠️ Timeout waiting for background workers to process the document."
