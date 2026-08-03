import Link from "next/link";

const guides = [
  {
    href: "/quickstart",
    title: "Quickstart",
    blurb: "Submit your first e-invoice with curl or the LHDN SDK, then verify signatures.",
  },
  {
    href: "/auth",
    title: "Authentication",
    blurb: "API keys (Bearer sk_) for machines. JWT only for human sessions — never in ERP.",
  },
  {
    href: "/webhooks",
    title: "Event catalog",
    blurb: "Outbound webhook events, headers, and X-Lazuar-Signature verification.",
  },
] as const;

const products = [
  {
    href: "/lhdn",
    title: "LHDN Gateway",
    blurb:
      "Malaysian e-Invoicing compliance. Submit clean JSON; we handle UBL 2.1 XML and PKI signatures.",
    badge: "Primary",
    audience: "external" as const,
  },
  {
    href: "/one",
    title: "Lazuar One (Core)",
    blurb:
      "Global identity, workspace provisioning, authentication, and platform-level entitlements.",
    badge: "Platform",
    audience: "external" as const,
  },
  {
    href: "/commerce",
    title: "Commerce",
    blurb:
      "Checkout, products, subscriptions, and public portal routes. Pair with workspace webhooks for unlock/revoke.",
    badge: "v1",
    audience: "external" as const,
  },
  {
    href: "/billing",
    title: "Billing",
    blurb: "Ledger, credits, packages, and billing operations for workspaces (console / admin).",
    badge: "Admin",
    audience: "external" as const,
  },
  {
    href: "/ops",
    title: "Ops Console API",
    blurb: "Internal operator surfaces used by the Lazuar console. Not for external integrators.",
    badge: "Internal",
    audience: "internal" as const,
  },
] as const;

export default function DeveloperHub() {
  return (
    <main className="flex flex-col items-center min-h-screen bg-[#fafafa] px-4 py-16 font-sans text-[#09090b]">
      <div className="max-w-2xl w-full text-center space-y-4 mb-12">
        <h1 className="text-3xl font-bold tracking-tight">Lazuar Developer Hub</h1>
        <p className="text-[#71717a] text-sm leading-relaxed">
          Integration guides and product APIs. Create keys in Ops → Developer → API Keys, call with{" "}
          <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">Bearer sk_…</code>, receive
          signed webhooks.
        </p>
      </div>

      <section className="w-full max-w-3xl mb-12">
        <h2 className="text-[11px] font-bold uppercase tracking-widest text-[#71717a] mb-4">
          Start here
        </h2>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          {guides.map((g) => (
            <Link
              key={g.href}
              href={g.href}
              className="group flex flex-col bg-white border border-[#e5e5e5] p-5 transition-all hover:border-[#09090b] hover:shadow-[4px_4px_0px_0px_rgba(0,0,0,0.1)]"
            >
              <h3 className="font-bold uppercase tracking-widest text-[12px] mb-2">{g.title}</h3>
              <p className="text-[#71717a] text-[13px] leading-relaxed flex-1">{g.blurb}</p>
              <span className="mt-4 text-[11px] font-bold tracking-widest uppercase text-[#09090b] group-hover:underline">
                Open →
              </span>
            </Link>
          ))}
        </div>
      </section>

      <section className="w-full max-w-3xl">
        <h2 className="text-[11px] font-bold uppercase tracking-widest text-[#71717a] mb-4">
          API references
        </h2>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          {products.map((m) => (
            <Link
              key={m.href}
              href={m.href}
              className={`group flex flex-col bg-white border p-6 transition-all hover:border-[#09090b] hover:shadow-[4px_4px_0px_0px_rgba(0,0,0,0.1)] ${
                m.audience === "internal" ? "border-dashed border-[#d4d4d8] opacity-90" : "border-[#e5e5e5]"
              }`}
            >
              <div className="flex items-center justify-between mb-4">
                <h2 className="font-bold uppercase tracking-widest text-[12px]">{m.title}</h2>
                <span
                  className={`text-[10px] px-2 py-1 font-mono ${
                    m.badge === "Primary"
                      ? "bg-[#09090b] text-white"
                      : m.badge === "Internal"
                        ? "bg-[#f4f4f5] text-[#a1a1aa]"
                        : "bg-[#f4f4f5] text-[#71717a]"
                  }`}
                >
                  {m.badge}
                </span>
              </div>
              <p className="text-[#71717a] text-[13px] leading-relaxed mb-6 flex-1">{m.blurb}</p>
              <span className="text-[11px] font-bold tracking-widest uppercase text-[#09090b] group-hover:underline">
                View Reference →
              </span>
            </Link>
          ))}
        </div>
      </section>

      <footer className="mt-16 max-w-3xl w-full text-center text-[12px] text-[#a1a1aa] space-y-1">
        <p>
          Production API:{" "}
          <code className="font-mono text-[#71717a]">https://hub.lazuar.com/api/v1</code>
        </p>
        <p>
          SDKs: <code className="font-mono text-[#71717a]">@lazuar/lhdn-sdk</code> ·{" "}
          <code className="font-mono text-[#71717a]">Lazuar.Lhdn.Sdk</code>
        </p>
      </footer>
    </main>
  );
}
