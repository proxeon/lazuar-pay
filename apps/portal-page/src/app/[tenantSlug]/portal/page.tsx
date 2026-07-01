import { notFound } from "next/navigation";
import { serverClient } from "../../../modules/core/lib/server-client";
import { ShieldCheck, FileText, CreditCard, RefreshCw } from "lucide-react";

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
          <h1 className="text-2xl font-semibold mb-4 text-foreground">Welcome to your Billing Dashboard</h1>
          <p className="text-sm text-muted-foreground max-w-md mx-auto">
            Please log in using a secure magic link sent to your email to manage your subscriptions and tax invoices.
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
    <div className="w-full max-w-5xl mx-auto space-y-12">
      <div className="flex flex-col sm:flex-row sm:items-center justify-between p-4 bg-emerald-50/50 border border-emerald-200 dark:bg-emerald-950/20 dark:border-emerald-900 gap-2 rounded-none">
        <p className="text-[11px] font-bold uppercase tracking-widest text-emerald-700 dark:text-emerald-500 flex items-center gap-1.5">
          <ShieldCheck size={14} /> Identity Verified
        </p>
        <p className="text-[11px] font-medium text-emerald-600 dark:text-emerald-500/80">
          Managing billing and tax invoices for this workspace.
        </p>
      </div>

      <div className="space-y-6">
        <div className="flex items-center justify-between border-b border-border/60 pb-2">
          <h2 className="text-lg font-bold tracking-tight text-foreground uppercase">Active Subscriptions</h2>
        </div>
        
        {commerceData.subscriptions.length === 0 ? (
          <div className="p-8 text-center border border-dashed border-border/60 bg-secondary/10">
            <p className="text-sm text-muted-foreground">No active subscriptions found.</p>
          </div>
        ) : (
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {commerceData.subscriptions.map(sub => {
              const isActive = sub.status === "ACTIVE" || sub.status === "PAST_DUE";

              return (
                <div key={sub.id} className="bg-card border border-border/60 shadow-sm p-6 rounded-none flex flex-col justify-between">
                  <div>
                    <div className="flex items-start justify-between mb-4">
                      <h3 className="text-base font-semibold text-foreground leading-tight">{sub.product_name}</h3>
                      <span className="text-[10px] font-bold uppercase tracking-widest bg-secondary text-foreground px-2 py-0.5 border border-border shrink-0 ml-2">
                        {sub.status.replace("_", " ")}
                      </span>
                    </div>
                    
                    <div className="space-y-2 mb-6 text-sm">
                      <div className="flex justify-between">
                        <span className="text-muted-foreground">Next Billing Date</span>
                        <span className="font-mono text-foreground font-medium">
                          {sub.current_period_end ? new Date(sub.current_period_end).toLocaleDateString() : "N/A"}
                        </span>
                      </div>
                    </div>
                  </div>
                  
                  <div className="pt-4 border-t border-border/40 flex items-center justify-between">
                    <a href={`/api/billing/invoice?subscription=${sub.id}`} className="flex items-center gap-1.5 text-[10px] font-bold uppercase tracking-widest text-muted-foreground hover:text-foreground transition-colors">
                      <FileText size={12} /> Tax Invoice
                    </a>
                    {isActive && (
                      <form action={async () => {
                        "use server";
                        await serverClient.POST("/public/commerce/{tenantSlug}/portal/cancel", {
                          params: { path: { tenantSlug }, query: { token: token ?? "" } },
                          body: { subscription_id: sub.id }
                        });
                      }}>
                        <button className="h-8 px-4 border border-red-200 bg-background text-red-600 text-[10px] font-bold uppercase tracking-widest hover:bg-red-50 transition-colors rounded-none">
                          Cancel
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

      <div className="space-y-6">
        <div className="flex items-center justify-between border-b border-border/60 pb-2">
          <h2 className="text-lg font-bold tracking-tight text-foreground uppercase">Payment Methods</h2>
        </div>
        
        <div className="p-6 bg-secondary/10 border border-border/60 flex flex-col sm:flex-row items-center justify-between gap-4">
          <div className="flex items-center gap-4">
            <div className="h-10 w-10 bg-background border border-border flex items-center justify-center shrink-0">
              <CreditCard size={18} className="text-muted-foreground" />
            </div>
            <div>
              <p className="text-sm font-semibold text-foreground">Secure Payment Vault</p>
              <p className="text-xs text-muted-foreground mt-0.5">Manage the cards charged for your active subscriptions.</p>
            </div>
          </div>
          <button disabled className="h-9 px-4 bg-foreground text-background text-[11px] font-bold uppercase tracking-widest hover:opacity-90 transition-opacity flex items-center gap-2 rounded-none opacity-50 cursor-not-allowed">
            <RefreshCw size={12} /> Update Card
          </button>
        </div>
      </div>

      <div className="space-y-6">
        <div className="flex items-center justify-between border-b border-border/60 pb-2">
          <h2 className="text-lg font-bold tracking-tight text-foreground uppercase">Transaction History & Tax Invoices</h2>
        </div>
        
        {commerceData.orders.length === 0 ? (
          <div className="p-8 text-center border border-dashed border-border/60 bg-secondary/10">
            <p className="text-sm text-muted-foreground">No historical transactions found.</p>
          </div>
        ) : (
          <div className="w-full overflow-x-auto">
            <table className="w-full text-left text-sm whitespace-nowrap">
              <thead className="bg-secondary/30 border-b border-border/60">
                <tr>
                  <th className="px-6 py-4 font-bold uppercase tracking-widest text-muted-foreground text-[10px]">Date</th>
                  <th className="px-6 py-4 font-bold uppercase tracking-widest text-muted-foreground text-[10px]">Description</th>
                  <th className="px-6 py-4 font-bold uppercase tracking-widest text-muted-foreground text-[10px]">Status</th>
                  <th className="px-6 py-4 font-bold uppercase tracking-widest text-muted-foreground text-[10px] text-right">Document</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-border/40">
                {commerceData.orders.map(order => (
                  <tr key={order.id} className="bg-card hover:bg-secondary/20 transition-colors">
                    <td className="px-6 py-4 font-mono text-muted-foreground text-xs">
                      {new Date(order.created_at).toLocaleDateString()}
                    </td>
                    <td className="px-6 py-4 font-medium text-foreground">
                      {order.product_name}
                    </td>
                    <td className="px-6 py-4">
                      <span className="text-[10px] font-bold uppercase tracking-widest text-foreground">
                        {order.status}
                      </span>
                    </td>
                    <td className="px-6 py-4 text-right">
                      <a href={`/api/billing/invoice?order=${order.id}`} className="inline-flex items-center justify-center h-8 px-3 border border-border bg-background hover:bg-accent hover:text-accent-foreground text-[10px] font-bold uppercase tracking-widest transition-colors gap-1.5">
                        <FileText size={12} /> Download
                      </a>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
