import { useState } from "react";
import { X, Loader2 } from "lucide-react";
import { toast } from "sonner";
import { api } from "../lib/api";
import type { Subscriber } from "../lib/api";

interface RecordPaymentModalProps {
  sub: Subscriber;
  onClose: () => void;
  onSuccess: () => void;
}

export default function RecordPaymentModal({ sub, onClose, onSuccess }: RecordPaymentModalProps) {
  const [amount, setAmount] = useState<number | "">(sub.plan_price);
  const [method, setMethod] = useState("BANK_TRANSFER");
  const [refNumber, setRefNumber] = useState("");
  const [notes, setNotes] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);
    try {
      await api.recordPayment(sub.id, {
        amount: Number(amount) || 0,
        payment_method: method,
        reference_number: refNumber.trim() || undefined,
        notes: notes.trim() || undefined
      });
      toast.success(`Payment recorded for ${sub.customer_name}.`);
      onSuccess();
    } catch (err: any) {
      toast.error(err.message || "Failed to record payment.");
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative bg-card border border-border/60 rounded-none shadow-[8px_8px_0px_0px_rgba(0,0,0,0.1)] w-full max-w-md overflow-hidden flex flex-col animate-in fade-in zoom-in-95 duration-200">
        <div className="flex items-center justify-between p-5 border-b border-border/60">
          <div>
            <h3 className="text-sm font-bold uppercase tracking-widest text-foreground">Record Payment</h3>
            <p className="text-[11px] text-muted-foreground mt-1">Log an external payment for {sub.customer_name}.</p>
          </div>
          <button onClick={onClose} className="text-muted-foreground hover:bg-secondary rounded-none transition-colors p-1">
            <X size={16} />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="p-5 space-y-4">
          <div className="space-y-1.5">
            <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Amount (RM) *</label>
            <input type="number" step="0.01" min="0" required value={amount} onChange={e => setAmount(e.target.value === "" ? "" : parseFloat(e.target.value))} className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring" />
          </div>

          <div className="space-y-1.5">
            <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Payment Method *</label>
            <select required value={method} onChange={e => setMethod(e.target.value)} className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring">
              <option value="BANK_TRANSFER">Bank Transfer</option>
              <option value="CASH">Cash</option>
              <option value="E_WALLET">E-Wallet</option>
              <option value="OTHER">Other</option>
            </select>
          </div>

          <div className="space-y-1.5">
            <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Reference Number</label>
            <input type="text" value={refNumber} onChange={e => setRefNumber(e.target.value)} placeholder="Optional transaction ID..." className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm font-mono shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring" />
          </div>

          <div className="space-y-1.5">
            <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Admin Notes</label>
            <textarea value={notes} onChange={e => setNotes(e.target.value)} rows={2} placeholder="Optional notes about this payment..." className="flex w-full rounded-none border border-border/60 bg-background px-3 py-2 text-sm shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring resize-y" />
          </div>

          <div className="flex items-center justify-between pt-4">
            <button type="button" onClick={onClose} className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground hover:text-foreground transition-colors px-2 py-1">Cancel</button>
            <button type="submit" disabled={isSubmitting || amount === ""} className="h-10 px-6 bg-foreground text-background text-xs font-bold tracking-wide uppercase rounded-none hover:bg-foreground/90 disabled:opacity-50 transition-colors shadow-[4px_4px_0px_0px_rgba(0,0,0,0.1)] hover:shadow-none hover:translate-y-[2px] hover:translate-x-[2px] active:scale-95 flex items-center gap-2">
              {isSubmitting && <Loader2 size={14} className="animate-spin" />}
              {isSubmitting ? "Saving..." : "Record Payment"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
