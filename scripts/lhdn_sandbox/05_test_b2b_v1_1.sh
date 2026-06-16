#!/bin/bash
source .env.test

TIMESTAMP=$(date +%s)
CURRENT_UTC=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
INTERNAL_ID="INV-B2B-V11-$TIMESTAMP"

echo "========================================="
echo " 5. Submitting v1.1 B2B Signed Invoice"
echo "========================================="

# A distinct corporate Buyer TIN/BRN is used to prevent LHDN from treating this as a self-billed importation transaction.
PAYLOAD=$(cat <<EOF
{
  "internal_id": "$INTERNAL_ID",
  "document_type": "01",
  "document_version": "1.1",
  "issue_date": "$CURRENT_UTC",
  "buyer_name": "Hebat Group",
  "buyer_tin": "C2584563200",
  "buyer_id_type": "BRN",
  "buyer_id_value": "201901234567",
  "buyer_address": {
    "line1": "Level 1, Menara Test",
    "city": "Kuala Lumpur",
    "postal_code": "50000",
    "state_code": "14",
    "country_code": "MYS"
  },
  "items": [{
    "description": "Software Subscription v1.1",
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
        LHDN_UUID=$(echo "$STATUS_RES" | jq -r '.lhdn_uuid')
        echo "✅ v1.1 B2B Invoice VALIDATED. UUID: $LHDN_UUID"
        exit 0
    elif [ "$STATUS" == "INVALID" ] || [ "$STATUS" == "FAILED" ]; then
        echo "❌ v1.1 Validation Failed: $(echo "$STATUS_RES" | jq -r '.error_message')"
        exit 1
    fi
    sleep 3
done

echo "❌ Timeout waiting for v1.1 validation."
exit 1
