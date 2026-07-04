import { redirect, notFound } from "next/navigation";
import { serverClient } from "../../../modules/core/lib/server-client";
import { ShieldCheck /* [MVP-HIDE] , FileText */ } from "lucide-react";

export default async function AggregatedPortalPage({
  params,
  searchParams,
}: {
  params: Promise<{ tenantSlug: string }>;
  searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
  const { tenantSlug } = await params;
  const resolvedSearchParams = await searchParams;
  const token = resolvedSearchParams.token as string | undefined;

  if (!token) {
    const { data: authCheck } = await serverClient.GET("/one/auth/me");
    if (!authCheck) {
      return (
        <div className="flex flex-col items-center justify-center min-h-[50vh] text-center p-4">
          <h1 className="text-2xl font-semibold mb-4 text-foreground">Welcome to your Dashboard</h1>
          <p className="text-sm text-muted-foreground max-w-md mx-auto">
            Please log in using a secure magic link sent to your email to manage your subscriptions and downloads.
          </p>
        </div>
      );
    }
  }

  const { data: commerceData, error: commerceError } = await serverClient.GET("/public/commerce/{tenantSlug}/portal", {
    params: { path: { tenantSlug }, query: { token: token ?? "" } },
    next: { revalidate: 0 }
  });

  if (commerceError || !commerceData) {
    notFound();
  }

  return (
    <div className="w-full max-w-4xl mx-auto space-y-8">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between p-4 bg-emerald-50/50 border border-emerald-200 dark:bg-emerald-950/20 dark:border-emerald-900 gap-2">
        <p className="text-[11px] font-bold uppercase tracking-widest text-emerald-700 dark:text-emerald-500 flex items-center gap-1.5">
          <ShieldCheck size={14} /> Identity Verified
        </p>
        <p className="text-[11px] font-medium text-emerald-600 dark:text-emerald-500/80">
          Accessing resources for this workspace.
        </p>
      </div>

      <div className="space-y-8">
        <h2 className="text-xl font-bold tracking-tight text-foreground border-b border-border/60 pb-2">Active Subscriptions</h2>
        {commerceData.subscriptions.length === 0 ? (
          <p className="text-sm text-muted-foreground">No active subscriptions found.</p>
        ) : (
          <div className="grid grid-cols-1 gap-4">
            {commerceData.subscriptions.map(sub => {
              const isActive = sub.status === "ACTIVE" || sub.status === "PAST_DUE";

              return (
                <div key={sub.id} className="bg-card border border-border/60 shadow-sm p-6 rounded-none flex flex-col md:flex-row gap-6 justify-between">
                  <div className="flex-1">
                    <div className="flex items-center gap-3 mb-2">
                      <h3 className="text-lg font-semibold text-foreground">{sub.product_name}</h3>
                      <span className="text-[10px] font-bold uppercase tracking-widest bg-secondary text-foreground px-2 py-0.5 border border-border">
                        {sub.status.replace("_", " ")}
                      </span>
                    </div>
                    {sub.current_period_end && (
                      <p className="text-xs text-muted-foreground font-mono mb-4">
                        Renews/Expires: {new Date(sub.current_period_end).toLocaleDateString()}
                      </p>
                    )}
                  </div>
                  
                  <div className="shrink-0 flex flex-col gap-2 items-end justify-center">
                    {/* [MVP-HIDE]
                    <a href={`/api/billing/invoice?subscription=${sub.id}`} className="flex items-center gap-1.5 text-[10px] font-bold uppercase tracking-widest text-muted-foreground hover:text-foreground transition-colors mb-2">
                      <FileText size={12} /> Download Tax Invoice
                    </a>
                    */}
                    {isActive && (
                      <form action={async () => {
                        "use server";
                        await serverClient.POST("/public/commerce/{tenantSlug}/portal/cancel", {
                          params: { path: { tenantSlug }, query: { token: token ?? "" } },
                          body: { subscription_id: sub.id }
                        });
                      }}>
                        <button className="h-9 px-4 border border-red-200 bg-background text-red-600 text-[11px] font-bold uppercase tracking-widest hover:bg-red-50 transition-colors rounded-none">
                          Cancel Plan
                        </button>
                      </form>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}
