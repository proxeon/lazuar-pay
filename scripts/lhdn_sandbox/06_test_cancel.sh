#!/bin/bash
source .env.test

TIMESTAMP=$(date +%s)
CURRENT_UTC=$(date -u +"%Y-%m-%dT%H:%M:%SZ")
INTERNAL_ID="INV-CANCEL-$TIMESTAMP"

echo "========================================="
echo " 6. Submitting Invoice for Cancellation"
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
    "description": "Invoice to be cancelled",
    "classification_code": "022",
    "quantity": 1,
    "unit_price": 500.0,
    "tax_rate": 0,
    "tax_amount": 0,
    "subtotal": 500.0,
    "tax_type_code": "06"
  }],
  "total_excluding_tax": 500.0,
  "total_tax": 0.0,
  "total_including_tax": 500.0
}
EOF
)

curl -s -X POST "$LAZUAR_API/lhdn/documents" -b cookies.txt -H "X-Tenant-Slug: $TENANT_SLUG" -H "Content-Type: application/json" -d "$PAYLOAD" > /dev/null

echo "⏳ Waiting for LHDN Validation before cancellation..."
VALIDATED=false

for i in {1..40}; do
    STATUS_RES=$(curl -s -X GET "$LAZUAR_API/lhdn/documents/$INTERNAL_ID" -b cookies.txt -H "X-Tenant-Slug: $TENANT_SLUG")
    STATUS=$(echo "$STATUS_RES" | jq -r '.status')
    
    if [ "$STATUS" == "VALID" ]; then
        VALIDATED=true
        LHDN_UUID=$(echo "$STATUS_RES" | jq -r '.lhdn_uuid')
        echo "✅ Invoice VALIDATED. UUID: $LHDN_UUID"
        break
    elif [ "$STATUS" == "INVALID" ] || [ "$STATUS" == "FAILED" ]; then
        echo "❌ Validation Failed: $(echo "$STATUS_RES" | jq -r '.error_message')"
        exit 1
    fi
    sleep 3
done

if [ "$VALIDATED" = false ]; then
    echo "❌ Timeout waiting for validation."
    exit 1
fi

echo "🚫 Executing 72-Hour Cancellation Request..."
CANCEL_PAYLOAD=$(cat <<EOF
{
  "reason": "Testing 72-hour cancellation window integration"
}
EOF
)

CANCEL_RES=$(curl -s -w "\nHTTP_STATUS:%{http_code}" -X POST "$LAZUAR_API/lhdn/documents/$INTERNAL_ID/cancel" \
  -b cookies.txt \
  -H "X-Tenant-Slug: $TENANT_SLUG" \
  -H "Content-Type: application/json" \
  -d "$CANCEL_PAYLOAD")

HTTP_STATUS=$(echo "$CANCEL_RES" | grep "HTTP_STATUS" | cut -d':' -f2)

if [ "$HTTP_STATUS" == "200" ]; then
    echo "✅ Document successfully cancelled at LHDN!"
    exit 0
else
    BODY=$(echo "$CANCEL_RES" | sed -e 's/HTTP_STATUS\:.*//g')
    echo "❌ Cancellation Failed (HTTP $HTTP_STATUS): $BODY"
    exit 1
fi
