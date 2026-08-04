import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2, DollarSign, RotateCcw, AlertTriangle, X } from "lucide-react";
import { toast } from "sonner";
import { client, type components } from "../../../lib/api-client";
import { cn } from "../../../lib/utils";
import SidePanel from "../../core/components/SidePanel";
import QuickCopy from "../../core/components/QuickCopy";

type TransactionLogDto = components["schemas"]["Commerce.TransactionLogDto"];

interface TransactionDetailPanelProps {
  transaction: TransactionLogDto | null;
  onClose: () => void;
  onUpdate: (transaction: TransactionLogDto | null) => void;
}

export default function TransactionDetailPanel({ transaction, onClose, onUpdate }: TransactionDetailPanelProps) {
  const queryClient = useQueryClient();
  const [isRefundModalOpen, setIsRefundModalOpen] = useState(false);
  const [refundReason, setRefundReason] = useState("");

  const refundMutation = useMutation({
    mutationFn: async () => {
      if (!transaction?.id) throw new Error("Missing transaction id for refund.");

      const { error } = await client.POST("/admin/commerce/transactions/{id}/refund", {
        params: { path: { id: transaction.id } },
        body: {},
      });
      if (error) throw new Error((error as any).detail || "Refund failed");
    },
    onSuccess: () => {
      toast.success("Refund requested successfully");
      queryClient.invalidateQueries({ queryKey: ["commerce-transactions"] });
      queryClient.invalidateQueries({ queryKey: ["financial-summary"] });
      setIsRefundModalOpen(false);
      // Status flips to REFUNDED when GatewayRefundCompleted is processed; optimistically mark requested.
      onUpdate(transaction ? { ...transaction, status: "REFUNDED" } : null);
    },
    onError: (err: any) => {
      toast.error("Refund failed", { description: err.message });
      setIsRefundModalOpen(false);
    }
  });

  if (!transaction) return null;

  const isRefunded = transaction.status === "REFUNDED";
  const isRefundable = transaction.status === "CONFIRMED" && transaction.amount > 0;

  return (
    <>
      <SidePanel
        isOpen={!!transaction}
        onClose={onClose}
        title="Transaction Record"
        disableOutsideClick={refundMutation.isPending}
      >
        <div className="space-y-8 animate-in fade-in duration-200">
          <div className="flex items-start justify-between border-b border-[#f4f4f5] pb-6">
            <div>
              <h3 className={cn("text-2xl font-bold tracking-tight font-mono", isRefunded ? "text-amber-600 line-through" : "text-[#09090b]")}>
                RM {transaction.amount.toFixed(2)}
              </h3>
              <div className="flex items-center gap-2 mt-1.5">
                <span className={cn(
                  "text-[10px] px-2 py-0.5 border font-bold uppercase tracking-widest whitespace-nowrap",
                  isRefunded ? "bg-amber-50 text-amber-700 border-amber-200" : "bg-emerald-50 text-emerald-700 border-emerald-200"
                )}>
                  {transaction.status}
                </span>
                <span className="text-[11px] text-[#71717a] font-mono">
                  {new Date(transaction.created_at).toLocaleString('en-GB')}
                </span>
              </div>
            </div>
            <div className="h-12 w-12 bg-[#f4f4f5] border border-[#e5e5e5] flex items-center justify-center rounded-none shrink-0">
               <DollarSign size={20} className="text-[#09090b]" />
            </div>
          </div>

          <div className="space-y-4">
            <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Customer Details</h4>
            <div className="space-y-3 text-[12px]">
              <div>
                <span className="text-[#a1a1aa] block mb-0.5">Name</span>
                <span className="font-semibold text-[#09090b] text-[13px]">{transaction.customer_name}</span>
              </div>
              <div>
                <span className="text-[#a1a1aa] block mb-0.5">Email Address</span>
                <div className="flex items-center gap-2">
                  <a href={`mailto:${transaction.customer_email}`} className="font-medium text-blue-600 hover:opacity-85 transition-opacity underline underline-offset-2">
                    {transaction.customer_email}
                  </a>
                  <QuickCopy text={transaction.customer_email} iconSize={11} className="hover:bg-[#fafafa]" />
                </div>
              </div>
            </div>
          </div>

          <div className="space-y-4">
            <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Financial Breakdown</h4>
            <div className="bg-[#fafafa] border border-[#e5e5e5] p-4 space-y-3">
              <div className="flex justify-between items-center text-[12px]">
                <span className="text-[#71717a]">Gross Amount</span>
                <span className="font-mono text-[#09090b]">RM {transaction.amount.toFixed(2)}</span>
              </div>
              <div className="flex justify-between items-center text-[12px]">
                <span className="text-[#71717a]">Gateway Fee</span>
                <span className="font-mono text-rose-600">- RM {transaction.fee_amount.toFixed(2)}</span>
              </div>
              <div className="border-t border-[#e5e5e5] pt-2 flex justify-between items-center text-[13px]">
                <span className="font-bold text-[#09090b]">Net Cash Settled</span>
                <span className="font-mono font-bold text-emerald-700">RM {transaction.net_amount.toFixed(2)}</span>
              </div>
            </div>
          </div>

          <div className="space-y-4">
            <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Ledger Context</h4>
            <div className="grid grid-cols-1 gap-4 text-[12px]">
              <div>
                <span className="text-[#a1a1aa] block mb-1">Product / Offer</span>
                <span className="font-medium text-[#09090b]">{transaction.product_name || "Unknown Product"}</span>
              </div>
              <div>
                <span className="text-[#a1a1aa] block mb-1">Payment Method</span>
                <span className="font-mono text-[#09090b]">{transaction.payment_method}</span>
              </div>
              <div>
                <span className="text-[#a1a1aa] block mb-1">Internal Reference ID</span>
                <div className="flex items-center gap-2">
                  <span className="font-mono text-[#52525b] truncate max-w-[280px]">{transaction.id}</span>
                  <QuickCopy text={transaction.id} iconSize={11} className="hover:bg-[#fafafa]" />
                </div>
              </div>
              {transaction.external_reference && (
                <div>
                  <span className="text-[#a1a1aa] block mb-1">Gateway Event ID</span>
                  <div className="flex items-center gap-2">
                    <span className="font-mono text-[#52525b] truncate max-w-[280px]">{transaction.external_reference}</span>
                    <QuickCopy text={transaction.external_reference} iconSize={11} className="hover:bg-[#fafafa]" />
                  </div>
                </div>
              )}
            </div>
          </div>

          {isRefundable && (
            <div className="space-y-4 pt-4">
              <h4 className="text-[10px] font-bold uppercase tracking-widest text-rose-600 border-b border-rose-100 pb-1">Danger Zone</h4>
              <button 
                onClick={() => setIsRefundModalOpen(true)}
                className="w-full h-9 border border-rose-200 bg-rose-50 text-[11px] font-bold uppercase tracking-widest text-rose-700 hover:bg-rose-100 transition-colors flex items-center justify-center gap-2"
              >
                <RotateCcw size={13} /> Issue Refund
              </button>
            </div>
          )}
        </div>
      </SidePanel>

      {isRefundModalOpen && (
        <div className="fixed inset-0 z-[60] flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/20 backdrop-blur-sm" onClick={() => !refundMutation.isPending && setIsRefundModalOpen(false)} />
          <form onSubmit={(e) => { e.preventDefault(); refundMutation.mutate(); }} className="relative bg-white border border-rose-200 shadow-2xl w-full max-w-sm flex flex-col animate-in zoom-in-95 duration-200">
            <div className="p-4 border-b border-rose-200 bg-rose-50 flex items-center justify-between">
              <h3 className="text-[13px] font-bold uppercase tracking-widest text-rose-700">Issue Refund</h3>
              <button type="button" onClick={() => setIsRefundModalOpen(false)} disabled={refundMutation.isPending} className="text-rose-400 hover:text-rose-700 disabled:opacity-50 p-1"><X size={16} /></button>
            </div>
            <div className="p-5 space-y-4">
              <div className="flex items-start gap-2 p-3 bg-amber-50 border border-amber-200 rounded-sm">
                <AlertTriangle size={14} className="text-amber-600 mt-0.5 shrink-0" />
                <p className="text-[11px] text-amber-800 leading-relaxed">
                  You are about to issue a full refund of <strong className="font-mono">RM {transaction.amount.toFixed(2)}</strong>. This action cannot be undone.
                </p>
              </div>
              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Reason (Optional)</label>
                <input type="text" value={refundReason} onChange={e => setRefundReason(e.target.value)} disabled={refundMutation.isPending} placeholder="e.g. Customer requested cancellation" className="w-full h-9 border border-[#e5e5e5] px-3 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50" />
              </div>
            </div>
            <div className="p-4 border-t border-rose-100 bg-rose-50/50 flex justify-end gap-2">
              <button type="button" onClick={() => setIsRefundModalOpen(false)} disabled={refundMutation.isPending} className="px-4 h-8 text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:bg-[#e5e5e5] hover:text-[#09090b] border border-[#e5e5e5] bg-white transition-colors disabled:opacity-50 rounded-sm">Cancel</button>
              <button type="submit" disabled={refundMutation.isPending} className="px-5 h-8 bg-rose-600 text-white text-[11px] font-bold uppercase tracking-widest hover:bg-rose-700 disabled:opacity-50 flex items-center gap-1.5 rounded-sm">
                {refundMutation.isPending && <Loader2 size={13} className="animate-spin" />} Process Refund
              </button>
            </div>
          </form>
        </div>
      )}
    </>
  );
}
