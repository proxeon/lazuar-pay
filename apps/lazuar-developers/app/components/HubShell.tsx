import Link from "next/link";
import type { ReactNode } from "react";

const nav = [
  { href: "/", label: "Hub" },
  { href: "/quickstart", label: "Quickstart" },
  { href: "/auth", label: "Authentication" },
  { href: "/webhooks", label: "Event catalog" },
  { href: "/lhdn", label: "LHDN API" },
] as const;

export function HubShell({
  title,
  description,
  children,
}: {
  title: string;
  description?: string;
  children: ReactNode;
}) {
  return (
    <div className="min-h-screen bg-[#fafafa] text-[#09090b] font-sans">
      <header className="border-b border-[#e5e5e5] bg-white">
        <div className="mx-auto max-w-3xl px-4 py-4 flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <Link href="/" className="text-[11px] font-bold tracking-widest uppercase">
            Lazuar Developer Hub
          </Link>
          <nav className="flex flex-wrap gap-x-4 gap-y-2 text-[11px] font-bold tracking-widest uppercase text-[#71717a]">
            {nav.map((item) => (
              <Link key={item.href} href={item.href} className="hover:text-[#09090b]">
                {item.label}
              </Link>
            ))}
          </nav>
        </div>
      </header>

      <main className="mx-auto max-w-3xl px-4 py-12">
        <div className="mb-10 space-y-3">
          <h1 className="text-3xl font-bold tracking-tight">{title}</h1>
          {description ? (
            <p className="text-[#71717a] text-sm leading-relaxed max-w-2xl">{description}</p>
          ) : null}
        </div>
        {children}
      </main>
    </div>
  );
}

export function GuideSection({
  title,
  children,
}: {
  title: string;
  children: ReactNode;
}) {
  return (
    <section className="mb-10">
      <h2 className="text-[12px] font-bold uppercase tracking-widest mb-3 border-b border-[#e5e5e5] pb-2">
        {title}
      </h2>
      <div className="space-y-3 text-[14px] leading-relaxed text-[#3f3f46]">{children}</div>
    </section>
  );
}

export function CodeBlock({ children }: { children: string }) {
  return (
    <pre className="overflow-x-auto border border-[#e5e5e5] bg-[#09090b] text-[#fafafa] p-4 text-[12px] font-mono leading-relaxed">
      <code>{children}</code>
    </pre>
  );
}

export function Callout({ children }: { children: ReactNode }) {
  return (
    <div className="border border-[#e5e5e5] bg-white p-4 text-[13px] leading-relaxed text-[#3f3f46]">
      {children}
    </div>
  );
}
