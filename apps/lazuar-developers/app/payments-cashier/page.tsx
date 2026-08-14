import Link from "next/link";
import { Callout, CodeBlock, GuideSection, HubShell } from "../components/HubShell";

export const metadata = {
  title: "Payments cashier quickstart — Lazuar Developer Hub",
  description:
    "Provision a Hub workspace, create an M2M checkout, and verify payment webhooks without Aura code",
};

const PROVISION_BODY = `{
  "external_product": "demo-app",
  "external_org_id": "tenant-001",
  "display_name": "Demo App Tenant 001",
  "is_test_mode": true,
  "webhook_url": "https://your-app.example/webhooks/hub/payments"
}`;

const CHECKOUT_BODY = `{
  "amount": 25.0,
  "currency": "MYR",
  "description": "Demo order 42",
  "customer_email": "guest@example.com",
  "success_url": "https://your-app.example/pay/success",
  "cancel_url": "https://your-app.example/pay/cancel",
  "metadata": { "order_id": "ord_42", "type": "demo_order" }
}`;

export default function PaymentsCashierPage() {
  return (
    <HubShell
      title="Quickstart: payments cashier"
      description="Integrate any server app with Hub BYOK checkouts and signed payment webhooks. No Aura monorepo required."
    >
      <GuideSection title="0. Choose the right product">
        <ul className="list-disc pl-5 space-y-2 text-[13px]">
          <li>
            <strong className="text-[#09090b]">Payments (this guide)</strong> — ad-hoc amount + metadata →
            gateway → <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">payment.*</code> webhooks.
          </li>
          <li>
            <strong className="text-[#09090b]">Commerce</strong> — Hub product catalog, public buy links,
            subscription lifecycle events. Different product.
          </li>
          <li>
            <strong className="text-[#09090b]">LHDN</strong> — e-invoice only.{" "}
            <Link href="/quickstart" className="underline">
              LHDN quickstart →
            </Link>
          </li>
        </ul>
        <Callout>
          Email-provider setup may gate Commerce product activation. It does{" "}
          <strong className="text-[#09090b]">not</strong> block M2M Payments checkouts.
        </Callout>
      </GuideSection>

      <GuideSection title="1. Prerequisites">
        <ol className="list-decimal pl-5 space-y-2">
          <li>Hub API base URL and provision secret (or SUPER_ADMIN JWT)</li>
          <li>Active BYOK gateway on the workspace (Ops → Payment settings)</li>
          <li>Public URL for your webhook receiver</li>
          <li>
            API key with{" "}
            <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">payments.checkouts:write</code>{" "}
            (bootstrap key from provision includes this)
          </li>
        </ol>
      </GuideSection>

      <GuideSection title="2. Provision workspace">
        <p className="mb-3 text-[13px] text-[#71717a]">
          Prefer <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">external_product</code> +{" "}
          <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">external_org_id</code>.
          Product is required on that body (400{" "}
          <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">external_product_required</code> if
          omitted). Legacy Aura clients may send only{" "}
          <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">aura_org_id</code> (GUID); that path
          still maps to product <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">aura</code>.
        </p>
        <CodeBlock>{`curl -sS -X POST "$HUB/one/integrations/workspaces/provision" \\
  -H "Content-Type: application/json" \\
  -H "X-Lazuar-Provision-Key: $PROVISION_SECRET" \\
  -d '${PROVISION_BODY}'`}</CodeBlock>
        <Callout>
          Store <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">api_key.plain_key</code> and{" "}
          <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">webhook.secret_key</code> once —
          re-provision is idempotent and will not re-show secrets.
        </Callout>
      </GuideSection>

      <GuideSection title="3. Create checkout">
        <CodeBlock>{`curl -sS -X POST "$HUB/integrations/payments/checkouts" \\
  -H "Authorization: Bearer $SK_TEST_KEY" \\
  -H "Content-Type: application/json" \\
  -H "Idempotency-Key: demo-order-42" \\
  -d '${CHECKOUT_BODY}'`}</CodeBlock>
        <p className="mt-3 text-[13px] text-[#71717a]">
          Redirect the guest to <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">checkout_url</code>.
          Never treat browser return alone as paid.
        </p>
      </GuideSection>

      <GuideSection title="4. Verify payment webhooks">
        <p className="mb-3 text-[13px]">
          Events:{" "}
          <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">payment.completed</code>,{" "}
          <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">payment.failed</code>. Header{" "}
          <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">X-Lazuar-Signature: t=…,v1=…</code> —
          HMAC-SHA256 of <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">{"{t}.{rawBody}"}</code>{" "}
          with your <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">whsec_…</code>.
        </p>
        <p className="text-[13px] text-[#71717a]">
          Full algorithm, metadata guidance, and versioning:{" "}
          <span className="font-mono text-[12px]">docs/payments-integration-quickstart.md</span> in the Hub
          repo. Event catalog:{" "}
          <Link href="/webhooks" className="underline">
            /webhooks
          </Link>
          . OpenAPI:{" "}
          <Link href="/payments" className="underline">
            /payments
          </Link>
          .
        </p>
      </GuideSection>

      <GuideSection title="5. Second-app proof">
        <p className="text-[13px] text-[#71717a]">
          Prove multi-app independence with the Second-app checklist in lazuar-docs (
          <span className="font-mono text-[12px]">integrations/second-app-checklist</span>
          ). The former curl harness{" "}
          <span className="font-mono text-[12px]">script/second-app-proof.md</span> was removed;
          use the curls in this page and{" "}
          <span className="font-mono text-[12px]">docs/payments-integration-quickstart.md</span>.
        </p>
      </GuideSection>
    </HubShell>
  );
}
