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
    <div className="flex flex-col flex-1 w-full font-sans selection:bg-foreground selection:text-background">
      {children}
    </div>
  );
}
