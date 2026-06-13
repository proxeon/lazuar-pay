import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Users, DollarSign, Activity, AlertTriangle, Package, Loader2, Copy, Check, ChevronLeft, ChevronRight } from "lucide-react";
import { toast } from "sonner";
import { client, type CommunitySubscriptionDto } from "../lib/api-client";
import { BarChart, Bar, XAxis, Tooltip, ResponsiveContainer } from "recharts";
import { cn } from "../lib/utils";

export default function CommunityInsights() {
  const [page, setPage] = useState(1);
  const [statusFilter, setStatusFilter] = useState("ALL");
  const [copiedId, setCopiedId] = useState<string | null>(null);

  const { data: stats, isLoading: statsLoading } = useQuery({
    queryKey: ["community-stats"],
    queryFn: async () => {
      const { data, error } = await client.GET("/admin/community/stats" as any);
      if (error) throw new Error(error.detail);
      return data;
    }
  });

  const { data: financials, isLoading: financialsLoading } = useQuery({
    queryKey: ["financial-summary"],
    queryFn: async () => {
      const { data, error } = await client.GET("/admin/billing/summary" as any);
      if (error) throw new Error(error.detail);
      return data;
    }
  });

  const { data: plans, isLoading: plansLoading } = useQuery({
    queryKey: ["community-plans"],
    queryFn: async () => {
      const { data, error } = await client.GET("/admin/community/plans" as any);
      if (error) throw new Error(error.detail);
      return data;
    }
  });

  const { data: subscribersData, isLoading: subsLoading } = useQuery({
    queryKey: ["community-subscribers", page],
    queryFn: async () => {
      const { data, error } = await client.GET("/admin/community/subscribers" as any, {
        params: { query: { page, limit: 50 } }
      });
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

  const handleCopyId = (id: string) => {
    navigator.clipboard.writeText(id);
    setCopiedId(id);
    toast.success("ID Copied", { description: "Paste this ID into the Ops Chat to execute actions." });
    setTimeout(() => setCopiedId(null), 2000);
  };

  // Client-side filtering for the current paginated chunk
  const displayedSubscribers = (subscribersData?.data as CommunitySubscriptionDto[] || [])
    .filter(sub => statusFilter === "ALL" || sub.status === statusFilter);

  return (
    <div className="flex-1 overflow-y-auto bg-[#fafafa] p-6 md:p-8">
      <div className="max-w-6xl mx-auto space-y-6">
        
        <div>
          <h1 className="text-xl font-bold text-[#09090b]">Community Insights</h1>
          <p className="text-xs text-[#71717a] mt-1">High-density overview of your financial and retention health.</p>
        </div>

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

        {/* --- SUBSCRIBER LEDGER --- */}
        <div className="bg-white border border-[#e5e5e5] flex flex-col">
          <div className="px-5 py-4 border-b border-[#f4f4f5] flex items-center justify-between bg-[#fafafa]/50">
            <h3 className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">Subscriber Directory</h3>
            <select 
              value={statusFilter} 
              onChange={(e) => setStatusFilter(e.target.value)}
              className="h-8 px-2 text-[10px] font-bold uppercase tracking-widest bg-white border border-[#e5e5e5] text-[#09090b] focus:outline-none focus:border-[#09090b]"
            >
              <option value="ALL">ALL STATUSES</option>
              <option value="ACTIVE">ACTIVE</option>
              <option value="PAST_DUE">PAST DUE</option>
              <option value="CANCELLED">CANCELLED</option>
              <option value="EXPIRED">EXPIRED</option>
            </select>
          </div>

          <div className="overflow-x-auto min-h-[400px]">
            <table className="w-full text-left text-[13px]">
              <thead className="bg-white border-b border-[#f4f4f5]">
                <tr>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">ID</th>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Customer</th>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Plan</th>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Status</th>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] text-right">Period End</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#f4f4f5]">
                {subsLoading ? (
                  <tr>
                    <td colSpan={5} className="py-12 text-center text-[#a1a1aa]"><Loader2 size={20} className="animate-spin mx-auto" /></td>
                  </tr>
                ) : displayedSubscribers.length === 0 ? (
                  <tr>
                    <td colSpan={5} className="py-12 text-center text-[12px] text-[#71717a]">No subscribers found.</td>
                  </tr>
                ) : (
                  displayedSubscribers.map((sub) => (
                    <tr key={sub.id} className="hover:bg-[#fafafa]/50 transition-colors group">
                      <td className="px-5 py-3 whitespace-nowrap">
                        <div className="flex items-center gap-2">
                          <span className="font-mono text-[#71717a] text-[11px]">{sub.id.substring(0, 8)}...</span>
                          <button 
                            onClick={() => handleCopyId(sub.id)}
                            className="p-1 text-[#a1a1aa] hover:text-[#09090b] transition-colors rounded-sm opacity-0 group-hover:opacity-100"
                            title="Copy ID for Ops Chat"
                          >
                            {copiedId === sub.id ? <Check size={13} className="text-emerald-600" /> : <Copy size={13} />}
                          </button>
                        </div>
                      </td>
                      <td className="px-5 py-3 min-w-[200px]">
                        <p className="font-medium text-[#09090b] text-[13px]">{sub.customer_name}</p>
                        <p className="text-[11px] text-[#71717a]">{sub.customer_email}</p>
                      </td>
                      <td className="px-5 py-3 whitespace-nowrap">
                        <p className="text-[12px] text-[#09090b]">{sub.plan_name}</p>
                        <p className="text-[10px] font-mono text-[#71717a]">RM {sub.plan_price.toFixed(2)}</p>
                      </td>
                      <td className="px-5 py-3 whitespace-nowrap">
                        <span className={cn(
                          "text-[10px] font-bold uppercase tracking-widest",
                          sub.status === "ACTIVE" ? "text-emerald-600" :
                          sub.status === "PAST_DUE" ? "text-rose-600" :
                          sub.status === "CANCELLED" ? "text-amber-600" : "text-[#a1a1aa]"
                        )}>
                          {sub.status.replace("_", " ")}
                        </span>
                      </td>
                      <td className="px-5 py-3 whitespace-nowrap text-right font-mono text-[#52525b] text-[11px]">
                        {sub.current_period_end ? new Date(sub.current_period_end).toLocaleDateString('en-GB', { day: 'numeric', month: 'short', year: 'numeric' }) : '-'}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>

          <div className="px-5 py-3 border-t border-[#f4f4f5] bg-[#fafafa]/50 flex items-center justify-between">
            <span className="text-[10px] font-bold uppercase tracking-widest text-[#a1a1aa]">
              Page {subscribersData?.current_page || 1} of {subscribersData?.total_pages || 1}
            </span>
            <div className="flex items-center gap-2">
              <button 
                onClick={() => setPage(p => Math.max(1, p - 1))}
                disabled={page === 1 || subsLoading}
                className="h-7 px-2 border border-[#e5e5e5] bg-white hover:bg-[#f4f4f5] text-[#09090b] text-[11px] font-bold uppercase tracking-widest transition-colors disabled:opacity-50 flex items-center gap-1"
              >
                <ChevronLeft size={13} /> Prev
              </button>
              <button 
                onClick={() => setPage(p => p + 1)}
                disabled={!subscribersData || page >= (subscribersData.total_pages || 1) || subsLoading}
                className="h-7 px-2 border border-[#e5e5e5] bg-white hover:bg-[#f4f4f5] text-[#09090b] text-[11px] font-bold uppercase tracking-widest transition-colors disabled:opacity-50 flex items-center gap-1"
              >
                Next <ChevronRight size={13} />
              </button>
            </div>
          </div>
        </div>

      </div>
    </div>
  );
}
