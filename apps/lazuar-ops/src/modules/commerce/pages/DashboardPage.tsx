import { useQuery } from "@tanstack/react-query";
import { Users, DollarSign, Activity, AlertTriangle, Package, Loader2, CreditCard, Mail } from "lucide-react";
import { Link } from "react-router-dom";
import { client } from "../../../lib/api-client";
import { BarChart, Bar, XAxis, Tooltip, ResponsiveContainer } from "recharts";
import { cn } from "../../../lib/utils";
import PageLayout from "../../core/components/PageLayout";

export default function DashboardPage() {
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
      const { data, error } = await client.GET("/admin/billing/summary");
      if (error) throw new Error(error.detail);
      return data;
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

  const { data: paymentConfigs, error: paymentConfigError, isLoading: paymentConfigLoading } = useQuery({
    queryKey: ["payment-config-status"],
    queryFn: async () => {
      const { data, error, response } = await client.GET("/admin/commerce/payment-config");
      if (response.status === 404) return [];
      if (error) throw new Error(error.detail);
      return data;
    }
  });

  const { data: emailConfig, error: emailConfigError, isLoading: emailConfigLoading } = useQuery({
    queryKey: ["email-config-status"],
    queryFn: async () => {
      const { data, error, response } = await client.GET("/admin/communications/email-config");
      if (response.status === 404) return null;
      if (error) throw new Error(error.detail);
      return data;
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
    { label: "Net Cash in Bank", value: formatMYR(financials?.net_revenue || 0), icon: DollarSign },
    { label: "Active Subscribers", value: stats?.active_subscribers || 0, icon: Users },
    { label: "Past Due", value: stats?.past_due_subscribers || 0, icon: AlertTriangle, alert: (stats?.past_due_subscribers || 0) > 0 },
    { label: "Cancellation Rate", value: `${stats?.churn_rate_percentage || 0}%`, icon: Activity },
  ];

  // FIX: Check if the array is empty or if no gateways have a valid API key
  const showGatewayWarning = paymentConfigError || !paymentConfigs || paymentConfigs.length === 0 || !paymentConfigs.some(c => c.is_active && (c.has_api_key || c.has_secret_key));
  const showEmailWarning = emailConfigError || !emailConfig || !emailConfig.is_active || emailConfig.has_api_key === false;

  return (
    <PageLayout 
      title="Sales Insights" 
      description="High-density overview of your financial and retention health."
      breadcrumbs={[{ label: "Commerce" }, { label: "Dashboard" }]}
    >
      <div className="space-y-6">
        
        {showGatewayWarning && (
          <div className="flex items-center justify-between p-4 bg-rose-50 border border-rose-200">
            <div className="flex items-center gap-3">
              <AlertTriangle size={18} className="text-rose-600" />
              <div>
                <p className="text-[13px] font-bold text-rose-800">Action Required: Payment Gateway Not Configured</p>
                <p className="text-[12px] text-rose-700 mt-0.5">Your checkout links cannot accept payments. Customers will be unable to purchase your products.</p>
              </div>
            </div>
            <Link to="/workspace/payment-gateways" className="h-8 px-4 bg-rose-600 hover:bg-rose-700 text-white text-[11px] font-bold uppercase tracking-widest flex items-center gap-2 transition-colors">
              <CreditCard size={14} /> Configure Now
            </Link>
          </div>
        )}

        {showEmailWarning && (
          <div className="flex items-center justify-between p-4 bg-amber-50 border border-amber-200">
            <div className="flex items-center gap-3">
              <AlertTriangle size={18} className="text-amber-600" />
              <div>
                <p className="text-[13px] font-bold text-amber-800">Action Required: Configure Email Provider</p>
                <p className="text-[12px] text-amber-700 mt-0.5">You must connect a Resend API key to activate checkout links and send automated receipts.</p>
              </div>
            </div>
            <Link to="/workspace/email" className="h-8 px-4 bg-amber-600 hover:bg-amber-700 text-white text-[11px] font-bold uppercase tracking-widest flex items-center gap-2 transition-colors">
              <Mail size={14} /> Connect Email
            </Link>
          </div>
        )}

        <div className="grid grid-cols-2 lg:grid-cols-4 gap-4">
          {topMetrics.map((kpi, i) => (
            <div key={i} className={cn(
              "bg-white border p-4 flex flex-col justify-between",
              kpi.alert ? "border-amber-300 bg-amber-50/30" : "border-[#e5e5e5]"
            )}>
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
