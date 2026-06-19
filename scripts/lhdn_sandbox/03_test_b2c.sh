#!/bin/bash
source .env.test

TIMESTAMP=$(date +%s)
CURRENT_UTC=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
# Mac vs Linux date compat for 30 days ago
START_DATE=$(date -v-30d -u +"%Y-%m-%dT%H:%M:%SZ" 2>/dev/null || date -d "30 days ago" -u +"%Y-%m-%dT%H:%M:%SZ")
INTERNAL_ID="B2C-$TIMESTAMP"

echo "========================================="
echo " 3. Submitting B2C Consolidated Invoice"
echo "========================================="

# Uses the generic LHDN B2C TIN to ensure the DocumentStrategyFactory routes this to the Consolidated Invoice strategy.
PAYLOAD=$(cat <<EOF
{
  "internal_id": "$INTERNAL_ID",
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

curl -s -X POST "$LAZUAR_API/lhdn/documents" -b cookies.txt -H "X-Tenant-Slug: $TENANT_SLUG" -H "Content-Type: application/json" -d "$PAYLOAD" > /dev/null

for i in {1..40}; do
    STATUS_RES=$(curl -s -X GET "$LAZUAR_API/lhdn/documents/$INTERNAL_ID" -b cookies.txt -H "X-Tenant-Slug: $TENANT_SLUG")
    STATUS=$(echo "$STATUS_RES" | jq -r '.status')
    
    if [ "$STATUS" == "VALID" ]; then
        echo "✅ B2C Consolidated Invoice VALIDATED. UUID: $(echo "$STATUS_RES" | jq -r '.lhdn_uuid')"
        exit 0
    elif [ "$STATUS" == "INVALID" ] || [ "$STATUS" == "FAILED" ]; then
        echo "❌ B2C Validation Failed: $(echo "$STATUS_RES" | jq -r '.error_message')"
        exit 1
    fi
    sleep 3
done
echo "❌ Timeout waiting for B2C validation."
exit 1
