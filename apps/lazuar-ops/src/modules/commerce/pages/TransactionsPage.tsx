import { useEffect, useState } from "react";
import { useOutletContext } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { Loader2, ArrowLeft, ArrowRight, ShieldCheck, User, Search, Download } from "lucide-react";
import { API_URL } from "../../../lib/api-client";
import { toast } from "sonner";
import { client, type components } from "../../../lib/api-client";
import { cn } from "../../../lib/utils";
import PageLayout from "../../core/components/PageLayout";
import QuickCopy from "../../core/components/QuickCopy";
import { useDebounce } from "../../../hooks/use-debounce";
import TransactionDetailPanel from "../components/TransactionDetailPanel";
import { statusBadgeClass, statusLabel } from "../components/transactionStatus";

type TransactionLogDto = components["schemas"]["Commerce.TransactionLogDto"];

export default function TransactionsPage() {
  const { activeWorkspaceId } = useOutletContext<{ activeWorkspaceId: string | null }>();
  const [page, setPage] = useState(1);
  const [statusFilter, setStatusFilter] = useState("ALL");
  const [methodFilter, setMethodFilter] = useState("ALL");
  const [searchTerm, setSearchTerm] = useState("");
  
  const [selectedTransaction, setSelectedTransaction] = useState<TransactionLogDto | null>(null);

  const debouncedSearchTerm = useDebounce(searchTerm, 300);
  const limit = 50;

  const { data: response, isLoading } = useQuery({
    queryKey: ["commerce-transactions", page, statusFilter, methodFilter, debouncedSearchTerm],
    queryFn: async () => {
      const { data, error } = await client.GET("/admin/commerce/transactions", {
        params: { 
          query: { 
            page, 
            limit,
            search: debouncedSearchTerm || undefined,
            status: statusFilter === "ALL" ? undefined : statusFilter,
            gateway_name: methodFilter === "ALL" ? undefined : methodFilter
          } 
        }
      });
      if (error) throw new Error(error.detail);
      return data;
    },
    enabled: !!activeWorkspaceId,
    refetchInterval: (query) => {
      const rows = query.state.data?.data ?? [];
      return rows.some((tx) => tx.status === "REFUND_PENDING") || selectedTransaction?.status === "REFUND_PENDING" ? 2000 : false;
    }
  });

  useEffect(() => {
    if (!selectedTransaction || !response?.data) return;
    const next = response.data.find((tx) => tx.id === selectedTransaction.id);
    if (next && (next.status !== selectedTransaction.status || next.refunded_amount !== selectedTransaction.refunded_amount)) {
      setSelectedTransaction(next);
    }
  }, [response, selectedTransaction]);

  const handlePrev = () => setPage((p) => Math.max(1, p - 1));
  const handleNext = () => {
    if (response && page < response.total_pages) {
      setPage((p) => p + 1);
    }
  };

  const handleFilterChange = (setter: React.Dispatch<React.SetStateAction<string>>, value: string) => {
    setter(value);
    setPage(1); 
  };

  return (
    <PageLayout 
      title="Transaction Logs" 
      description="Audit Hub-recorded money rows. Match external_reference to Billplz bill id / Stripe PaymentIntent / CHIP id. Fees are Hub-recorded, not the bank payout file."
      breadcrumbs={[{ label: "Commerce", href: "/commerce/dashboard" }, { label: "Transactions" }]}
    >
      <div className="bg-white border border-[#e5e5e5] rounded-none flex flex-col h-full min-h-[600px]">
        <div className="px-5 py-4 border-b border-[#f4f4f5] flex items-center justify-between bg-[#fafafa]/50">
          <div className="flex items-center gap-3">
            <div className="relative w-64">
              <Search size={14} className="absolute left-3 top-2 text-[#a1a1aa]" />
              <input 
                type="text" 
                placeholder="Search ref ID or customer..." 
                value={searchTerm}
                onChange={e => {
                  setSearchTerm(e.target.value);
                  setPage(1);
                }}
                className="w-full h-8 pl-9 pr-3 text-[12px] bg-white border border-[#e5e5e5] focus:outline-none focus:border-[#09090b]" 
              />
            </div>

            <select 
              value={statusFilter} 
              onChange={(e) => handleFilterChange(setStatusFilter, e.target.value)}
              className="h-8 px-2 text-[10px] font-bold uppercase tracking-widest bg-white border border-[#e5e5e5] text-[#09090b] focus:outline-none focus:border-[#09090b]"
            >
              <option value="ALL">ALL STATUSES</option>
              <option value="CONFIRMED">CONFIRMED</option>
              <option value="REFUND_PENDING">PENDING REFUND</option>
              <option value="PARTIALLY_REFUNDED">PARTIALLY REFUNDED</option>
              <option value="REFUNDED">REFUNDED</option>
              <option value="REFUND_FAILED">REFUND FAILED</option>
            </select>
            
            <select 
              value={methodFilter} 
              onChange={(e) => handleFilterChange(setMethodFilter, e.target.value)}
              className="h-8 px-2 text-[10px] font-bold uppercase tracking-widest bg-white border border-[#e5e5e5] text-[#09090b] focus:outline-none focus:border-[#09090b]"
            >
              <option value="ALL">ALL GATEWAYS</option>
              <option value="STRIPE">STRIPE</option>
              <option value="CHIP">CHIP</option>
              <option value="BILLPLZ">BILLPLZ</option>
              <option value="RAZORPAY">RAZORPAY</option>
              <option value="XENDIT">XENDIT</option>
              <option value="OFFLINE">OFFLINE</option>
            </select>
            <button
              type="button"
              className="h-8 px-3 text-[10px] font-bold uppercase tracking-widest bg-white border border-[#e5e5e5] inline-flex items-center gap-1.5 hover:border-[#09090b]"
              onClick={async () => {
                const to = new Date();
                const from = new Date(to.getTime() - 31 * 24 * 60 * 60 * 1000);
                const params = new URLSearchParams({
                  from: from.toISOString(),
                  to: to.toISOString(),
                });
                if (statusFilter !== "ALL") params.set("status", statusFilter);
                const res = await fetch(`${API_URL}/admin/commerce/transactions/export?${params}`, {
                  credentials: "include",
                  headers: { "X-Tenant-Id": activeWorkspaceId || "" },
                });
                if (!res.ok) {
                  toast.error("Export failed.");
                  return;
                }
                const blob = await res.blob();
                const url = URL.createObjectURL(blob);
                const a = document.createElement("a");
                a.href = url;
                a.download = `transactions_${from.toISOString().slice(0, 10)}_${to.toISOString().slice(0, 10)}.csv`;
                a.click();
                URL.revokeObjectURL(url);
              }}
            >
              <Download size={12} /> CSV
            </button>
          </div>
          
          <div className="text-[11px] text-[#71717a] font-mono">
            {response ? `Total: ${response.total_count}` : "..."}
          </div>
        </div>

        <div className="w-full overflow-x-auto flex-1">
          <table className="w-full text-left text-[13px] min-w-[900px]">
            <thead className="bg-white border-b border-[#f4f4f5]">
              <tr>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Date & Time</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Customer</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Amount</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Status / Method</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Ref ID</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Recorded By</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#f4f4f5]">
              {isLoading ? (
                <tr><td colSpan={6} className="py-12 text-center text-[#a1a1aa]"><Loader2 size={20} className="animate-spin mx-auto" /></td></tr>
              ) : response?.data.length === 0 ? (
                <tr><td colSpan={6} className="py-12 text-center text-[12px] text-[#71717a]">No transactions found for the given criteria.</td></tr>
              ) : (
                response?.data.map((tx) => {
                  const isSystem = tx.recorded_by_name?.includes("System") || tx.recorded_by_name?.includes("AI Agent");
                  
                  return (
                    <tr 
                      key={tx.id} 
                      onClick={() => setSelectedTransaction(tx)}
                      className="hover:bg-[#fafafa] transition-colors cursor-pointer group"
                    >
                      <td className="px-5 py-3.5 whitespace-nowrap">
                        <p className="text-[12px] font-medium text-[#09090b]">
                          {new Date(tx.created_at).toLocaleDateString('en-GB')}
                        </p>
                        <p className="text-[10px] font-mono text-[#71717a] mt-0.5">
                          {new Date(tx.created_at).toLocaleTimeString('en-GB')}
                        </p>
                      </td>
                      <td className="px-5 py-3.5 min-w-[200px]">
                        <p className="font-medium text-[#09090b] text-[13px] group-hover:text-blue-600 transition-colors">{tx.customer_name}</p>
                        <p className="text-[11px] text-[#71717a] mt-0.5">{tx.customer_email}</p>
                      </td>
                      <td className="px-5 py-3.5 whitespace-nowrap">
                        <p className={cn(
                          "font-mono font-bold text-[13px]",
                          tx.status === "REFUNDED" ? "text-amber-600 line-through" : "text-[#09090b]"
                        )}>
                          RM {tx.amount.toFixed(2)}
                        </p>
                        {(tx.refunded_amount ?? 0) > 0 && tx.status !== "REFUNDED" && (
                          <p className="text-[10px] font-mono text-amber-700 mt-0.5">− RM {tx.refunded_amount.toFixed(2)}</p>
                        )}
                      </td>
                      <td className="px-5 py-3.5 whitespace-nowrap">
                        <span className={cn(
                          "text-[9px] px-1.5 py-0.5 border font-bold uppercase tracking-widest",
                          statusBadgeClass(tx.status)
                        )}>
                          {statusLabel(tx.status, tx.refunded_amount)}
                        </span>
                        <p className="text-[10px] text-[#71717a] font-medium mt-1.5">
                          {tx.recorded_by_name}{tx.gateway_name ? ` · ${tx.gateway_name}` : ""}
                        </p>
                      </td>
                      <td className="px-5 py-3.5">
                        <div className="flex flex-col gap-1">
                          <div className="flex items-center gap-1.5">
                            <span className="text-[12px] font-mono font-bold text-[#09090b]">
                              {tx.id.substring(0,8)}...
                            </span>
                            <QuickCopy text={tx.id} iconSize={10} className="opacity-0 group-hover:opacity-100 p-0.5" />
                          </div>
                          {tx.external_reference && (
                            <div className="flex items-center gap-1.5">
                              <span className="text-[10px] font-mono text-[#71717a] truncate max-w-[120px]" title={tx.external_reference}>
                                {tx.external_reference}
                              </span>
                              <QuickCopy text={tx.external_reference || ""} iconSize={10} className="opacity-0 group-hover:opacity-100 p-0.5" />
                            </div>
                          )}
                        </div>
                      </td>
                      <td className="px-5 py-3.5 whitespace-nowrap">
                        <div className="flex items-center gap-2">
                          {isSystem ? <ShieldCheck size={14} className="text-blue-600" /> : <User size={14} className="text-[#71717a]" />}
                          <span className={cn("text-[12px] font-medium", isSystem ? "text-blue-700" : "text-[#09090b]")}>
                            {tx.recorded_by_name}
                          </span>
                        </div>
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>

        <div className="px-5 py-3 border-t border-[#e5e5e5] bg-[#fafafa]/50 flex items-center justify-between shrink-0">
          <span className="text-[11px] text-[#71717a]">
            Page {page} of {response?.total_pages || 1}
          </span>
          <div className="flex items-center gap-2">
            <button 
              onClick={handlePrev} 
              disabled={page <= 1 || isLoading}
              className="h-8 px-3 border border-[#e5e5e5] bg-white text-[#09090b] text-[11px] font-bold uppercase tracking-widest hover:bg-[#f4f4f5] transition-colors disabled:opacity-50 flex items-center gap-1.5 rounded-sm"
            >
              <ArrowLeft size={14} /> Prev
            </button>
            <button 
              onClick={handleNext} 
              disabled={!response || page >= response.total_pages || isLoading}
              className="h-8 px-3 border border-[#e5e5e5] bg-white text-[#09090b] text-[11px] font-bold uppercase tracking-widest hover:bg-[#f4f4f5] transition-colors disabled:opacity-50 flex items-center gap-1.5 rounded-sm"
            >
              Next <ArrowRight size={14} />
            </button>
          </div>
        </div>
      </div>

      <TransactionDetailPanel
        transaction={selectedTransaction}
        onClose={() => setSelectedTransaction(null)}
        onUpdate={setSelectedTransaction}
      />
    </PageLayout>
  );
}
