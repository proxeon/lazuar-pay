// apps/portal-page/src/app/[tenantSlug]/community/[resourceSlug]/checkout/layout.tsx
import { ReactNode } from "react";
import Link from "next/link";

export default async function BlindCheckoutLayout({
  children,
  params,
}: {
  children: ReactNode;
  params: Promise<{ tenantSlug: string; resourceSlug: string }>;
}) {
  const { tenantSlug, resourceSlug } = await params;

  return (
    <div className="min-h-screen flex flex-col bg-zinc-50 dark:bg-black">
      <header className="sticky top-0 z-40 w-full bg-card border-b border-border/60">
        <div className="max-w-5xl mx-auto px-4 h-14 flex items-center justify-between">
          <Link
            href={`/${tenantSlug}/community/${resourceSlug}`}
            className="inline-flex items-center gap-2 -ml-2 px-2 py-1.5 text-muted-foreground hover:text-foreground transition-all"
          >
            <svg
              className="h-4 w-4"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            >
              <path d="M19 12H5" />
              <path d="M12 19l-7-7 7-7" />
            </svg>
            <span className="text-sm font-medium">Back</span>
          </Link>
          <div className="flex items-center gap-1.5 text-muted-foreground">
            <svg
              className="h-3.5 w-3.5"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
            >
              <rect x="3" y="11" width="18" height="11" rx="2" ry="2" />
              <path d="M7 11V7a5 5 0 0 1 10 0v4" />
            </svg>
            <span className="text-xs font-semibold uppercase tracking-widest">
              Secure Checkout
            </span>
          </div>
        </div>
      </header>

      <main className="flex-1 w-full">
        {children}
      </main>
    </div>
  );
}
