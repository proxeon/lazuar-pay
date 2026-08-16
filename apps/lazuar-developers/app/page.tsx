import Link from "next/link";

const DOCS = process.env.NEXT_PUBLIC_DOCS_URL || "http://localhost:5180";

const guides = [
  {
    href: `${DOCS}/integrations/`,
    title: "How to integrate",
    blurb: "Start in the VitePress guides. OpenAPI below is the schema, not onboarding.",
    external: true,
  },
  {
    href: `${DOCS}/integrations/hosted-checkout`,
    title: "Hosted Commerce checkout",
    blurb: "Signup → BYOK + Resend → product link → fulfill on order.completed / subscription.activated.",
    external: true,
  },
  {
    href: "/payments-cashier",
    title: "Payments cashier",
    blurb: "Provision a workspace, mint a key, create an M2M checkout, verify payment webhooks.",
    external: false,
  },
  {
    href: "/quickstart",
    title: "LHDN quickstart",
    blurb: "Submit your first e-invoice with curl or the LHDN SDK, then verify signatures.",
    external: false,
  },
] as const;

const reference = [
  {
    href: "/payments",
    title: "Payments (M2M cashier)",
    blurb:
      "Ad-hoc amount checkouts. Scoped keys, idempotency, payment.completed / payment.failed. Not Commerce catalog.",
    badge: "Cashier",
  },
  {
    href: "/lhdn",
    title: "LHDN Gateway",
    blurb: "Malaysian e-Invoicing. Submit JSON; we handle UBL 2.1 XML and PKI signatures.",
    badge: "Primary",
  },
  {
    href: "/commerce",
    title: "Commerce OpenAPI",
    blurb:
      "Hosted checkout is a guide, not this Scalar admin tree. Public buy links + webhooks; M2M list/get/cancel is /integrations/commerce/subscriptions.",
    badge: "Reference",
  },
] as const;

const advanced = [
  {
    href: "/one",
    title: "Lazuar One (Core)",
    blurb: "Identity, workspace provision, API keys. Not a checkout onboarding path.",
  },
  {
    href: "/billing",
    title: "Billing",
    blurb: "Ledger, credits, packages (console / admin).",
  },
  {
    href: "/ops",
    title: "Ops Console API",
    blurb: "Internal operator surfaces. Not for external integrators.",
  },
] as const;

export default function DeveloperHub() {
  return (
    <main className="flex flex-col items-center min-h-screen bg-[#fafafa] px-4 py-16 font-sans text-[#09090b]">
      <div className="max-w-2xl w-full text-center space-y-4 mb-12">
        <h1 className="text-3xl font-bold tracking-tight">Lazuar Developer Hub</h1>
        <p className="text-[#71717a] text-sm leading-relaxed">
          How to integrate starts in the guides. OpenAPI is the schema. Create keys in Ops →
          Developer → API Keys, call with{" "}
          <code className="font-mono text-[12px] bg-[#f4f4f5] px-1">Bearer sk_…</code>, receive
          signed webhooks.
        </p>
      </div>

      <section className="w-full max-w-3xl mb-12">
        <h2 className="text-[11px] font-bold uppercase tracking-widest text-[#71717a] mb-4">
          Start here
        </h2>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
          {guides.map((g) => (
            <Link
              key={g.href}
              href={g.href}
              className="group flex flex-col bg-white border border-[#e5e5e5] p-5 transition-all hover:border-[#09090b] hover:shadow-[4px_4px_0px_0px_rgba(0,0,0,0.1)]"
            >
              <h3 className="font-bold uppercase tracking-widest text-[12px] mb-2">{g.title}</h3>
              <p className="text-[#71717a] text-[13px] leading-relaxed flex-1">{g.blurb}</p>
              <span className="mt-4 text-[11px] font-bold tracking-widest uppercase text-[#09090b] group-hover:underline">
                {g.external ? "Open guide →" : "Open →"}
              </span>
            </Link>
          ))}
        </div>
      </section>

      <section className="w-full max-w-3xl mb-12">
        <h2 className="text-[11px] font-bold uppercase tracking-widest text-[#71717a] mb-4">
          API reference (not onboarding)
        </h2>
        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          {reference.map((m) => (
            <Link
              key={m.href}
              href={m.href}
              className="group flex flex-col bg-white border border-[#e5e5e5] p-6 transition-all hover:border-[#09090b] hover:shadow-[4px_4px_0px_0px_rgba(0,0,0,0.1)]"
            >
              <div className="flex items-center justify-between mb-4">
                <h2 className="font-bold uppercase tracking-widest text-[12px]">{m.title}</h2>
                <span className="text-[10px] px-2 py-1 font-mono bg-[#f4f4f5] text-[#71717a]">
                  {m.badge}
                </span>
              </div>
              <p className="text-[#71717a] text-[13px] leading-relaxed mb-6 flex-1">{m.blurb}</p>
              <span className="text-[11px] font-bold tracking-widest uppercase text-[#09090b] group-hover:underline">
                View schema →
              </span>
            </Link>
          ))}
        </div>
      </section>

      <section className="w-full max-w-3xl">
        <h2 className="text-[11px] font-bold uppercase tracking-widest text-[#71717a] mb-4">
          Reference (advanced)
        </h2>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          {advanced.map((m) => (
            <Link
              key={m.href}
              href={m.href}
              className="group flex flex-col bg-white border border-dashed border-[#d4d4d8] p-5 opacity-90 transition-all hover:border-[#09090b] hover:opacity-100"
            >
              <h3 className="font-bold uppercase tracking-widest text-[12px] mb-2">{m.title}</h3>
              <p className="text-[#71717a] text-[13px] leading-relaxed flex-1">{m.blurb}</p>
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
