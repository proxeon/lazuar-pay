import { useState, useEffect } from "react";
import { useQuery } from "@tanstack/react-query";
import { X, Loader2 } from "lucide-react";
import { toast } from "sonner";
import { api } from "../lib/api";
import type { Plan } from "../lib/api";

interface AddSubscriberModalProps {
  onClose: () => void;
  onSuccess: () => void;
}

export default function AddSubscriberModal({ onClose, onSuccess }: AddSubscriberModalProps) {
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [phone, setPhone] = useState("");
  const [planId, setPlanId] = useState("");
  const [paymentMethod, setPaymentMethod] = useState("BANK_TRANSFER");
  const [amountPaid, setAmountPaid] = useState<number | "">("");
  const [referenceNumber, setReferenceNumber] = useState("");
  const [notes, setNotes] = useState("");
  const [isReminderOnly, setIsReminderOnly] = useState(false);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const { data: plans } = useQuery<Plan[]>({
    queryKey: ["community-plans"],
    queryFn: api.getPlans,
  });

  // Auto-fill amount when plan is selected
  useEffect(() => {
    if (planId && plans) {
      const selected = plans.find(p => p.id === planId);
      if (selected && amountPaid === "") {
        setAmountPaid(selected.price);
      }
    }
  }, [planId, plans]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);

    try {
      await api.createSubscriber({
        name: name.trim(),
        email: email.trim(),
        phone: phone.trim(),
        plan_id: planId,
        source: "MANUAL_ENTRY",
        payment_method: isReminderOnly ? undefined : paymentMethod,
        reference_number: paymentMethod === "BANK_TRANSFER" && referenceNumber ? referenceNumber.trim() : undefined,
        amount_paid: isReminderOnly ? undefined : (typeof amountPaid === "number" ? amountPaid : undefined),
        notes: notes.trim() || undefined,
        is_reminder_only: isReminderOnly,
      });
      toast.success("Subscriber added successfully!");
      onSuccess();
    } catch (err: any) {
      toast.error(err.message || "Failed to add subscriber.");
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative bg-card border border-border/60 rounded-none shadow-[8px_8px_0px_0px_rgba(0,0,0,0.1)] w-full max-w-lg overflow-hidden flex flex-col animate-in fade-in zoom-in-95 duration-200 max-h-[90vh]">
        
        {/* Header */}
        <div className="flex items-center justify-between p-5 border-b border-border/60 shrink-0">
          <div>
            <h3 className="text-sm font-bold uppercase tracking-widest text-foreground">Add Subscriber</h3>
            <p className="text-[11px] text-muted-foreground mt-1">Manually create a new subscriber entry.</p>
          </div>
          <button onClick={onClose} className="text-muted-foreground hover:bg-secondary rounded-none transition-colors p-1">
            <X size={16} />
          </button>
        </div>

        {/* Form */}
        <form onSubmit={handleSubmit} className="p-5 space-y-4 overflow-y-auto flex-1">
          <div className="space-y-1.5">
            <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Full Name *</label>
            <input type="text" required value={name} onChange={e => setName(e.target.value)} placeholder="e.g. Ahmad Firdaus" className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring" />
          </div>

          <div className="space-y-1.5">
            <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Email *</label>
            <input type="email" required value={email} onChange={e => setEmail(e.target.value)} placeholder="e.g. ahmad@email.com" className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring" />
          </div>

          <div className="space-y-1.5">
            <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Phone *</label>
            <input type="tel" required value={phone} onChange={e => setPhone(e.target.value)} placeholder="+60 12-345 6789" className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring" />
          </div>

          <div className="space-y-1.5">
            <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Plan *</label>
            <select required value={planId} onChange={e => setPlanId(e.target.value)} className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring">
              <option value="">Select a plan...</option>
              {plans?.filter(p => p.is_active).map(p => (
                <option key={p.id} value={p.id}>{p.name} — RM {p.price.toFixed(2)}/{p.interval}</option>
              ))}
            </select>
          </div>

          <div className="flex items-center gap-3 py-2">
            <input type="checkbox" id="reminder_only" checked={isReminderOnly} onChange={e => setIsReminderOnly(e.target.checked)} className="h-4 w-4 rounded-none border-border/60 accent-foreground" />
            <label htmlFor="reminder_only" className="text-sm font-medium text-foreground">Reminder only (no payment collection)</label>
          </div>

          {!isReminderOnly && (
            <>
              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Payment Method</label>
                <select value={paymentMethod} onChange={e => setPaymentMethod(e.target.value)} className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring">
                  <option value="BANK_TRANSFER">Bank Transfer</option>
                  <option value="CASH">Cash</option>
                  <option value="E_WALLET">E-Wallet</option>
                  <option value="ONLINE_GATEWAY">Online Gateway</option>
                  <option value="OTHER">Other</option>
                </select>
              </div>

              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Amount Paid (RM)</label>
                <input type="number" step="0.01" min="0" value={amountPaid} onChange={e => setAmountPaid(e.target.value === "" ? "" : parseFloat(e.target.value))} placeholder="0.00" className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring" />
                <p className="text-[11px] text-muted-foreground">Auto-filled from plan price. Set to 0 if unpaid.</p>
              </div>

              {paymentMethod === "BANK_TRANSFER" && (
                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Reference Number</label>
                  <input type="text" value={referenceNumber} onChange={e => setReferenceNumber(e.target.value)} placeholder="e.g. FPX-20250101-12345" className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm font-mono shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring" />
                </div>
              )}
            </>
          )}

          <div className="space-y-1.5">
            <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Admin Notes</label>
            <textarea value={notes} onChange={e => setNotes(e.target.value)} rows={2} placeholder="Optional internal notes..." className="flex w-full rounded-none border border-border/60 bg-background px-3 py-2 text-sm shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring resize-y" />
          </div>

          <div className="flex items-center justify-between pt-4">
            <button type="button" onClick={onClose} className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground hover:text-foreground transition-colors px-2 py-1">Cancel</button>
            <button type="submit" disabled={isSubmitting || !planId} className="h-10 px-6 bg-foreground text-background text-xs font-bold tracking-wide uppercase rounded-none hover:bg-foreground/90 disabled:opacity-50 transition-colors shadow-[4px_4px_0px_0px_rgba(0,0,0,0.1)] hover:shadow-none hover:translate-y-[2px] hover:translate-x-[2px] active:scale-95 flex items-center gap-2">
              {isSubmitting && <Loader2 size={14} className="animate-spin" />}
              {isSubmitting ? "Adding..." : "Add Subscriber"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
