#!/bin/bash
source .env.test

if [ -z "$ORIGINAL_UUID" ]; then
    echo "❌ Missing ORIGINAL_UUID in .env.test. Run 01_test_b2b.sh first."
    exit 1
fi

TIMESTAMP=$(date +%s)
CURRENT_UTC=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
INTERNAL_ID="CN-$TIMESTAMP"

echo "========================================="
echo " 2. Submitting Credit Note for B2B Invoice"
echo "========================================="

PAYLOAD=$(cat <<EOF
{
  "internal_id": "$INTERNAL_ID",
  "document_type": "02",
  "issue_date": "$CURRENT_UTC",
  "original_lhdn_uuid": "$ORIGINAL_UUID",
  "adjustment_reason": "Refund for unused service period",
  "buyer_name": "AXXX_XXXXRI",
  "buyer_tin": "IG56848407100",
  "buyer_id_type": "NRIC",
  "buyer_id_value": "990806086487",
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

curl -s -X POST "$LAZUAR_API/lhdn/documents" -b cookies.txt -H "X-Tenant-Slug: $TENANT_SLUG" -H "Content-Type: application/json" -d "$PAYLOAD" > /dev/null

for i in {1..20}; do
    STATUS_RES=$(curl -s -X GET "$LAZUAR_API/lhdn/documents/$INTERNAL_ID" -b cookies.txt -H "X-Tenant-Slug: $TENANT_SLUG")
    STATUS=$(echo "$STATUS_RES" | jq -r '.status')
    
    if [ "$STATUS" == "VALID" ]; then
        echo "✅ Credit Note VALIDATED. UUID: $(echo "$STATUS_RES" | jq -r '.lhdn_uuid')"
        exit 0
    elif [ "$STATUS" == "INVALID" ] || [ "$STATUS" == "FAILED" ]; then
        echo "❌ Credit Note Validation Failed: $(echo "$STATUS_RES" | jq -r '.error_message')"
        exit 1
    fi
    sleep 3
done
echo "❌ Timeout waiting for Credit Note validation."
exit 1
