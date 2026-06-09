import { ArrowUpRight, ArrowDownRight, TrendingUp, Users, DollarSign, Activity, Download, Menu } from "lucide-react";
import { AreaChart, Area, XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, BarChart, Bar } from 'recharts';
import { motion } from "motion/react";
import { cn } from "../lib/utils";

const data = [
  { name: 'Jan', revenue: 4000, users: 2400 },
  { name: 'Feb', revenue: 3000, users: 1398 },
  { name: 'Mar', revenue: 2000, users: 9800 },
  { name: 'Apr', revenue: 2780, users: 3908 },
  { name: 'May', revenue: 1890, users: 4800 },
  { name: 'Jun', revenue: 2390, users: 3800 },
  { name: 'Jul', revenue: 3490, users: 4300 },
];

const kpis = [
  { label: "Total Revenue", value: "$124,500", trend: "+12.5%", isPositive: true, icon: DollarSign },
  { label: "Active Users", value: "32,410", trend: "+8.2%", isPositive: true, icon: Users },
  { label: "Bounce Rate", value: "24.1%", trend: "-2.4%", isPositive: true, icon: Activity },
  { label: "Conversion", value: "4.8%", trend: "-1.1%", isPositive: false, icon: TrendingUp },
];

interface DashboardProps {
  isMobile?: boolean;
  toggleSidebar?: () => void;
}

