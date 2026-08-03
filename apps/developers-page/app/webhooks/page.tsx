import Link from "next/link";
import { Callout, CodeBlock, GuideSection, HubShell } from "../components/HubShell";

export const metadata = {
  title: "Event catalog — Lazuar Developer Hub",
  description: "Outbound webhook events and X-Lazuar-Signature verification",
};

const COMMERCE_EVENTS = [
  {
    type: "subscription.activated",
    label: "Subscription activated",
    hint: "New paid subscription unlocked",
  },
  {
    type: "subscription.resumed",
    label: "Subscription resumed",
    hint: "Recovered from past due / suspend",
  },
  {
    type: "subscription.suspended",
    label: "Subscription suspended",
    hint: "Dunning final suspend action",
  },
  {
    type: "subscription.canceled",
    label: "Subscription canceled",
    hint: "Cancel or dunning cancel",
  },
  {
    type: "subscription.past_due",
    label: "Subscription past due",
    hint: "Renewal failed; collection in progress",
  },
  {
    type: "order.completed",
    label: "Order completed",
    hint: "One-time purchase settled",
  },
  {
    type: "payment_link.paid",
    label: "Payment link paid",
    hint: "Custom payment link settled",
  },
] as const;

const LHDN_EVENTS = [
  {
    type: "invoice.valid",
    label: "Invoice valid",
    hint: "MyInvois accepted document (status VALID)",
  },
  {
    type: "invoice.invalid",
    label: "Invoice invalid",
    hint: "MyInvois rejected document",
  },
  {
    type: "invoice.cancelled",
    label: "Invoice cancelled",
    hint: "Document cancelled after validation",
  },
  {
    type: "invoice.submitted",
    label: "Invoice submitted",
    hint: "Accepted for async processing (when emitted)",
  },
] as const;

