import { CSSProperties, ReactNode } from "react";
import { fetchWorkspaceBranding } from "../../modules/core/lib/branding";

export default async function TenantLayout({
  children,
  params,
}: {
  children: ReactNode;
  params: Promise<{ tenantSlug: string }>;
}) {
  const { tenantSlug } = await params;
  const branding = await fetchWorkspaceBranding(tenantSlug);
  const style = branding?.primary_color
    ? ({ ["--brand" as string]: branding.primary_color } as CSSProperties)
    : undefined;

  return (
    <div
      className="flex flex-col flex-1 w-full font-sans selection:bg-foreground selection:text-background"
      style={style}
    >
      {children}
    </div>
  );
}
