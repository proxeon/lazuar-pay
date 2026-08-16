import { useState } from "react";
import { DollarSign, RotateCcw } from "lucide-react";
import { cn } from "../../../lib/utils";
import SidePanel from "../../core/components/SidePanel";
import QuickCopy from "../../core/components/QuickCopy";
import RefundModal from "./RefundModal";
import { canRefund, remainingAmount, statusBadgeClass, statusLabel, type TransactionLogDto } from "./transactionStatus";

interface TransactionDetailPanelProps {
  transaction: TransactionLogDto | null;
  onClose: () => void;
  onUpdate: (transaction: TransactionLogDto | null) => void;
}

export default function TransactionDetailPanel({ transaction, onClose, onUpdate }: TransactionDetailPanelProps) {
  const [isRefundModalOpen, setIsRefundModalOpen] = useState(false);

  if (!transaction) return null;

  const isRefunded = transaction.status === "REFUNDED";
  const isPending = transaction.status === "REFUND_PENDING";
  const remaining = remainingAmount(transaction);
  const refundable = canRefund(transaction);

  return (
    <>
      <SidePanel
        isOpen={!!transaction}
        onClose={onClose}
        title="Transaction Record"
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
                  statusBadgeClass(transaction.status)
                )}>
                  {statusLabel(transaction.status, transaction.refunded_amount)}
                </span>
                <span className="text-[11px] text-[#71717a] font-mono">
                  {new Date(transaction.created_at).toLocaleString("en-GB")}
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
              <div className="flex justify-between items-center text-[12px]">
                <span className="text-[#71717a]">Already refunded</span>
                <span className="font-mono text-amber-700">RM {(transaction.refunded_amount ?? 0).toFixed(2)}</span>
              </div>
              <div className="flex justify-between items-center text-[12px]">
                <span className="text-[#71717a]">Remaining</span>
                <span className="font-mono text-[#09090b]">RM {remaining.toFixed(2)}</span>
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
                <span className="text-[#a1a1aa] block mb-1">Recorded By</span>
                <span className="font-mono text-[#09090b]">{transaction.recorded_by_name}</span>
              </div>
              {transaction.gateway_name && (
                <div>
                  <span className="text-[#a1a1aa] block mb-1">Gateway</span>
                  <span className="font-mono text-[#09090b]">{transaction.gateway_name}</span>
                </div>
              )}
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

          {(refundable || isPending) && (
            <div className="space-y-4 pt-4">
              <h4 className="text-[10px] font-bold uppercase tracking-widest text-rose-600 border-b border-rose-100 pb-1">Danger Zone</h4>
              {isPending ? (
                <button
                  disabled
                  className="w-full h-9 border border-blue-200 bg-blue-50 text-[11px] font-bold uppercase tracking-widest text-blue-700 disabled:opacity-80 flex items-center justify-center gap-2"
                >
                  Refund in progress
                </button>
              ) : (
                <button
                  onClick={() => setIsRefundModalOpen(true)}
                  className="w-full h-9 border border-rose-200 bg-rose-50 text-[11px] font-bold uppercase tracking-widest text-rose-700 hover:bg-rose-100 transition-colors flex items-center justify-center gap-2"
                >
                  <RotateCcw size={13} /> {transaction.status === "PARTIALLY_REFUNDED" ? "Refund rest" : transaction.status === "REFUND_FAILED" ? "Retry refund" : "Issue Refund"}
                </button>
              )}
            </div>
          )}
        </div>
      </SidePanel>

      {isRefundModalOpen && (
        <RefundModal
          transaction={transaction}
          onClose={() => setIsRefundModalOpen(false)}
          onSettled={(status) => {
            if (status === "refund_requested") {
              onUpdate({ ...transaction, status: "REFUND_PENDING" });
            }
          }}
        />
      )}
    </>
  );
}
