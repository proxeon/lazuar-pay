import Link from "next/link";
import { Callout, CodeBlock, GuideSection, HubShell } from "../components/HubShell";

export const metadata = {
  title: "Authentication — Lazuar Developer Hub",
  description: "API keys, scopes, and JWT for Lazuar machine integrations",
};

const SCOPE_ROWS = [
  {
    scope: "lhdn.documents:write",
    purpose: "Submit / cancel e-invoice documents",
  },
  {
    scope: "lhdn.documents:read",
    purpose: "Document status, TIN validate",
  },
  {
    scope: "payments.checkouts:write",
    purpose: "Create M2M ad-hoc checkout sessions (any integrator cashier)",
  },
  {
    scope: "payments.checkouts:read",
    purpose: "Poll checkout status (write also implies read)",
  },
  {
    scope: "payments.config:read",
    purpose: "Optional — connection status only (no secrets)",
  },
  {
    scope: "webhooks.endpoints:manage",
    purpose: "Optional — register outbound webhook URLs via API",
  },
] as const;

export default function AuthGuidePage() {
  return (
    <HubShell
      title="Authentication"
      description="Machine clients use scoped API keys. Human sessions use JWT cookies. Never put a user JWT in an ERP, cron, or server-side integration."
    >
      <GuideSection title="Two credential types">
        <div className="grid gap-4 sm:grid-cols-2">
          <Callout>
            <p className="text-[11px] font-bold uppercase tracking-widest text-[#09090b] mb-2">
              API keys (machines)
            </p>
            <ul className="list-disc pl-4 space-y-1.5 text-[13px]">
              <li>
                Prefixes: <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">sk_test_…</code>{" "}
                and <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">sk_live_…</code>
              </li>
              <li>
                Send as{" "}
                <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">
                  Authorization: Bearer sk_…
                </code>
              </li>
              <li>Create in Ops console → Developer → API Keys</li>
              <li>Closed scope catalog (LHDN, payments, webhooks) — least privilege</li>
              <li>Secret shown once at create; store in a secrets manager</li>
              <li>Bound to one workspace (organization); never mint keys or write payment config</li>
            </ul>
          </Callout>
          <Callout>
            <p className="text-[11px] font-bold uppercase tracking-widest text-[#09090b] mb-2">
              JWT (humans only)
            </p>
            <ul className="list-disc pl-4 space-y-1.5 text-[13px]">
              <li>Issued after login for Ops / portal browsers</li>
              <li>HTTP-only cookies for first-party UIs</li>
              <li>Short-lived; bound to a user and workspace membership</li>
              <li>
                <strong className="text-[#09090b]">Do not</strong> embed JWT in ERP, mobile backends,
                or scheduled jobs
              </li>
              <li>Admin routes may also require X-Tenant-Id</li>
            </ul>
          </Callout>
        </div>
      </GuideSection>

      <GuideSection title="Scope catalog">
        <p className="mb-4">
          Platform keys store one or more scopes. Policies on integration routes require the matching
          claim. Write scopes typically imply read for the same resource family.
        </p>
        <div className="overflow-x-auto border border-[#e5e5e5]">
          <table className="w-full text-left text-[13px]">
            <thead className="bg-[#fafafa] border-b border-[#e5e5e5]">
              <tr>
                <th className="px-4 py-2 text-[10px] font-bold uppercase tracking-widest text-[#71717a]">
                  Scope
                </th>
                <th className="px-4 py-2 text-[10px] font-bold uppercase tracking-widest text-[#71717a]">
                  Purpose
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#f4f4f5]">
              {SCOPE_ROWS.map((row) => (
                <tr key={row.scope}>
                  <td className="px-4 py-2.5 font-mono text-[12px] text-[#09090b] whitespace-nowrap">
                    {row.scope}
                  </td>
                  <td className="px-4 py-2.5 text-[#3f3f46]">{row.purpose}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <Callout>
          <p className="mb-2">
            <strong className="text-[#09090b]">Never a machine scope:</strong> key mint / list
            secrets / revoke, payment-config write (BYOK secrets), superadmin, or unrelated admin
            surfaces. Those require a human OrgAdmin JWT.
          </p>
          <p>
            <strong className="text-[#09090b]">Aura integrator default:</strong>{" "}
            <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">
              payments.checkouts:write
            </code>{" "}
            +{" "}
            <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">
              payments.checkouts:read
            </code>{" "}
            only — do not grant LHDN scopes unless the integration submits e-invoices.
          </p>
        </Callout>
      </GuideSection>

      <GuideSection title="How to send the key">
        <p>
          The host middleware accepts the full secret as a Bearer token. Do not invent a custom{" "}
          <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">X-Api-Key</code> header for
          product APIs.
        </p>
        <CodeBlock>{`curl -X POST "https://hub.lazuar.com/api/v1/lhdn/documents" \\
  -H "Authorization: Bearer sk_test_YOUR_KEY" \\
  -H "Content-Type: application/json" \\
  -H "Idempotency-Key: inv-2026-0001" \\
  -d @document.json`}</CodeBlock>
        <Callout>
          <p className="mb-2">
            <strong className="text-[#09090b]">Console path:</strong> open your workspace in Ops →{" "}
            <span className="font-mono text-[12px]">Developer → API Keys</span>. Create a{" "}
            <em>test</em> key for sandbox and a <em>live</em> key for production. Use the scope
            multi-select (or Aura / LHDN presets).
          </p>
          <p className="mb-2">
            Prefer least privilege: grant only the scopes your integration needs. Payment-only keys
            cannot call LHDN document write routes (403); LHDN-only keys cannot create checkouts.
          </p>
          <p>
            <strong className="text-[#09090b]">Revoke &amp; cache:</strong> Revoking a key soft-deactivates
            it and publishes a cache-eviction event so the same process fails closed immediately.
            Credential lookups are also cached up to <strong>5 minutes</strong>; multi-instance
            worst case without the revoke event is ≤5 minutes until the next lookup.
          </p>
        </Callout>
      </GuideSection>

      <GuideSection title="What not to do">
        <ul className="list-disc pl-5 space-y-2">
          <li>Copy a browser cookie JWT into Postman and treat it as a long-lived integration secret</li>
          <li>Ship sk_live_ secrets in frontend bundles or public repos</li>
          <li>Reuse one super-admin JWT across tenants</li>
          <li>Call internal Ops chat / admin surfaces with integrator keys</li>
          <li>Mint keys with every scope checked “just in case”</li>
        </ul>
      </GuideSection>

      <GuideSection title="Next steps">
        <div className="flex flex-wrap gap-3 text-[12px] font-bold tracking-widest uppercase">
          <Link href="/quickstart" className="border border-[#09090b] bg-white px-4 py-2 hover:bg-[#09090b] hover:text-white">
            First e-invoice →
          </Link>
          <Link href="/webhooks" className="border border-[#e5e5e5] bg-white px-4 py-2 hover:border-[#09090b]">
            Event catalog →
          </Link>
          <Link href="/lhdn" className="border border-[#e5e5e5] bg-white px-4 py-2 hover:border-[#09090b]">
            LHDN reference →
          </Link>
        </div>
      </GuideSection>
    </HubShell>
  );
}
