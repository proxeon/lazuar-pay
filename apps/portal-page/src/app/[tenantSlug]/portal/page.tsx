import { redirect, notFound } from "next/navigation";
import { serverClient } from "../../../modules/core/lib/server-client";
import { ShieldCheck, Download, ExternalLink, Video } from "lucide-react";

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
          <p className="text-muted-foreground text-sm max-w-md">
            Please log in using a secure magic link sent to your email to manage your subscriptions and downloads.
          </p>
        </div>
      );
    }
  }

  // 1. Fetch the user's active financial contracts (Commerce Module)
  const { data: commerceData, error: commerceError } = await serverClient.GET("/public/commerce/{tenantSlug}/portal", {
    params: { path: { tenantSlug }, query: { token: token ?? "" } },
    next: { revalidate: 0 }
  });

  if (commerceError || !commerceData) {
    notFound();
  }

  const subProductIds = commerceData.subscriptions.map(s => s.product_id);
  const orderProductIds = commerceData.orders.map(o => o.product_id);

  // 2. Fetch the actual fulfillment details in parallel (Community & Vault Modules)
  const [spacesRes, assetsRes] = await Promise.all([
    subProductIds.length > 0 ? serverClient.GET("/public/community/{tenantSlug}/portal/spaces", {
      params: { path: { tenantSlug }, query: { product_ids: subProductIds } }
    }) : Promise.resolve({ data: [] }),
    orderProductIds.length > 0 ? serverClient.GET("/public/vault/{tenantSlug}/portal/assets", {
      params: { path: { tenantSlug }, query: { product_ids: orderProductIds } }
    }) : Promise.resolve({ data: [] })
  ]);

  const spacesMap = new Map(spacesRes.data?.map(s => [s.product_id, s]));
  const assetsMap = new Map(assetsRes.data?.map(a => [a.product_id, a]));

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
              const space = spacesMap.get(sub.product_id);
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
                    
                    {isActive && space && (
                      <div className="flex flex-col sm:flex-row gap-3 mt-4">
                        {space.telegram_link && (
                          <a href={space.telegram_link} target="_blank" rel="noopener noreferrer" className="flex items-center gap-2 text-sm font-medium text-blue-600 dark:text-blue-400 hover:underline">
                            <ExternalLink size={16} /> Join Telegram Group
                          </a>
                        )}
                        {space.zoom_link && (
                          <a href={space.zoom_link} target="_blank" rel="noopener noreferrer" className="flex items-center gap-2 text-sm font-medium text-indigo-600 dark:text-indigo-400 hover:underline">
                            <Video size={16} /> Open Weekly Zoom
                          </a>
                        )}
                      </div>
                    )}
                  </div>
                  
                  <div className="shrink-0 flex items-center">
                    {isActive && (
                      <form action={async () => {
                        "use server";
                        await serverClient.POST("/public/community/{tenantSlug}/portal/cancel", {
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

        <h2 className="text-xl font-bold tracking-tight text-foreground border-b border-border/60 pb-2 mt-12">Digital Vault (One-Time Purchases)</h2>
        {commerceData.orders.length === 0 ? (
          <p className="text-sm text-muted-foreground">No digital downloads found.</p>
        ) : (
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            {commerceData.orders.map(order => {
              const asset = assetsMap.get(order.product_id);
              
              return (
                <div key={order.id} className="bg-card border border-border/60 shadow-sm p-6 rounded-none flex flex-col justify-between h-full">
                  <div>
                    <h3 className="text-base font-semibold text-foreground mb-1">{order.product_name}</h3>
                    <p className="text-[10px] text-muted-foreground font-mono uppercase tracking-widest mb-6">
                      Purchased: {new Date(order.created_at).toLocaleDateString()}
                    </p>
                  </div>
                  
                  {asset ? (
                    <a href={asset.cloudflare_r2_url} download className="h-10 w-full bg-foreground text-background flex items-center justify-center gap-2 text-[11px] font-bold uppercase tracking-widest hover:opacity-90 transition-opacity">
                      <Download size={14} /> Download File
                    </a>
                  ) : (
                    <button disabled className="h-10 w-full bg-secondary text-muted-foreground flex items-center justify-center text-[11px] font-bold uppercase tracking-widest cursor-not-allowed">
                      Pending Fulfillment
                    </button>
                  )}
                </div>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}