export default function WebhooksCatalogPage() {
  return (
    <HubShell
      title="Event catalog"
      description="Outbound webhooks notify your systems when commerce lifecycle or e-invoice status changes. Configure endpoints in Ops → Developer → Webhooks."
    >
      <GuideSection title="Delivery model">
        <ul className="list-disc pl-5 space-y-2">
          <li>
            Workspace multi-endpoint fan-out: every active endpoint that accepts the event receives
            a delivery (no silent drop on product URL mismatch).
          </li>
          <li>
            Empty event filter on an endpoint means <strong>all</strong> catalog events for that
            product path.
          </li>
          <li>
            Headers on workspace (Commerce) deliveries:
            <ul className="list-disc pl-5 mt-2 space-y-1">
              <li>
                <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">X-Lazuar-Signature</code>{" "}
                — <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">t=…,v1=…</code>
              </li>
              <li>
                <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">X-Lazuar-Event</code>
              </li>
              <li>
                <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">X-Lazuar-Delivery-Id</code>
              </li>
              <li>
                <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">X-Lazuar-Webhook-Id</code>
              </li>
            </ul>
          </li>
          <li>
            Signing secret is shown once as{" "}
            <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">whsec_…</code> when you create
            the endpoint.
          </li>
        </ul>
      </GuideSection>

      <GuideSection title="Commerce / workspace events">
        <div className="border border-[#e5e5e5] bg-white overflow-hidden">
          <table className="w-full text-left text-[13px]">
            <thead className="bg-[#f4f4f5] text-[10px] font-bold uppercase tracking-widest text-[#71717a]">
              <tr>
                <th className="px-4 py-2">Event type</th>
                <th className="px-4 py-2">Description</th>
              </tr>
            </thead>
            <tbody>
              {COMMERCE_EVENTS.map((e) => (
                <tr key={e.type} className="border-t border-[#e5e5e5]">
                  <td className="px-4 py-3 font-mono text-[12px] text-[#09090b]">{e.type}</td>
                  <td className="px-4 py-3 text-[#71717a]">
                    <span className="text-[#09090b] font-medium">{e.label}</span>
                    <span className="block text-[12px] mt-0.5">{e.hint}</span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </GuideSection>

      <GuideSection title="LHDN document events">
        <p>
          LHDN product webhooks (register via{" "}
          <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">POST /lhdn/webhooks</code> or
          console when available) emit JSON with{" "}
          <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">event</code> derived from
          document status:
        </p>
        <div className="border border-[#e5e5e5] bg-white overflow-hidden">
          <table className="w-full text-left text-[13px]">
            <thead className="bg-[#f4f4f5] text-[10px] font-bold uppercase tracking-widest text-[#71717a]">
              <tr>
                <th className="px-4 py-2">Event</th>
                <th className="px-4 py-2">Description</th>
              </tr>
            </thead>
            <tbody>
              {LHDN_EVENTS.map((e) => (
                <tr key={e.type} className="border-t border-[#e5e5e5]">
                  <td className="px-4 py-3 font-mono text-[12px] text-[#09090b]">{e.type}</td>
                  <td className="px-4 py-3 text-[#71717a]">
                    <span className="text-[#09090b] font-medium">{e.label}</span>
                    <span className="block text-[12px] mt-0.5">{e.hint}</span>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <Callout>
          LHDN path currently signs with HMAC-SHA256 hex of the raw body in{" "}
          <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">X-Lazuar-Signature</code>{" "}
          (body-only). Workspace Commerce deliveries use the Standard Webhooks–style{" "}
          <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">t=,v1=</code> format below.
          Prefer the workspace catalog for commerce unlock/revoke.
        </Callout>
      </GuideSection>

      <GuideSection title="Verify X-Lazuar-Signature (workspace)">
        <p>
          Signed payload is{" "}
          <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">{"{timestamp}.{rawBody}"}</code>.
          Header format:{" "}
          <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">
            {"t={unix},v1={hmac_sha256_hex}"}
          </code>
          .
        </p>
        <CodeBlock>{`// Node.js — workspace / Commerce deliveries
import crypto from "node:crypto";

function verifyLazuarSignature(secret, rawBody, header, toleranceSec = 300) {
  // header: t=1700000000,v1=abcdef...
  const parts = Object.fromEntries(
    header.split(",").map((p) => {
      const [k, v] = p.split("=");
      return [k.trim(), v];
    })
  );
  const t = Number(parts.t);
  const v1 = parts.v1;
  if (!t || !v1) return false;
  if (Math.abs(Date.now() / 1000 - t) > toleranceSec) return false;

  const expected = crypto
    .createHmac("sha256", secret)
    .update(\`\${t}.\${rawBody}\`, "utf8")
    .digest("hex");

  return crypto.timingSafeEqual(Buffer.from(expected), Buffer.from(v1));
}`}</CodeBlock>
        <CodeBlock>{`// C# — workspace / Commerce deliveries
using System.Security.Cryptography;
using System.Text;

static bool Verify(string secret, string rawBody, string header, long nowUnix, int toleranceSec = 300)
{
    var map = header.Split(',').Select(p => p.Split('=', 2))
        .ToDictionary(p => p[0].Trim(), p => p[1]);
    if (!map.TryGetValue("t", out var tStr) || !map.TryGetValue("v1", out var v1)) return false;
    if (!long.TryParse(tStr, out var t)) return false;
    if (Math.Abs(nowUnix - t) > toleranceSec) return false;

    var signed = $"{t}.{rawBody}";
    var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signed));
    var expected = Convert.ToHexString(hash).ToLowerInvariant();
    return CryptographicOperations.FixedTimeEquals(
        Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(v1));
}`}</CodeBlock>
      </GuideSection>

      <GuideSection title="Next steps">
        <div className="flex flex-wrap gap-3 text-[12px] font-bold tracking-widest uppercase">
          <Link href="/auth" className="border border-[#e5e5e5] bg-white px-4 py-2 hover:border-[#09090b]">
            Authentication →
          </Link>
          <Link href="/quickstart" className="border border-[#09090b] bg-white px-4 py-2 hover:bg-[#09090b] hover:text-white">
            Quickstart →
          </Link>
          <Link href="/commerce" className="border border-[#e5e5e5] bg-white px-4 py-2 hover:border-[#09090b]">
            Commerce reference →
          </Link>
        </div>
      </GuideSection>
    </HubShell>
  );
}
