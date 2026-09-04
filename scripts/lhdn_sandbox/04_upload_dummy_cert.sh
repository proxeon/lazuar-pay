#!/bin/bash
source .env.test

if [ ! -f "cookies.txt" ]; then
    echo "❌ Missing cookies.txt. Run 00_provision.sh first."
    exit 1
fi

echo "========================================="
echo " 4. Generating & Uploading Dummy Cert"
echo "========================================="

# 1. Generate OpenSSL Dummy Cert (Self-Signed)
echo "🔑 Generating self-signed dummy.p12..."
openssl genrsa -out dummy.key 2048 2>/dev/null
openssl req -new -key dummy.key -out dummy.csr -subj "/C=MY/O=Lazuar Test Org/CN=Lazuar Dummy Cert" 2>/dev/null
openssl x509 -req -days 365 -in dummy.csr -signkey dummy.key -out dummy.crt 2>/dev/null
P12_PASSWORD="${P12_PASSWORD:?set P12_PASSWORD}"
openssl pkcs12 -export -out dummy.p12 -inkey dummy.key -in dummy.crt -password "pass:$P12_PASSWORD" 2>/dev/null

# 2. Base64 encode (cross-platform compatible: Linux & macOS)
CERT_BASE64=$(base64 < dummy.p12 | tr -d '\n' | tr -d '\r')

# 3. Get OrganizationId from the database using TENANT_SLUG
echo "🔍 Fetching OrganizationId for $TENANT_SLUG..."
ORG_ID=$(docker exec -i lazuar-db psql -U postgres -d lazuar_mvp -t -c "SELECT \"Id\" FROM one.\"Organizations\" WHERE \"Slug\" = '$TENANT_SLUG';" | xargs)

if [ -z "$ORG_ID" ]; then
    echo "❌ Failed to retrieve OrganizationId."
    exit 1
fi

echo "✅ OrganizationId: $ORG_ID"

# 4. Upload to Lazuar API via Phase 2 Endpoint
echo "☁️  Uploading Certificate to API..."

PAYLOAD=$(cat <<EOF
{
  "p12_base64_file": "$CERT_BASE64",
  "passphrase": "LhdnTest123!"
}
EOF
)

UPLOAD_RES=$(curl -s -w "\nHTTP_STATUS:%{http_code}" -X PUT "$LAZUAR_API/lhdn/workspaces/$ORG_ID/lhdn-certificate" \
  -b cookies.txt \
  -H "Content-Type: application/json" \
  -d "$PAYLOAD")

HTTP_STATUS=$(echo "$UPLOAD_RES" | grep "HTTP_STATUS" | cut -d':' -f2)
BODY=$(echo "$UPLOAD_RES" | sed -e 's/HTTP_STATUS\:.*//g')

if [ "$HTTP_STATUS" == "200" ]; then
    echo "✅ Certificate successfully uploaded and encrypted at rest!"
else
    echo "❌ Upload Failed (HTTP $HTTP_STATUS): $BODY"
    exit 1
fi

# 5. Cleanup
rm dummy.key dummy.csr dummy.crt dummy.p12
echo "🧹 Cleaned up local dummy files."
