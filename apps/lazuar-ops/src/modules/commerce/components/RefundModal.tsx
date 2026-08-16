import { useMemo, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { AlertTriangle, Loader2, X } from "lucide-react";
import { toast } from "sonner";
import { client } from "../../../lib/api-client";
import { remainingAmount, type TransactionLogDto } from "./transactionStatus";

const API_GATEWAYS = ["STRIPE", "CHIP", "RAZORPAY"] as const;
const GATEWAY_OPTIONS = ["STRIPE", "CHIP", "RAZORPAY", "BILLPLZ"] as const;

interface RefundModalProps {
  transaction: TransactionLogDto;
  subscriptionId?: string;
  onClose: () => void;
  onSettled?: (status: string) => void;
}

function refundErrorMessage(error: unknown): string {
  if (!error || typeof error !== "object") return "Refund failed";
  const e = error as { detail?: string; status?: string; message?: string };
  return e.detail || e.status || e.message || "Refund failed";
}

export default function RefundModal({ transaction, subscriptionId, onClose, onSettled }: RefundModalProps) {
  const queryClient = useQueryClient();
  const remaining = remainingAmount(transaction);
  const [amount, setAmount] = useState(remaining.toFixed(2));
  const [reason, setReason] = useState("");
  const [gatewayOverride, setGatewayOverride] = useState(transaction.gateway_name ?? "");

  const needsGateway = !transaction.gateway_name;
  const resolvedGateway = (gatewayOverride || transaction.gateway_name || "").toUpperCase();
  const isApiRail = transaction.supports_api_refund || API_GATEWAYS.includes(resolvedGateway as (typeof API_GATEWAYS)[number]);
  const parsedAmount = Number.parseFloat(amount);
  const amountValid = Number.isFinite(parsedAmount) && parsedAmount > 0 && parsedAmount <= remaining + 0.0001;

  const copy = useMemo(() => {
    if (isApiRail) {
      return {
        cta: `Refund RM ${Number.isFinite(parsedAmount) ? parsedAmount.toFixed(2) : remaining.toFixed(2)} via ${resolvedGateway || "gateway"}`,
        warning: "This sends money back at the processor. It cannot be undone from Lazuar.",
      };
    }
    if (resolvedGateway === "BILLPLZ") {
      return {
        cta: `Mark RM ${Number.isFinite(parsedAmount) ? parsedAmount.toFixed(2) : remaining.toFixed(2)} refunded`,
        warning: "Billplz has no bill-refund API. Refund the bill in the Billplz dashboard, then mark it here.",
      };
    }
    return {
      cta: `Mark RM ${Number.isFinite(parsedAmount) ? parsedAmount.toFixed(2) : remaining.toFixed(2)} refunded`,
      warning: "This was logged offline. Mark only after you returned the money.",
    };
  }, [isApiRail, parsedAmount, remaining, resolvedGateway]);

  const mutation = useMutation({
    mutationFn: async () => {
      if (!amountValid) throw new Error("Enter an amount up to the remaining balance.");
      if (needsGateway && !resolvedGateway) throw new Error("Select the gateway used for this payment.");

      const { data, error } = await client.POST("/admin/commerce/transactions/{id}/refund", {
        params: { path: { id: transaction.id } },
        body: {
          amount: parsedAmount,
          reason: reason.trim() || undefined,
          gateway_name: needsGateway ? resolvedGateway : undefined,
          mark_refunded: isApiRail ? undefined : true,
          subscription_id: subscriptionId,
        },
      });
      if (error) throw new Error(refundErrorMessage(error));
      return data?.status ?? (isApiRail ? "refund_requested" : "refunded");
    },
    onSuccess: (status) => {
      if (status === "refund_requested") {
        toast.success("Refund requested");
      } else {
        toast.success("Marked refunded");
      }
      queryClient.invalidateQueries({ queryKey: ["commerce-transactions"] });
      queryClient.invalidateQueries({ queryKey: ["commerce-payments"] });
      queryClient.invalidateQueries({ queryKey: ["financial-summary"] });
      onSettled?.(status);
      onClose();
    },
    onError: (err: Error) => {
      toast.error("Refund failed", { description: err.message });
    },
  });

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/20 backdrop-blur-sm" onClick={() => !mutation.isPending && onClose()} />
      <form
        onSubmit={(e) => {
          e.preventDefault();
          mutation.mutate();
        }}
        className="relative bg-white border border-rose-200 shadow-2xl w-full max-w-sm flex flex-col animate-in zoom-in-95 duration-200"
      >
        <div className="p-4 border-b border-rose-200 bg-rose-50 flex items-center justify-between">
          <div>
            <h3 className="text-[13px] font-bold uppercase tracking-widest text-rose-700">Refund</h3>
            <p className="text-[10px] font-mono text-rose-600/80 mt-0.5">
              {resolvedGateway || "Gateway required"} · {transaction.recorded_by_name}
            </p>
          </div>
          <button type="button" onClick={onClose} disabled={mutation.isPending} className="text-rose-400 hover:text-rose-700 disabled:opacity-50 p-1">
            <X size={16} />
          </button>
        </div>
        <div className="p-5 space-y-4">
          <div className="bg-[#fafafa] border border-[#e5e5e5] p-3 space-y-1.5 text-[12px] font-mono">
            <div className="flex justify-between"><span className="text-[#71717a] font-sans">Original</span><span>RM {transaction.amount.toFixed(2)}</span></div>
            <div className="flex justify-between"><span className="text-[#71717a] font-sans">Already refunded</span><span>RM {(transaction.refunded_amount ?? 0).toFixed(2)}</span></div>
            <div className="flex justify-between font-bold"><span className="text-[#09090b] font-sans">Remaining</span><span>RM {remaining.toFixed(2)}</span></div>
          </div>

          <div className="flex items-start gap-2 p-3 bg-amber-50 border border-amber-200 rounded-sm">
            <AlertTriangle size={14} className="text-amber-600 mt-0.5 shrink-0" />
            <p className="text-[11px] text-amber-800 leading-relaxed">{copy.warning}</p>
          </div>

          {needsGateway && (
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Gateway</label>
              <select
                value={gatewayOverride}
                onChange={(e) => setGatewayOverride(e.target.value)}
                disabled={mutation.isPending}
                className="w-full h-9 border border-[#e5e5e5] px-3 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50"
              >
                <option value="">Select gateway</option>
                {GATEWAY_OPTIONS.map((g) => (
                  <option key={g} value={g}>{g}</option>
                ))}
              </select>
            </div>
          )}

          <div className="space-y-1.5">
            <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Amount (MYR)</label>
            <input
              type="number"
              min={0.01}
              max={remaining}
              step={0.01}
              value={amount}
              onChange={(e) => setAmount(e.target.value)}
              disabled={mutation.isPending}
              className="w-full h-9 border border-[#e5e5e5] px-3 text-[13px] font-mono focus:outline-none focus:border-[#09090b] disabled:opacity-50"
            />
            <p className="text-[10px] text-[#71717a]">
              Already refunded RM {(transaction.refunded_amount ?? 0).toFixed(2)} · remaining RM {remaining.toFixed(2)}
            </p>
          </div>

          <div className="space-y-1.5">
            <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Reason (Optional)</label>
            <input
              type="text"
              maxLength={255}
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              disabled={mutation.isPending}
              placeholder="e.g. Customer requested cancellation"
              className="w-full h-9 border border-[#e5e5e5] px-3 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50"
            />
          </div>

          <p className="text-[10px] text-[#71717a] leading-relaxed">
            Cancel the subscription separately if access should stop. Refund does not cancel.
          </p>
        </div>
        <div className="p-4 border-t border-rose-100 bg-rose-50/50 flex justify-end gap-2">
          <button
            type="button"
            onClick={onClose}
            disabled={mutation.isPending}
            className="px-4 h-8 text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:bg-[#e5e5e5] hover:text-[#09090b] border border-[#e5e5e5] bg-white transition-colors disabled:opacity-50 rounded-sm"
          >
            Cancel
          </button>
          <button
            type="submit"
            disabled={mutation.isPending || !amountValid}
            className="px-5 h-8 bg-rose-600 text-white text-[11px] font-bold uppercase tracking-widest hover:bg-rose-700 disabled:opacity-50 flex items-center gap-1.5 rounded-sm"
          >
            {mutation.isPending && <Loader2 size={13} className="animate-spin" />}
            {copy.cta}
          </button>
        </div>
      </form>
    </div>
  );
}
