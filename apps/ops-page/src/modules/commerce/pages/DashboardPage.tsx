import { useQuery } from "@tanstack/react-query";
import { Users, DollarSign, Activity, AlertTriangle, Package, Loader2, CreditCard } from "lucide-react";
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

  const { data: paymentConfig, error: paymentConfigError, isLoading: paymentConfigLoading } = useQuery({
    queryKey: ["payment-config-status"],
    queryFn: async () => {
      const { data, error, response } = await client.GET("/admin/commerce/payment-config");
      if (response.status === 404) return null;
      if (error) throw new Error(error.detail);
      return data;
    }
  });

  if (statsLoading || financialsLoading || productsLoading || paymentConfigLoading) {
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

  const showGatewayWarning = paymentConfigError || !paymentConfig || !paymentConfig.is_active;

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
            <Link to="/commerce/payment" className="h-8 px-4 bg-rose-600 hover:bg-rose-700 text-white text-[11px] font-bold uppercase tracking-widest flex items-center gap-2 transition-colors">
              <CreditCard size={14} /> Configure Now
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
            <h3 className="text-[11px] font-bold uppercase tracking-widest text-[#09090b] mb-6">Revenue Trend</h3>
            <div className="flex-1 w-full relative">
              <div className="absolute inset-0 flex items-center justify-center">
                 <span className="text-[11px] text-[#a1a1aa]">Not enough data to graph</span>
              </div>
            </div>
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
