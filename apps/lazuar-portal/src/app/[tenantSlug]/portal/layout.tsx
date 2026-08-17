import { ReactNode, Suspense } from "react";
import { serverClient } from "../../../modules/core/lib/server-client";
import { PortalDashboardLink } from "../../../modules/portal/components/PortalDashboardLink";

export default async function PortalLayout({
  children,
  params,
}: {
  children: ReactNode;
  params: Promise<{ tenantSlug: string }>;
}) {
  const { tenantSlug } = await params;

  const { data: authData } = await serverClient.GET("/one/auth/me");
  const userName = authData?.name || "Member";

  return (
    <div className="min-h-screen flex flex-col bg-zinc-50 dark:bg-black">
      <header className="sticky top-0 z-40 w-full bg-card border-b border-border/60 shadow-sm">
        <div className="max-w-5xl mx-auto px-4 h-16 flex items-center justify-between">
          <Suspense fallback={<span className="text-sm font-bold uppercase tracking-widest text-foreground">Buyer Dashboard</span>}>
            <PortalDashboardLink
              tenantSlug={tenantSlug}
              className="text-sm font-bold uppercase tracking-widest text-foreground hover:opacity-70 transition-opacity"
            >
              Buyer Dashboard
            </PortalDashboardLink>
          </Suspense>

          <div className="flex items-center gap-4">
            <span className="text-xs font-bold uppercase tracking-widest text-muted-foreground hidden sm:inline">
              {userName}
            </span>
            {authData && (
              <>
                <div className="h-4 w-px bg-border hidden sm:block"></div>
                <form action={async () => {
                  "use server";
                  await serverClient.POST("/one/auth/logout");
                }}>
                  <button 
                    type="submit"
                    className="text-xs font-bold uppercase tracking-widest text-foreground hover:text-red-600 transition-colors flex items-center gap-1.5"
                  >
                    Logout
                  </button>
                </form>
              </>
            )}
          </div>
        </div>
      </header>

      <main className="flex-1 w-full max-w-5xl mx-auto px-4 py-8 md:py-12">
        {children}
      </main>
    </div>
  );
}
