import { useQuery } from "@tanstack/react-query";
import { Users, DollarSign, Activity, AlertTriangle, Package, Loader2 } from "lucide-react";
import { client } from "../../../lib/api-client";
import { BarChart, Bar, XAxis, Tooltip, ResponsiveContainer } from "recharts";
import { cn } from "../../../lib/utils";
import PageLayout from "../../core/components/PageLayout";

export default function DashboardPage() {
  const { data: stats, isLoading: statsLoading } = useQuery({
    queryKey: ["community-stats"],
    queryFn: async () => {
      const { data, error } = await client.GET("/admin/community/stats");
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

  const { data: plans, isLoading: plansLoading } = useQuery({
    queryKey: ["community-plans"],
    queryFn: async () => {
      const { data, error } = await client.GET("/admin/community/plans");
      if (error) throw new Error(error.detail);
      return data;
    }
  });

  if (statsLoading || financialsLoading || plansLoading) {
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

  return (
    <PageLayout 
      title="Community Insights" 
      description="High-density overview of your financial and retention health."
      breadcrumbs={[{ label: "Community" }, { label: "Dashboard" }]}
    >
      <div className="space-y-6">
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
              <div className="absolute inset-0">
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={stats?.cash_flow_trend || []} margin={{ top: 0, right: 0, left: 0, bottom: 0 }} barSize={32}>
                    <XAxis dataKey="month" axisLine={false} tickLine={false} tick={{ fontSize: 10, fill: '#71717a' }} dy={10} />
                    <Tooltip
                      cursor={{ fill: '#fafafa' }}
                      contentStyle={{ borderRadius: '0', border: '1px solid #e5e5e5', boxShadow: 'none', fontSize: '12px' }}
                    />
                    <Bar dataKey="amount" fill="#e4e4e7" activeBar={{ fill: '#09090b' }} />
                  </BarChart>
                </ResponsiveContainer>
              </div>
            </div>
          </div>

          <div className="lg:col-span-2 bg-white border border-[#e5e5e5] flex flex-col h-[320px]">
            <div className="px-5 py-4 border-b border-[#f4f4f5] flex items-center gap-2 bg-[#fafafa]/50">
              <Package size={14} className="text-[#a1a1aa]" />
              <h3 className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">Active Plans Performance</h3>
            </div>
            <div className="flex-1 overflow-auto">
              <table className="w-full text-left text-[12px]">
                <thead className="sticky top-0 bg-white border-b border-[#f4f4f5]">
                  <tr>
                    <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Plan Name</th>
                    <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Price</th>
                    <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Interval</th>
                    <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] text-right">Enrolled</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[#f4f4f5]">
                  {(plans || []).map((plan: any) => (
                    <tr key={plan.id} className="hover:bg-[#fafafa]/50 transition-colors">
                      <td className="px-5 py-3.5 font-medium text-[#09090b]">
                        {plan.name}
                        {plan.is_full && <span className="ml-2 text-[9px] px-1.5 py-0.5 bg-rose-50 text-rose-600 border border-rose-200">FULL</span>}
                      </td>
                      <td className="px-5 py-3.5 font-mono text-[#52525b]">RM {plan.price.toFixed(2)}</td>
                      <td className="px-5 py-3.5 text-[#52525b] uppercase text-[10px]">{plan.interval}</td>
                      <td className="px-5 py-3.5 text-right font-mono font-bold text-[#09090b]">
                        {plan.enrolled_count} <span className="text-[#a1a1aa] font-normal text-[10px]">/ {plan.max_capacity || '∞'}</span>
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
