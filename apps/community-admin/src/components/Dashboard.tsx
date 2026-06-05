import { useQuery, useQueryClient } from "@tanstack/react-query";
import { client } from "../lib/api-client";
import type { CommunityStatsResponse } from "../lib/api-client";
import { DollarSign, Users, Menu, Activity, RefreshCw, UserPlus, Zap } from "lucide-react";
import { BarChart, Bar, XAxis, YAxis, Tooltip, ResponsiveContainer, PieChart, Pie, Cell } from "recharts";

const PIE_COLORS = ['#0f172a', '#3b82f6', '#10b981', '#f59e0b', '#8b5cf6'];

export default function Dashboard({ isMobile, toggleSidebar }: any) {
  const queryClient = useQueryClient();

  const { data: stats, isLoading, isFetching } = useQuery<CommunityStatsResponse>({
    queryKey: ["community-stats"],
    queryFn: async () => {
      const { data, error } = await client.GET("/admin/community/stats");
      if (error) throw new Error(error.detail || "Failed to fetch stats");
      return data as CommunityStatsResponse;
    },
  });

  return (
    <div className="flex-1 w-full p-4 md:p-8 mx-auto max-w-[1240px] flex flex-col gap-6">
      <header className="flex items-center justify-between pb-2">
        <div className="flex items-center gap-3">
          {isMobile && (
            <button onClick={toggleSidebar} className="p-1.5 hover:bg-secondary rounded-none transition-colors">
              <Menu size={20} />
            </button>
          )}
          <div>
            <h1 className="text-[20px] font-semibold tracking-tight text-foreground">Analytics Overview</h1>
            <p className="text-[11px] font-bold uppercase tracking-[0.2em] text-muted-foreground mt-1">
              Your community financial pulse and retention metrics.
            </p>
          </div>
        </div>

        <button 
          onClick={() => queryClient.invalidateQueries({ queryKey: ["community-stats"] })}
          disabled={isFetching}
          className="p-2 border border-border/60 bg-card hover:bg-secondary rounded-none transition-colors text-foreground flex items-center justify-center disabled:opacity-50"
          title="Refresh Data"
        >
          <RefreshCw size={16} className={isFetching ? "animate-spin text-muted-foreground" : ""} />
        </button>
      </header>

      {isLoading || !stats ? (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 animate-pulse">
          {[...Array(8)].map((_, i) => (
            <div key={i} className="h-[130px] bg-card rounded-none border border-border/60 p-5 flex flex-col justify-between" />
          ))}
        </div>
      ) : (
        <>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            <StatCard title="Platform MRR" value={`RM ${stats.mrr.toFixed(2)}`} icon={DollarSign} description="Excludes Invoice-Only tracking" theme="default" />
            <StatCard title="Active Members" value={stats.active_subscribers} icon={Users} description="Subscribers with unhindered access" theme="active" />
            <StatCard title="Net New (30 Days)" value={stats.net_new_last_30_days > 0 ? `+${stats.net_new_last_30_days}` : stats.net_new_last_30_days} icon={UserPlus} description="New minus cancelled last 30 days" theme="default" />
            <StatCard title="Churn Rate (30 Days)" value={`${stats.churn_rate_percentage}%`} icon={Activity} description="Percentage of users lost" theme={stats.churn_rate_percentage > 10 ? "danger" : "default"} />
          </div>

          <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
            <StatCard title="Average Rev Per User" value={`RM ${stats.average_revenue_per_user.toFixed(2)}`} icon={DollarSign} description="ARPU across active platform users" theme="default" />
            <StatCard title="Total Cash Collected" value={`RM ${stats.total_revenue_collected.toFixed(2)}`} icon={DollarSign} description="All-time confirmed payments" theme="default" />
            <StatCard title="Reminder Effectiveness" value={`${stats.reminder_effectiveness_percentage}%`} icon={Zap} description="Paid within 48h of automated reminder" theme={stats.reminder_effectiveness_percentage > 50 ? "active" : "warning"} />
          </div>

          <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
            <div className="lg:col-span-2 bg-card border border-border/60 p-5 shadow-sm rounded-none flex flex-col">
              <h3 className="text-xs font-bold uppercase tracking-widest text-foreground mb-6">6-Month Cash Flow Trend</h3>
              <div className="flex-1 min-h-[250px] w-full">
                <ResponsiveContainer width="100%" height="100%">
                  <BarChart data={stats.cash_flow_trend} margin={{ top: 0, right: 0, left: -20, bottom: 0 }}>
                    <XAxis dataKey="month" axisLine={false} tickLine={false} tick={{ fontSize: 10, fill: '#64748b' }} dy={10} />
                    <YAxis axisLine={false} tickLine={false} tick={{ fontSize: 10, fill: '#64748b' }} tickFormatter={(val) => `RM${val}`} />
                    <Tooltip cursor={{ fill: 'rgba(0,0,0,0.05)' }} contentStyle={{ borderRadius: '0', border: '1px solid #e2e8f0', fontSize: '12px', fontWeight: 'bold' }} formatter={(val: number) => [`RM ${val.toFixed(2)}`, "Cash Collected"]} />
                    <Bar dataKey="amount" fill="#0f172a" radius={[2, 2, 0, 0]} maxBarSize={50} />
                  </BarChart>
                </ResponsiveContainer>
              </div>
            </div>

            <div className="lg:col-span-1 bg-card border border-border/60 p-5 shadow-sm rounded-none flex flex-col">
              <h3 className="text-xs font-bold uppercase tracking-widest text-foreground mb-2">Payment Methods</h3>
              <p className="text-[10px] text-muted-foreground mb-4">Breakdown of all-time transactions.</p>
              <div className="flex-1 min-h-[200px] w-full relative">
                <ResponsiveContainer width="100%" height="100%">
                  <PieChart>
                    <Pie data={stats.payment_methods} cx="50%" cy="50%" innerRadius={60} outerRadius={80} paddingAngle={2} dataKey="count" nameKey="method" stroke="none">
                      {stats.payment_methods.map((_, index) => <Cell key={`cell-${index}`} fill={PIE_COLORS[index % PIE_COLORS.length]} />)}
                    </Pie>
                    <Tooltip contentStyle={{ borderRadius: '0', border: '1px solid #e2e8f0', fontSize: '11px', fontWeight: 'bold' }} formatter={(val: number, name: string) => [`${val} transactions`, name.replace(/_/g, ' ')]} />
                  </PieChart>
                </ResponsiveContainer>
              </div>

              <div className="flex flex-col gap-2 mt-4">
                {stats.payment_methods.map((method, index) => (
                  <div key={method.method} className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      <div className="w-3 h-3 rounded-sm" style={{ backgroundColor: PIE_COLORS[index % PIE_COLORS.length] }} />
                      <span className="text-[11px] font-medium uppercase tracking-wider text-muted-foreground">{method.method.replace(/_/g, ' ')}</span>
                    </div>
                    <span className="text-xs font-bold font-mono">RM {method.total_amount.toFixed(0)}</span>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </>
      )}
    </div>
  );
}

interface StatCardProps {
  title: string;
  value: string | number;
  icon: any;
  description: string;
  theme?: "default" | "active" | "warning" | "danger";
}

function StatCard({ title, value, icon: Icon, description, theme = "default" }: StatCardProps) {
  let cardStyle = "bg-card border-border/60 hover:border-foreground/40 hover:shadow-[4px_4px_0px_0px_rgba(0,0,0,0.05)]";
  let iconWrapperStyle = "bg-secondary text-muted-foreground border-border/60";
  let textStyle = "text-foreground";

  if (theme === "active") {
    iconWrapperStyle = "bg-emerald-50 text-emerald-600 border-emerald-200/60 dark:bg-emerald-950/30 dark:border-emerald-900";
  } else if (theme === "warning") {
    cardStyle = "bg-[#fffdf5] dark:bg-amber-950/10 border-amber-200 hover:border-amber-300 shadow-[0_2px_8px_-3px_rgba(245,158,11,0.05)] hover:shadow-[4px_4px_0px_0px_rgba(245,158,11,0.1)]";
    iconWrapperStyle = "bg-amber-100/60 text-amber-600 border-amber-200/50 dark:bg-amber-900/30 dark:border-amber-800";
    textStyle = "text-amber-900 dark:text-amber-500";
  } else if (theme === "danger") {
    cardStyle = "bg-[#fffafa] dark:bg-red-950/10 border-red-100 hover:border-red-200 shadow-[0_2px_8px_-3px_rgba(239,68,68,0.05)] hover:shadow-[4px_4px_0px_0px_rgba(239,68,68,0.1)]";
    iconWrapperStyle = "bg-red-50 text-red-500 border-red-100 dark:bg-red-900/30 dark:border-red-800";
    textStyle = "text-red-900 dark:text-red-500";
  }

  return (
    <div className={`p-5 rounded-none border h-[130px] shadow-sm flex flex-col justify-between transition-all duration-200 ${cardStyle}`}>
      <div className="flex justify-between items-start gap-2">
        <div className="flex flex-col gap-0.5">
          <span className="text-[12px] font-medium text-muted-foreground leading-none truncate max-w-[140px]">{title}</span>
          <span className="text-[10px] text-muted-foreground/60 mt-1.5 line-clamp-1">{description}</span>
        </div>
        <div className={`p-1.5 rounded-none border flex items-center justify-center shrink-0 ${iconWrapperStyle}`}>
          <Icon className="w-4 h-4" />
        </div>
      </div>
      <div className={`text-[26px] font-bold tracking-tight ${textStyle}`}>{value}</div>
    </div>
  );
}
