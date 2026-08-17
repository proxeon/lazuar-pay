import Link from "next/link";
import { Callout, CodeBlock, GuideSection, HubShell } from "../components/HubShell";

export const metadata = {
  title: "Quickstart — Lazuar Developer Hub",
  description: "Submit your first Malaysian e-invoice and verify webhook signatures",
};

const SAMPLE_BODY = `{
  "internal_id": "INV-2026-0001",
  "document_type": "01",
  "issue_date": "2026-08-04T10:00:00Z",
  "buyer_name": "Acme Sdn Bhd",
  "buyer_tin": "C12345678901",
  "buyer_id_type": "BRN",
  "buyer_id_value": "202001012345",
  "buyer_address": {
    "line1": "1 Jalan Example",
    "city": "Kuala Lumpur",
    "postal_code": "50000",
    "state_code": "14",
    "country_code": "MYS"
  },
  "items": [
    {
      "description": "Consulting services",
      "classification_code": "022",
      "quantity": 1,
      "unit_price": 100.0,
      "tax_rate": 0,
      "tax_amount": 0,
      "subtotal": 100.0,
      "tax_type_code": "06"
    }
  ],
  "total_excluding_tax": 100.0,
  "total_tax": 0,
  "total_including_tax": 100.0
}`;

export default function QuickstartPage() {
  return (
    <HubShell
      title="Quickstart: first e-invoice"
      description="Create a test API key, submit a document to the LHDN gateway, poll status, and verify outbound signatures."
    >
      <GuideSection title="1. Prerequisites">
        <ol className="list-decimal pl-5 space-y-2">
          <li>Workspace with LHDN product access in Ops console</li>
          <li>
            API key from <span className="font-mono text-[12px]">Developer → API Keys</span> (
            <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">sk_test_…</code>)
          </li>
          <li>
            Optional: webhook endpoint under{" "}
            <span className="font-mono text-[12px]">Developer → Webhooks</span> or{" "}
            <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">POST /one/workspaces/{"{id}"}/webhooks</code>
          </li>
        </ol>
        <Callout>
          Base URL (production):{" "}
          <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">
            https://hub.lazuar.com/api/v1
          </code>
          . Local:{" "}
          <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">
            http://localhost:8080/api/v1
          </code>
          . Always send{" "}
          <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">Authorization: Bearer sk_…</code>
          — never a user JWT.
        </Callout>
      </GuideSection>

      <GuideSection title="2. Install an SDK (optional)">
        <p className="text-[13px] text-[#71717a]">
          Official packages wrap the same HTTP contract. Use the monorepo packages today; published
          registry names:
        </p>
        <CodeBlock>{`# TypeScript
pnpm add @lazuar/lhdn-sdk
# or npm i @lazuar/lhdn-sdk

# .NET
dotnet add package Lazuar.Lhdn.Sdk`}</CodeBlock>
        <CodeBlock>{`// TypeScript
import { initLhdnClient } from "@lazuar/lhdn-sdk";

const client = initLhdnClient({
  apiKey: process.env.LAZUAR_API_KEY!, // sk_test_…
  baseUrl: "https://hub.lazuar.com/api/v1",
});

await client.lhdn.documents.post(
  { /* SubmitDocumentRequestDto */ },
  { headers: { "Idempotency-Key": "inv-2026-0001" } }
);`}</CodeBlock>
        <CodeBlock>{`// .NET
using Lazuar.Lhdn.Sdk;

var client = LhdnClientFactory.Create(
    apiKey: Environment.GetEnvironmentVariable("LAZUAR_API_KEY")!,
    baseUrl: "https://hub.lazuar.com/api/v1");

// Use generated LhdnClient document operations with Idempotency-Key header`}</CodeBlock>
      </GuideSection>

      <GuideSection title="3. Submit with curl">
        <p>
          Include a unique{" "}
          <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">Idempotency-Key</code> on every
          submit so retries do not create duplicate MyInvois documents.
        </p>
        <CodeBlock>{`export LAZUAR_API_KEY="sk_test_YOUR_KEY"
export BASE="https://hub.lazuar.com/api/v1"

curl -sS -X POST "$BASE/lhdn/documents" \\
  -H "Authorization: Bearer $LAZUAR_API_KEY" \\
  -H "Content-Type: application/json" \\
  -H "Idempotency-Key: inv-2026-0001" \\
  -d '${SAMPLE_BODY.replace(/'/g, "'\\''")}'`}</CodeBlock>
        <p className="text-[13px] text-[#71717a]">
          Poll status until MyInvois finishes validation:
        </p>
        <CodeBlock>{`curl -sS "$BASE/lhdn/documents/INV-2026-0001" \\
  -H "Authorization: Bearer $LAZUAR_API_KEY"`}</CodeBlock>
      </GuideSection>

      <GuideSection title="4. Verify webhook signature">
        <p>
          Workspace deliveries use Standard Webhooks–style signing. Full catalog and receivers:{" "}
          <Link href="/webhooks" className="underline font-medium text-[#09090b]">
            Event catalog
          </Link>
          .
        </p>
        <CodeBlock>{`# X-Lazuar-Signature: t=1700000000,v1=<hex>
# signed_payload = "{t}.{raw_body}"
# v1 = HMAC-SHA256(secret, signed_payload) as lowercase hex

python3 - <<'PY'
import hmac, hashlib, time

secret = b"whsec_YOUR_SECRET"
body = b'{"event_type":"subscription.activated"}'
t = int(time.time())
sig = hmac.new(secret, f"{t}.".encode() + body, hashlib.sha256).hexdigest()
print(f"t={t},v1={sig}")
PY`}</CodeBlock>
        <Callout>
          Reject deliveries older than ~5 minutes and always use a constant-time compare on the hex
          digest. See the TypeScript and C# snippets on the{" "}
          <Link href="/webhooks" className="underline font-medium text-[#09090b]">
            event catalog
          </Link>{" "}
          page.
        </Callout>
      </GuideSection>

      <GuideSection title="5. Full API reference">
        <div className="flex flex-wrap gap-3 text-[12px] font-bold tracking-widest uppercase">
          <Link href="/lhdn" className="border border-[#09090b] bg-white px-4 py-2 hover:bg-[#09090b] hover:text-white">
            LHDN OpenAPI →
          </Link>
          <Link href="/auth" className="border border-[#e5e5e5] bg-white px-4 py-2 hover:border-[#09090b]">
            Authentication →
          </Link>
          <Link href="/webhooks" className="border border-[#e5e5e5] bg-white px-4 py-2 hover:border-[#09090b]">
            Event catalog →
          </Link>
        </div>
      </GuideSection>
    </HubShell>
  );
}