export default function Dashboard({ isMobile, toggleSidebar }: DashboardProps) {
  return (
    <div className="flex-1 w-full p-4 md:p-8 mx-auto max-w-[1240px] flex flex-col gap-6">
      {/* Header */}
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
            <h1 className="text-[20px] font-semibold tracking-tight text-[#09090b]">Overview</h1>
            <p className="text-[13px] text-[#71717a] mt-0.5">Monitor your key performance indicators.</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <select className="h-8 bg-white border text-[13px] font-medium border-[#e5e5e5] rounded-md px-2.5 text-[#09090b] shadow-sm outline-none focus:ring-1 focus:ring-[#09090b] cursor-pointer hover:bg-[#fafafa] transition-colors">
            <option>Last 7 Days</option>
            <option>Last 30 Days</option>
            <option>This Year</option>
          </select>
          <button className="flex items-center gap-1.5 bg-[#09090b] text-white text-[13px] font-medium px-3 h-8 rounded-md shadow-sm hover:bg-[#27272a] transition-colors focus:outline-none focus:ring-2 focus:ring-[#09090b] focus:ring-offset-2 focus:ring-offset-white">
            <Download size={14} />
            Download
          </button>
        </div>
      </header>

      {/* KPI Grid */}
      <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
        {kpis.map((kpi, i) => (
          <motion.div
            initial={{ opacity: 0, y: 10 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ delay: i * 0.1, ease: "easeOut" }}
            key={kpi.label}
            className="flex flex-col justify-between bg-white p-5 rounded-lg border border-[#e5e5e5] h-[124px] shadow-[0_1px_2px_rgba(0,0,0,0.02)]"
          >
            <div className="flex justify-between items-start">
              <span className="text-[13px] font-medium text-[#71717a]">{kpi.label}</span>
              <kpi.icon className="w-[18px] h-[18px] text-[#a1a1aa]" />
            </div>
            <div>
              <div className="text-[24px] font-semibold tracking-tight text-[#09090b] mb-1 leading-none">
                {kpi.value}
              </div>
              <div className={cn(
                "flex items-center gap-1 text-[12px] font-medium",
                kpi.isPositive ? "text-emerald-600" : "text-rose-600"
              )}>
                {kpi.isPositive ? <ArrowUpRight className="w-3.5 h-3.5" /> : <ArrowDownRight className="w-3.5 h-3.5" />}
                {kpi.trend}
                <span className="text-[#a1a1aa] font-normal ml-0.5">vs last month</span>
              </div>
            </div>
          </motion.div>
        ))}
      </div>

      {/* Charts Area */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-4">
        {/* Main Chart */}
        <motion.div 
          initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.3, ease: "easeOut" }}
          className="lg:col-span-2 flex flex-col bg-white p-5 rounded-lg border border-[#e5e5e5] shadow-[0_1px_2px_rgba(0,0,0,0.02)] min-h-[360px]"
        >
          <div className="flex items-center justify-between mb-6">
            <h2 className="text-[14px] font-semibold text-[#09090b]">Revenue Growth</h2>
            <div className="text-[12px] font-medium text-[#71717a] flex items-center gap-1.5">
              <span className="w-2 h-2 rounded-full bg-[#09090b]" /> 
              Actual
              <span className="w-2 h-2 rounded-full border border-[#09090b] ml-2" /> 
              Target
            </div>
          </div>
          <div className="flex-1 w-full min-h-[250px]">
            <ResponsiveContainer width="100%" height="100%">
              <AreaChart data={data} margin={{ top: 10, right: 10, left: -25, bottom: 0 }}>
                <defs>
                  <linearGradient id="colorRevenue" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#09090b" stopOpacity={0.1}/>
                    <stop offset="95%" stopColor="#09090b" stopOpacity={0}/>
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#f4f4f5" />
                <XAxis dataKey="name" axisLine={false} tickLine={false} tick={{ fontSize: 11, fill: '#71717a', fontWeight: 500 }} dy={10} />
                <YAxis axisLine={false} tickLine={false} tick={{ fontSize: 11, fill: '#71717a', fontWeight: 500 }} />
                <Tooltip 
                  contentStyle={{ borderRadius: '6px', border: '1px solid #e5e5e5', boxShadow: '0 4px 12px rgba(0,0,0,0.05)', fontSize: '13px', backgroundColor: '#fff' }}
                  itemStyle={{ color: '#09090b', fontWeight: 600 }}
                  cursor={{ stroke: '#e5e5e5', strokeWidth: 1, strokeDasharray: '4 4' }}
                />
                <Area type="monotone" dataKey="revenue" stroke="#09090b" strokeWidth={2} fillOpacity={1} fill="url(#colorRevenue)" />
              </AreaChart>
            </ResponsiveContainer>
          </div>
        </motion.div>

        {/* Secondary Chart */}
        <motion.div 
          initial={{ opacity: 0, y: 10 }} animate={{ opacity: 1, y: 0 }} transition={{ delay: 0.4, ease: "easeOut" }}
          className="flex flex-col bg-white p-5 rounded-lg border border-[#e5e5e5] shadow-[0_1px_2px_rgba(0,0,0,0.02)] min-h-[360px]"
        >
          <div className="flex flex-col mb-6">
            <h2 className="text-[14px] font-semibold text-[#09090b]">User Acquisition</h2>
            <p className="text-[13px] text-[#71717a] mt-0.5">New signups by month</p>
          </div>
          <div className="flex-1 w-full min-h-[250px]">
            <ResponsiveContainer width="100%" height="100%">
              <BarChart data={data} margin={{ top: 10, right: 0, left: -25, bottom: 0 }} barSize={28}>
                <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="#f4f4f5" />
                <XAxis dataKey="name" axisLine={false} tickLine={false} tick={{ fontSize: 11, fill: '#71717a', fontWeight: 500 }} dy={10} />
                <YAxis axisLine={false} tickLine={false} tick={{ fontSize: 11, fill: '#71717a', fontWeight: 500 }} />
                <Tooltip
                   cursor={{ fill: '#fafafa' }}
                   contentStyle={{ borderRadius: '6px', border: '1px solid #e5e5e5', boxShadow: '0 4px 12px rgba(0,0,0,0.05)', fontSize: '13px', backgroundColor: '#fff' }}
                   itemStyle={{ color: '#09090b', fontWeight: 600 }}
                />
                <Bar dataKey="users" fill="#e4e4e7" radius={[4, 4, 0, 0]} activeBar={{ fill: '#09090b' }} />
              </BarChart>
            </ResponsiveContainer>
          </div>
        </motion.div>
      </div>
    </div>
  );
}
