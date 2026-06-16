#!/bin/bash
source .env.test

TIMESTAMP=$(date +%s)
CURRENT_UTC=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
INTERNAL_ID="INV-B2B-$TIMESTAMP"

echo "========================================="
echo " 1. Submitting B2B Standard Invoice"
echo "========================================="

PAYLOAD=$(cat <<EOF
{
  "internal_id": "$INTERNAL_ID",
  "document_type": "01",
  "issue_date": "$CURRENT_UTC",
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

curl -s -X POST "$LAZUAR_API/lhdn/documents" -b cookies.txt -H "X-Tenant-Slug: $TENANT_SLUG" -H "Content-Type: application/json" -d "$PAYLOAD" > /dev/null

for i in {1..20}; do
    STATUS_RES=$(curl -s -X GET "$LAZUAR_API/lhdn/documents/$INTERNAL_ID" -b cookies.txt -H "X-Tenant-Slug: $TENANT_SLUG")
    STATUS=$(echo "$STATUS_RES" | jq -r '.status')
    
    if [ "$STATUS" == "VALID" ]; then
        ORIGINAL_UUID=$(echo "$STATUS_RES" | jq -r '.lhdn_uuid')
        echo "✅ B2B Invoice VALIDATED. UUID: $ORIGINAL_UUID"
        echo "ORIGINAL_UUID=$ORIGINAL_UUID" >> .env.test
        exit 0
    elif [ "$STATUS" == "INVALID" ] || [ "$STATUS" == "FAILED" ]; then
        echo "❌ B2B Validation Failed: $(echo "$STATUS_RES" | jq -r '.error_message')"
        exit 1
    fi
    sleep 3
done
echo "❌ Timeout waiting for B2B validation."
exit 1
