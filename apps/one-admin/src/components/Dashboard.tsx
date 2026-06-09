import { ArrowUpRight, Users, Download, Menu } from "lucide-react";
import { XAxis, YAxis, CartesianGrid, Tooltip, ResponsiveContainer, BarChart, Bar } from 'recharts';
import { motion } from "motion/react";

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
            <p className="text-[13px] text-[#71717a] mt-0.5">Platform user growth and client signups.</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          <select className="h-8 bg-white border text-[13px] font-medium border-[#e5e5e5] rounded-none px-2.5 text-[#09090b] shadow-sm outline-none focus:ring-1 focus:ring-[#09090b] cursor-pointer hover:bg-[#fafafa] transition-colors">
            <option>Last 7 Days</option>
            <option>Last 30 Days</option>
            <option>This Year</option>
          </select>
          <button className="flex items-center gap-1.5 bg-[#09090b] text-white text-[13px] font-medium px-3 h-8 rounded-none shadow-sm hover:bg-[#27272a] transition-colors focus:outline-none focus:ring-2 focus:ring-[#09090b] focus:ring-offset-2 focus:ring-offset-white">
            <Download size={14} />
            Download
          </button>
        </div>
      </header>

      {/* KPI Grid (Single Focused Card for Client Registry) */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <motion.div
          initial={{ opacity: 0, y: 10 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ duration: 0.2, ease: "easeOut" }}
          className="flex flex-col justify-between bg-white p-5 rounded-none border border-[#e5e5e5] h-[124px] shadow-[0_1px_2px_rgba(0,0,0,0.02)]"
        >
          <div className="flex justify-between items-start">
            <span className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Client Registry</span>
            <Users className="w-[18px] h-[18px] text-[#a1a1aa]" />
          </div>
          <div>
            <div className="text-[24px] font-semibold tracking-tight text-[#09090b] mb-1 leading-none font-mono">
              1,248
            </div>
            <div className="flex items-center gap-1 text-[12px] font-semibold text-emerald-600">
              <ArrowUpRight className="w-3.5 h-3.5" />
              +14.2%
              <span className="text-[#a1a1aa] font-normal ml-0.5">vs last month</span>
            </div>
          </div>
        </motion.div>
      </div>

      {/* Full-width Registration Trend Chart */}
      <motion.div 
        initial={{ opacity: 0, y: 10 }} 
        animate={{ opacity: 1, y: 0 }} 
        transition={{ delay: 0.1, ease: "easeOut" }}
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
