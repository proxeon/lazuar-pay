import { useQuery } from "@tanstack/react-query";
import { Users, DollarSign, Activity, AlertTriangle, Package, Loader2, CreditCard, Mail, RotateCcw, CheckCircle2, Copy } from "lucide-react";
import { Link, useOutletContext } from "react-router-dom";
import { toast } from "sonner";
import { client } from "../../../lib/api-client";
import { BarChart, Bar, XAxis, Tooltip, ResponsiveContainer } from "recharts";
import { cn } from "../../../lib/utils";
import PageLayout from "../../core/components/PageLayout";

const CHECKLIST_DISMISS_KEY = "lazuar-getting-started-dismissed-until";

export default function DashboardPage() {
  const { entitlements, activeWorkspaceId } = useOutletContext<{
    entitlements?: { workspace_id: string; workspace_slug?: string }[];
    activeWorkspaceId?: string;
  }>();
  const workspaceSlug = entitlements?.find((e) => e.workspace_id === activeWorkspaceId)?.workspace_slug;
  const { data: stats, isLoading: statsLoading } = useQuery({
    queryKey: ["commerce-stats"],
    queryFn: async () => {
      const { data, error } = await client.GET("/admin/commerce/stats");
      if (error) throw new Error(error.detail);
      return data;
    }
  });

  const { data: financials, isLoading: financialsLoading } = useQuery({
    queryKey: ["financial-summary"],
    queryFn: async () => {
      const { data, error, response } = await client.GET("/admin/billing/summary");
      if (response.status === 403) return { forbidden: true as const, data: null };
      if (error) throw new Error(error.detail);
      return { forbidden: false as const, data };
    }
  });

  const { data: products, isLoading: productsLoading } = useQuery({
    queryKey: ["commerce-products"],
    queryFn: async () => {
      const { data, error } = await client.GET("/admin/commerce/products");
      if (error) throw new Error(error.detail);
      return data;
    }
  });

  const { data: paymentConfigs, isLoading: paymentConfigLoading } = useQuery({
    queryKey: ["payment-config-status"],
    queryFn: async () => {
      const { data, error, response } = await client.GET("/admin/commerce/payment-config");
      if (response.status === 403) return { forbidden: true as const, items: [] as NonNullable<typeof data> };
      if (response.status === 404) return { forbidden: false as const, items: [] as NonNullable<typeof data> };
      if (error) throw new Error(error.detail);
      return { forbidden: false as const, items: data ?? [] };
    }
  });

  const { data: emailConfig, isLoading: emailConfigLoading } = useQuery({
    queryKey: ["email-config-status"],
    queryFn: async () => {
      const { data, error, response } = await client.GET("/admin/communications/email-config");
      if (response.status === 403) return { forbidden: true as const, config: null };
      if (response.status === 404) return { forbidden: false as const, config: null };
      if (error) throw new Error(error.detail);
      return { forbidden: false as const, config: data };
    }
  });

  if (statsLoading || financialsLoading || productsLoading || paymentConfigLoading || emailConfigLoading) {
    return (
      <div className="flex-1 flex items-center justify-center h-full bg-[#fafafa]">
        <Loader2 className="animate-spin text-[#a1a1aa] h-8 w-8" />
      </div>
    );
  }

  const formatMYR = (val: number) => `RM ${val.toLocaleString('en-MY', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;

  const topMetrics = [
    { label: "Net revenue (after fees & tax)", value: financials?.forbidden ? "—" : formatMYR(financials?.data?.net_revenue || 0), icon: DollarSign, tip: "P&L net from the ledger (gross − refunds − discounts − known gateway fees − tax). Not bank cash. Hub/pack spend is excluded." },
    { label: "MRR", value: formatMYR(stats?.mrr || 0), icon: DollarSign, tip: "Committed monthly equivalent of active memberships. Not cash. Past-due is excluded." },
    { label: "ARR", value: formatMYR(stats?.arr ?? ((stats?.mrr || 0) * 12)), icon: DollarSign, tip: "Committed monthly equivalent of active memberships. Not cash. Past-due is excluded." },
    { label: "Active Subscribers", value: stats?.active_subscribers || 0, icon: Users },
    { label: "Past Due", value: stats?.past_due_subscribers || 0, icon: AlertTriangle, alert: (stats?.past_due_subscribers || 0) > 0 },
    { label: "Cancellation Rate", value: `${stats?.churn_rate_percentage || 0}%`, icon: Activity },
    { label: "Recovered (lifetime)", value: formatMYR(stats?.recovered_revenue || 0), icon: RotateCcw },
  ];

  const gatewayReady = !!paymentConfigs?.forbidden
    || !!paymentConfigs?.items?.some((c) => c.is_active && (c.has_api_key || c.has_secret_key));
  const emailReady = !!emailConfig?.forbidden
    || (!!emailConfig?.config && emailConfig.config.is_active && emailConfig.config.has_api_key !== false);
  const firstProduct = (products || [])[0] as { slug?: string } | undefined;
  const productReady = (products || []).length > 0;
  const linkReady = productReady && !!workspaceSlug && !!firstProduct?.slug;
  const checkoutOrigin = (import.meta.env.VITE_PORTAL_URL as string | undefined) || "http://localhost:3004";
  const checkoutUrl = linkReady ? `${checkoutOrigin.replace(/\/$/, "")}/${workspaceSlug}/checkout/${firstProduct!.slug}` : "";
  const allStepsDone = gatewayReady && emailReady && productReady && linkReady;
  const dismissedUntil = Number(localStorage.getItem(CHECKLIST_DISMISS_KEY) || 0);
  const showChecklist = !allStepsDone && Date.now() > dismissedUntil;

  const checklist = [
    { label: "Workspace created", done: true, href: null as string | null },
    { label: "Payment gateway (BYOK)", done: gatewayReady, href: "/workspace/payment-gateways" },
    { label: "Email (Resend) — required for paid checkout", done: emailReady, href: "/workspace/email" },
    { label: "First product", done: productReady, href: "/commerce/products" },
    { label: "Share checkout link", done: linkReady, href: null as string | null },
  ];

  return (
    <PageLayout 
      title="Sales Insights" 
      description="High-density overview of your financial and retention health."
      breadcrumbs={[{ label: "Commerce" }, { label: "Dashboard" }]}
    >
      <div className="space-y-6">
        {showChecklist && (
          <div className="bg-white border border-[#e5e5e5] p-5 space-y-3">
            <div className="flex items-start justify-between gap-4">
              <div>
                <p className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">Getting started</p>
                <p className="text-[12px] text-[#71717a] mt-1">
                  Signup → gateway → Resend → product → openable pay link. Test cards: Stripe test mode / Billplz sandbox.
                </p>
              </div>
              <button
                type="button"
                className="text-[10px] font-bold uppercase tracking-widest text-[#a1a1aa] hover:text-[#09090b]"
                onClick={() => {
                  localStorage.setItem(CHECKLIST_DISMISS_KEY, String(Date.now() + 30 * 24 * 60 * 60 * 1000));
                  window.location.reload();
                }}
              >
                Dismiss 30 days
              </button>
            </div>
            <ol className="space-y-2">
              {checklist.map((step, i) => (
                <li key={step.label} className="flex items-center justify-between gap-3 text-[13px]">
                  <span className="flex items-center gap-2">
                    {step.done ? (
                      <CheckCircle2 size={14} className="text-emerald-600" />
                    ) : i === 1 ? (
                      <CreditCard size={14} className="text-rose-500" />
                    ) : i === 2 ? (
                      <Mail size={14} className="text-amber-600" />
                    ) : (
                      <AlertTriangle size={14} className="text-amber-500" />
                    )}
                    <span className={step.done ? "text-[#52525b]" : "text-[#09090b] font-medium"}>
                      {i + 1}. {step.label}
                    </span>
                  </span>
                  {!step.done && step.href && (
                    <Link to={step.href} className="text-[10px] font-bold uppercase tracking-widest underline">
                      Open
                    </Link>
                  )}
                </li>
              ))}
            </ol>
            {linkReady && (
              <button
                type="button"
                className="h-8 px-3 border border-[#e5e5e5] text-[11px] font-bold uppercase tracking-widest inline-flex items-center gap-1.5 hover:border-[#09090b]"
                onClick={() => {
                  void navigator.clipboard.writeText(checkoutUrl);
                  toast.success("Checkout link copied.");
                }}
              >
                <Copy size={12} /> Copy pay link
              </button>
            )}
          </div>
        )}

        <div className="grid grid-cols-2 lg:grid-cols-3 xl:grid-cols-5 gap-4">
          {topMetrics.map((kpi, i) => (
            <div key={i} className={cn(
              "bg-white border p-4 flex flex-col justify-between",
              kpi.alert ? "border-amber-300 bg-amber-50/30" : "border-[#e5e5e5]"
            )} title={"tip" in kpi ? kpi.tip : undefined}>
              <div className="flex justify-between items-start mb-3">
                <span className="text-[10px] font-bold uppercase tracking-widest text-[#71717a]">{kpi.label}</span>
                <kpi.icon size={14} className={kpi.alert ? "text-amber-500" : "text-[#a1a1aa]"} />
              </div>
              <span className={cn(
                "text-2xl font-bold tracking-tight font-mono leading-none",
                kpi.alert ? "text-amber-700" : "text-[#09090b]"
              )}>
                {kpi.value}
              </span>
            </div>
          ))}
        </div>
        <p className="text-[10px] text-[#71717a] -mt-2">
          MRR / ARR is the committed monthly equivalent of active memberships. Not cash. Past-due is excluded.{" "}
          Recovered is campaign-lifetime cash collected while PAST_DUE or SUSPENDED, not this month.{" "}
          <Link to="/commerce/dunning-campaigns" className="underline underline-offset-2 hover:text-[#09090b]">
            Dunning Campaigns
          </Link>
        </p>

        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          <div className="lg:col-span-1 bg-white border border-[#e5e5e5] p-5 flex flex-col h-[320px]">
            <div className="flex items-center justify-between mb-2">
              <h3 className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">Revenue Trend</h3>
              <span className="text-[10px] font-mono text-[#71717a]">
                Total {formatMYR(stats?.total_revenue_collected || 0)}
              </span>
            </div>
            <div className="flex-1 w-full relative min-h-0">
              {(stats?.cash_flow_trend?.length ?? 0) > 0 && (stats?.cash_flow_trend?.some(p => (p.amount ?? 0) > 0)) ? (
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={stats?.cash_flow_trend || []} margin={{ top: 8, right: 4, left: 0, bottom: 0 }}>
                    <XAxis dataKey="month" tick={{ fontSize: 10 }} stroke="#a1a1aa" />
                    <Tooltip
                      formatter={(value: number | string) => formatMYR(Number(value))}
                      contentStyle={{ fontSize: 12 }}
                    />
                    <Bar dataKey="amount" fill="#09090b" radius={[2, 2, 0, 0]} />
                  </BarChart>
                </ResponsiveContainer>
              ) : (
                <div className="absolute inset-0 flex items-center justify-center">
                  <span className="text-[11px] text-[#a1a1aa]">No confirmed payments yet</span>
                </div>
              )}
            </div>
            {(stats?.payment_methods?.length ?? 0) > 0 && (
              <div className="mt-3 pt-3 border-t border-[#f4f4f5] space-y-1">
                <p className="text-[9px] font-bold uppercase tracking-widest text-[#71717a]">By source</p>
                {stats!.payment_methods!.slice(0, 3).map((pm) => (
                  <div key={pm.method} className="flex justify-between text-[11px]">
                    <span className="text-[#52525b] font-mono">{pm.method}</span>
                    <span className="font-mono text-[#09090b]">{formatMYR(pm.total_amount || 0)}</span>
                  </div>
                ))}
              </div>
            )}
          </div>

          <div className="lg:col-span-2 bg-white border border-[#e5e5e5] flex flex-col h-[320px]">
            <div className="px-5 py-4 border-b border-[#f4f4f5] flex items-center gap-2 bg-[#fafafa]/50">
              <Package size={14} className="text-[#a1a1aa]" />
              <h3 className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">Product Catalog</h3>
            </div>
            <div className="flex-1 overflow-auto">
              <table className="w-full text-left text-[12px]">
                <thead className="sticky top-0 bg-white border-b border-[#f4f4f5]">
                  <tr>
                    <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Product Name</th>
                    <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Price</th>
                    <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Interval</th>
                    <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] text-right">Status</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[#f4f4f5]">
                  {(products || []).map((product: any) => (
                    <tr key={product.id} className="hover:bg-[#fafafa]/50 transition-colors">
                      <td className="px-5 py-3.5 font-medium text-[#09090b]">
                        {product.name}
                      </td>
                      <td className="px-5 py-3.5 font-mono text-[#52525b]">RM {product.price.toFixed(2)}</td>
                      <td className="px-5 py-3.5 text-[#52525b] uppercase text-[10px]">{product.interval}</td>
                      <td className="px-5 py-3.5 text-right">
                         <span className={cn(
                          "text-[9px] px-1.5 py-0.5 border font-bold uppercase tracking-widest whitespace-nowrap inline-block",
                          product.is_active ? "bg-emerald-50 text-emerald-700 border-emerald-200" : "bg-zinc-100 text-zinc-500 border-zinc-200"
                        )}>
                          {product.is_active ? "Active" : "Archived"}
                        </span>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        </div>
      </div>
    </PageLayout>
  );
}
