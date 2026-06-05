import { useState, useMemo } from "react";
import { Download, ReceiptText, Filter, SearchX } from "lucide-react";
import { toast } from "sonner";
import { cn } from "../lib/utils";

type TransactionStatus = "PAID" | "REFUNDED" | "FAILED";
interface Transaction { id: string; date: string; amount: number; currency: string; tenantName: string; productName: string; status: TransactionStatus; receiptId: string; }

const mockTransactions: Transaction[] = [
  { id: "tx_001", date: "2025-01-15T10:30:00Z", amount: 99.00, currency: "MYR", tenantName: "Design Masters HQ", productName: "Founders Mastermind (Monthly)", status: "PAID", receiptId: "RCP-DM-8821" },
  { id: "tx_002", date: "2025-01-12T14:15:00Z", amount: 299.00, currency: "MYR", tenantName: "CodeCrafters Academy", productName: "Fullstack Engineering Bootcamp", status: "PAID", receiptId: "RCP-CA-9932" },
  { id: "tx_003", date: "2024-12-15T10:30:00Z", amount: 99.00, currency: "MYR", tenantName: "Design Masters HQ", productName: "Founders Mastermind (Monthly)", status: "PAID", receiptId: "RCP-DM-8744" },
  { id: "tx_004", date: "2024-12-01T09:00:00Z", amount: 49.00, currency: "MYR", tenantName: "Growth Hackers", productName: "SEO Deep Dive (2025 Edition)", status: "REFUNDED", receiptId: "RCP-GH-1102" },
  { id: "tx_005", date: "2024-11-15T10:30:00Z", amount: 99.00, currency: "MYR", tenantName: "Design Masters HQ", productName: "Founders Mastermind (Monthly)", status: "FAILED", receiptId: "RCP-DM-8610" }
];

