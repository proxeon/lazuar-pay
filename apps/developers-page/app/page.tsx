import Link from "next/link";

const modules = [
  {
    href: "/one",
    title: "Lazuar One (Core)",
    blurb:
      "Global identity, workspace provisioning, authentication, and platform-level entitlements.",
  },
  {
    href: "/ops",
    title: "Ops Console API",
    blurb: "Operator and workspace management surfaces used by the Lazuar console.",
  },
  {
    href: "/billing",
    title: "Billing",
    blurb: "Ledger, credits, packages, and billing operations for workspaces.",
  },
  {
    href: "/lhdn",
    title: "LHDN Gateway",
    blurb:
      "Malaysian e-Invoicing compliance. Submit clean JSON; we handle UBL 2.1 XML and PKI signatures.",
  },
] as const;

export default function DeveloperHub() {
  return (
    <main className="flex flex-col items-center justify-center min-h-screen bg-[#fafafa] px-4 font-sans text-[#09090b]">
      <div className="max-w-2xl w-full text-center space-y-4 mb-12">
        <h1 className="text-3xl font-bold tracking-tight">Lazuar Developer Hub</h1>
        <p className="text-[#71717a] text-sm">
          Select a module to view its API reference and integration guides.
        </p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6 w-full max-w-3xl">
        {modules.map((m) => (
          <Link
            key={m.href}
            href={m.href}
            className="group flex flex-col bg-white border border-[#e5e5e5] p-6 transition-all hover:border-[#09090b] hover:shadow-[4px_4px_0px_0px_rgba(0,0,0,0.1)]"
          >
            <div className="flex items-center justify-between mb-4">
              <h2 className="font-bold uppercase tracking-widest text-[12px]">{m.title}</h2>
              <span className="text-[10px] bg-[#f4f4f5] text-[#71717a] px-2 py-1 font-mono">
                v1
              </span>
            </div>
            <p className="text-[#71717a] text-[13px] leading-relaxed mb-6 flex-1">{m.blurb}</p>
            <span className="text-[11px] font-bold tracking-widest uppercase text-[#09090b] group-hover:underline">
              View Reference →
            </span>
          </Link>
        ))}
      </div>
    </main>
  );
}
