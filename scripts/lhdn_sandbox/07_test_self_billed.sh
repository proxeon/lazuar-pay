#!/bin/bash
source .env.test

TIMESTAMP=$(date +%s)
CURRENT_UTC=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
INTERNAL_ID="SB-INV-$TIMESTAMP"

echo "========================================="
echo " 7. Submitting Self-Billed Invoice (Type 11)"
echo "========================================="

PAYLOAD=$(cat <<EOF
{
  "internal_id": "$INTERNAL_ID",
  "document_type": "11",
  "issue_date": "$CURRENT_UTC",
  "buyer_name": "Freelance Consultant",
  "buyer_tin": "EI00000000020", 
  "buyer_id_type": "NRIC",
  "buyer_id_value": "900101145321",
  "buyer_address": {
    "line1": "123 Remote Work Ave",
    "city": "Cyberjaya",
    "postal_code": "63000",
    "state_code": "10",
    "country_code": "MYS"
  },
  "items": [{
    "description": "Consulting Services",
    "classification_code": "022",
    "quantity": 1,
    "unit_price": 1500.0,
    "tax_rate": 0,
    "tax_amount": 0,
    "subtotal": 1500.0,
    "tax_type_code": "06"
  }],
  "total_excluding_tax": 1500.0,
  "total_tax": 0.0,
  "total_including_tax": 1500.0
}
EOF
)

curl -s -X POST "$LAZUAR_API/lhdn/documents" -b cookies.txt -H "X-Tenant-Slug: $TENANT_SLUG" -H "Content-Type: application/json" -d "$PAYLOAD" > /dev/null

for i in {1..40}; do
    STATUS_RES=$(curl -s -X GET "$LAZUAR_API/lhdn/documents/$INTERNAL_ID" -b cookies.txt -H "X-Tenant-Slug: $TENANT_SLUG")
    STATUS=$(echo "$STATUS_RES" | jq -r '.status')
    
    if [ "$STATUS" == "VALID" ]; then
        SB_UUID=$(echo "$STATUS_RES" | jq -r '.lhdn_uuid')
        echo "✅ Self-Billed Invoice VALIDATED. UUID: $SB_UUID"
        exit 0
    elif [ "$STATUS" == "INVALID" ] || [ "$STATUS" == "FAILED" ]; then
        echo "❌ Self-Billed Validation Failed: $(echo "$STATUS_RES" | jq -r '.error_message')"
        exit 1
    fi
    sleep 3
done
echo "❌ Timeout waiting for Self-Billed validation."
exit 1
