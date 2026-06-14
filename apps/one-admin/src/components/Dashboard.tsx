import { ArrowUpRight, ArrowDownRight, TrendingUp, Users, DollarSign, Activity, Download, Menu, Loader2, AlertTriangle } from "lucide-react";
import { AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, BarChart, Bar } from 'recharts';
import { motion } from "motion/react";
import { cn } from "../lib/utils";
import { useQuery } from "@tanstack/react-query";
import { client } from "../lib/api-client";

const signupTrendData = [
  { name: 'Jan', clients: 120 },
  { name: 'Feb', clients: 180 },
  { name: 'Mar', clients: 240 },
  { name: 'Apr', clients: 310 },
  { name: 'May', clients: 450 },
  { name: 'Jun', clients: 680 },
  { name: 'Jul', clients: 1248 },
];

interface DashboardProps {
  isMobile?: boolean;
  toggleSidebar?: () => void;
}

export default function Dashboard({ isMobile, toggleSidebar }: DashboardProps) {
  const { data: financials, isLoading } = useQuery({
    queryKey: ["financial-summary"],
    queryFn: async () => {
      const { data, error } = await client.GET("/admin/billing/summary");
      if (error) throw new Error(error.detail);
      return data;
    }
  });

  const formatMYR = (val: number) => `RM ${val.toLocaleString('en-MY', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;

  const kpis = [
    { label: "Gross Revenue", value: financials ? formatMYR(financials.gross_revenue) : "RM 0.00", trend: "Catalog Value", isPositive: true, icon: DollarSign },
    { label: "Net Cash in Bank", value: financials ? formatMYR(financials.net_revenue) : "RM 0.00", trend: "Actual Deposits", isPositive: true, icon: TrendingUp },
    { label: "Gateway Fees Paid", value: financials ? formatMYR(financials.total_gateway_fees) : "RM 0.00", trend: "Phantom Money", isPositive: false, icon: Activity },
    { label: "Tax Liabilities", value: financials ? formatMYR(financials.total_tax_liabilities) : "RM 0.00", trend: "Owed to Gov", isPositive: false, icon: AlertTriangle },
  ];

  return (
    <div className="flex-1 w-full p-4 md:p-8 mx-auto max-w-[1240px] flex flex-col gap-6">
      <header className="flex flex-col md:flex-row md:items-center justify-between pb-2 gap-4">
        <div className="flex items-center gap-3">
          {isMobile && (
            <button
              onClick={toggleSidebar}
              className="p-1.5 -ml-1.5 rounded-md text-[#71717a] hover:bg-[#e5e5e5] hover:text-[#09090b] focus:outline-none transition-colors"
            >
              <Menu size={20} />
            </button>
          )}
          <div>
            <h1 className="text-[20px] font-semibold tracking-tight text-[#09090b]">Financial Overview</h1>
            <p className="text-[13px] text-[#71717a] mt-0.5">Double-entry ledger metrics and platform growth.</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <select className="h-8 bg-white border text-[13px] font-medium border-[#e5e5e5] rounded-none px-2.5 text-[#09090b] shadow-sm outline-none focus:ring-1 focus:ring-[#09090b] cursor-pointer hover:bg-[#fafafa] transition-colors">
            <option>All Time</option>
            <option>Last 30 Days</option>
            <option>This Year</option>
          </select>
          <button className="flex items-center gap-1.5 bg-[#09090b] text-white text-[13px] font-medium px-3 h-8 rounded-none shadow-sm hover:bg-[#27272a] transition-colors focus:outline-none focus:ring-2 focus:ring-[#09090b] focus:ring-offset-2 focus:ring-offset-white">
            <Download size={14} />
            Export Ledger
          </button>
        </div>
      </header>

      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {kpis.map((kpi, i) => (
          <motion.div
            initial={{ opacity: 0, y: 10 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: i * 0.05, ease: "easeOut" }}
            key={kpi.label}
            className="flex flex-col justify-between bg-white p-5 rounded-none border border-[#e5e5e5] h-[124px] shadow-[0_1px_2px_rgba(0,0,0,0.02)]"
          >
            <div className="flex justify-between items-start">
              <span className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">{kpi.label}</span>
              <kpi.icon className="w-[18px] h-[18px] text-[#a1a1aa]" />
            </div>
            <div>
              <div className="text-[22px] font-semibold tracking-tight text-[#09090b] mb-1 leading-none font-mono">
                {isLoading ? <Loader2 size={18} className="animate-spin" /> : kpi.value}
              </div>
              <div className={cn(
                "flex items-center gap-1 text-[11px] font-bold uppercase tracking-wider",
                kpi.isPositive ? "text-emerald-600" : "text-rose-600"
              )}>
                {kpi.isPositive ? <ArrowUpRight className="w-3.5 h-3.5" /> : <ArrowDownRight className="w-3.5 h-3.5" />}
                {kpi.trend}
              </div>
            </div>
          </motion.div>
        ))}
      </div>

      <motion.div
        initial={{ opacity: 0, y: 10 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ delay: 0.2, ease: "easeOut" }}
        className="flex flex-col bg-white p-5 rounded-none border border-[#e5e5e5] shadow-[0_1px_2px_rgba(0,0,0,0.02)] min-h-[380px]"
      >
        <div className="flex flex-col mb-6">
          <h2 className="text-[14px] font-semibold text-[#09090b]">Client Acquisition</h2>
          <p className="text-[13px] text-[#71717a] mt-0.5">Total registered client credentials over time</p>
        </div>
        <div className="flex-1 w-full min-h-[260px]">
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={signupTrendData} margin={{ top: 10, right: 0, left: -25, bottom: 0 }} barSize={32}>
              <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#f4f4f5" />
              <XAxis dataKey="name" axisLine={false} tickLine={false} tick={{ fontSize: 11, fill: '#71717a', fontWeight: 500 }} dy={10} />
              <YAxis axisLine={false} tickLine={false} tick={{ fontSize: 11, fill: '#71717a', fontWeight: 500 }} />
              <Tooltip
                cursor={{ fill: '#fafafa' }}
                contentStyle={{ borderRadius: '0', border: '1px solid #e5e5e5', boxShadow: '0 4px 12px rgba(0,0,0,0.05)', fontSize: '13px', backgroundColor: '#fff' }}
                itemStyle={{ color: '#09090b', fontWeight: 600 }}
              />
              <Bar dataKey="clients" fill="#e4e4e7" radius={0} activeBar={{ fill: '#09090b' }} />
            </BarChart>
          </ResponsiveContainer>
        </div>
      </motion.div>
    </div>
  );
}