export default function Ledger() {
  const [tenantFilter, setTenantFilter] = useState<string>("ALL");
  const [statusFilter, setStatusFilter] = useState<string>("ALL");

  const uniqueTenants = useMemo(() => Array.from(new Set(mockTransactions.map(t => t.tenantName))), []);
  const filteredTransactions = useMemo(() => mockTransactions.filter(tx => (tenantFilter === "ALL" || tx.tenantName === tenantFilter) && (statusFilter === "ALL" || tx.status === statusFilter)), [tenantFilter, statusFilter]);

  const formatDate = (isoString: string) => new Date(isoString).toLocaleDateString("en-MY", { year: 'numeric', month: 'short', day: 'numeric' });

  return (
    <div className="flex-1 w-full p-4 md:p-8 mx-auto max-w-[1000px] flex flex-col gap-6 animate-in fade-in duration-300">
      <header className="flex flex-col pb-2 border-b border-[#e5e5e5]">
        <h1 className="text-[20px] font-semibold tracking-tight text-[#09090b]">Global Receipt Ledger</h1>
        <p className="text-[13px] text-[#71717a] mt-1 font-mono uppercase tracking-wider">A unified history of all your transactions.</p>
      </header>

      <div className="bg-white border border-[#e5e5e5] rounded-none shadow-[4px_4px_0px_0px_rgba(0,0,0,0.05)] overflow-hidden flex flex-col">
        <div className="px-5 py-4 border-b border-[#f4f4f5] bg-[#fafafa]/50 flex flex-col sm:flex-row sm:items-center justify-between gap-4">
          <div className="flex items-center gap-2">
            <ReceiptText size={16} className="text-[#a1a1aa]" />
            <h2 className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">Transaction History</h2>
          </div>
          
          <div className="flex items-center gap-3">
            <div className="relative">
              <Filter size={12} className="absolute left-3 top-2 text-[#a1a1aa]" />
              <select value={tenantFilter} onChange={(e) => setTenantFilter(e.target.value)} className="h-8 pl-8 pr-3 text-[11px] font-bold uppercase tracking-widest bg-white border border-[#e5e5e5] rounded-none text-[#09090b] shadow-sm outline-none focus:ring-1 focus:ring-[#09090b] appearance-none cursor-pointer">
                <option value="ALL">All Providers</option>
                {uniqueTenants.map(t => <option key={t} value={t}>{t}</option>)}
              </select>
            </div>
            <select value={statusFilter} onChange={(e) => setStatusFilter(e.target.value)} className="h-8 px-3 text-[11px] font-bold uppercase tracking-widest bg-white border border-[#e5e5e5] rounded-none text-[#09090b] shadow-sm outline-none focus:ring-1 focus:ring-[#09090b] appearance-none cursor-pointer">
              <option value="ALL">All Statuses</option>
              <option value="PAID">Paid</option>
              <option value="REFUNDED">Refunded</option>
              <option value="FAILED">Failed</option>
            </select>
          </div>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full text-left text-[13px]">
            <thead>
              <tr className="border-b border-[#e5e5e5] bg-[#fafafa]">
                <th className="h-10 px-5 font-bold uppercase tracking-widest text-[#71717a] text-[10px]">Date</th>
                <th className="h-10 px-5 font-bold uppercase tracking-widest text-[#71717a] text-[10px]">Description</th>
                <th className="h-10 px-5 font-bold uppercase tracking-widest text-[#71717a] text-[10px]">Amount</th>
                <th className="h-10 px-5 font-bold uppercase tracking-widest text-[#71717a] text-[10px]">Status</th>
                <th className="h-10 px-5 font-bold uppercase tracking-widest text-[#71717a] text-[10px] text-right">Receipt</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#f4f4f5]">
              {filteredTransactions.length === 0 ? (
                <tr>
                  <td colSpan={5} className="py-12 text-center">
                    <SearchX className="h-8 w-8 text-[#a1a1aa] mx-auto mb-3" />
                    <p className="text-[13px] font-bold uppercase tracking-widest text-[#09090b]">No transactions found</p>
                  </td>
                </tr>
              ) : (
                filteredTransactions.map((tx) => (
                  <tr key={tx.id} className="hover:bg-[#fafafa]/50 transition-colors">
                    <td className="p-5 whitespace-nowrap text-[#52525b] font-mono">{formatDate(tx.date)}</td>
                    <td className="p-5">
                      <p className="font-bold text-[#09090b]">{tx.productName}</p>
                      <p className="text-[10px] font-mono uppercase tracking-widest text-[#71717a] mt-0.5">via {tx.tenantName}</p>
                    </td>
                    <td className="p-5 whitespace-nowrap font-mono font-bold text-[#09090b]">{tx.currency} {tx.amount.toFixed(2)}</td>
                    <td className="p-5 whitespace-nowrap">
                      <span className={cn("inline-flex items-center px-2 py-0.5 rounded-none border text-[9px] font-bold uppercase tracking-widest", tx.status === "PAID" ? "bg-emerald-50 text-emerald-700 border-emerald-200" : tx.status === "REFUNDED" ? "bg-zinc-100 text-zinc-600 border-zinc-200" : "bg-rose-50 text-rose-700 border-rose-200")}>
                        {tx.status}
                      </span>
                    </td>
                    <td className="p-5 whitespace-nowrap text-right">
                      {tx.status === "PAID" || tx.status === "REFUNDED" ? (
                        <button onClick={() => toast.success("Download started", { description: `Downloading PDF receipt for ${tx.receiptId}...`})} className="inline-flex items-center justify-center h-8 w-8 rounded-none border border-transparent text-[#71717a] hover:text-[#09090b] hover:bg-[#e5e5e5] hover:border-[#a1a1aa] transition-colors focus:outline-none">
                          <Download size={14} />
                        </button>
                      ) : <span className="text-[10px] text-[#a1a1aa] font-mono pr-2">N/A</span>}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}
