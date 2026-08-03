import Link from "next/link";
import { Callout, CodeBlock, GuideSection, HubShell } from "../components/HubShell";

export const metadata = {
  title: "Authentication — Lazuar Developer Hub",
  description: "API keys vs JWT for Lazuar machine integrations",
};

export default function AuthGuidePage() {
  return (
    <HubShell
      title="Authentication"
      description="Machine clients use API keys. Human sessions use JWT cookies. Never put a user JWT in an ERP, cron, or server-side integration."
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
              <li>Scoped product grants (e.g. lhdn.documents:write)</li>
              <li>Secret shown once at create; store in a secrets manager</li>
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
            <em>test</em> key for sandbox submissions and a <em>live</em> key for production.
          </p>
          <p>
            Prefer least privilege: grant only the scopes your integration needs (for example{" "}
            <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">lhdn.documents:write</code>
            ). Keys cannot mint more keys or change payment config when correctly scoped.
          </p>
        </Callout>
      </GuideSection>

      <GuideSection title="What not to do">
        <ul className="list-disc pl-5 space-y-2">
          <li>Copy a browser cookie JWT into Postman and treat it as a long-lived integration secret</li>
          <li>Ship sk_live_ secrets in frontend bundles or public repos</li>
          <li>Reuse one super-admin JWT across tenants</li>
          <li>Call internal Ops chat / admin surfaces with integrator keys</li>
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
