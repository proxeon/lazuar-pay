// apps/portal-page/src/app/[tenantSlug]/layout.tsx
import { ReactNode } from "react";

export default async function TenantLayout({
  children,
  params,
}: {
  children: ReactNode;
  params: Promise<{ tenantSlug: string }>;
}) {
  await params;

  return (
    <div className="flex flex-col min-h-screen w-full bg-zinc-50 dark:bg-black font-sans text-foreground selection:bg-foreground selection:text-background antialiased">
      {children}
    </div>
  );
}
