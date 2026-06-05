import { useState } from "react";
import { X, Loader2 } from "lucide-react";
import { toast } from "sonner";
import { client } from "../lib/api-client";
import type { Subscriber } from "../lib/api-client";

interface EditSubscriberModalProps {
  sub: Subscriber;
  onClose: () => void;
  onSuccess: () => void;
}

export default function EditSubscriberModal({ sub, onClose, onSuccess }: EditSubscriberModalProps) {
  const [name, setName] = useState(sub.customer_name);
  const [email, setEmail] = useState(sub.customer_email);
  const [phone, setPhone] = useState(sub.customer_phone || "");
  const [isReminderOnly, setIsReminderOnly] = useState(sub.is_reminder_only || false);
  const [preferredChannel, setPreferredChannel] = useState(sub.preferred_channel || "");
  const [notes, setNotes] = useState(sub.admin_notes || "");
  
  const initialDate = sub.next_billing_date ? sub.next_billing_date.split('T')[0] : "";
  const [nextRenewalDate, setNextRenewalDate] = useState(initialDate);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);

    const finalDate = nextRenewalDate ? `${nextRenewalDate}T00:00:00Z` : undefined;

    const { error } = await client.PUT("/admin/community/subscribers/{id}", {
      params: { path: { id: sub.id } },
      body: {
        is_reminder_only: isReminderOnly,
        preferred_channel: preferredChannel || undefined,
        admin_notes: notes.trim() || undefined,
        next_renewal_date: finalDate,
      }
    });

    setIsSubmitting(false);

    if (error) {
      toast.error(error.detail || "Failed to update subscriber.");
    } else {
      toast.success("Subscriber details updated successfully!");
      onSuccess();
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative bg-card border border-border/60 rounded-none shadow-[8px_8px_0px_0px_rgba(0,0,0,0.1)] w-full max-w-lg overflow-hidden flex flex-col animate-in fade-in zoom-in-95 duration-200 max-h-[90vh]">
        <div className="flex items-center justify-between p-5 border-b border-border/60 shrink-0">
          <div>
            <h3 className="text-sm font-bold uppercase tracking-widest text-foreground">Edit Subscriber</h3>
            <p className="text-[11px] text-muted-foreground mt-1">Update profile and configuration details.</p>
          </div>
          <button onClick={onClose} className="text-muted-foreground hover:bg-secondary rounded-none transition-colors p-1"><X size={16} /></button>
        </div>

        <form onSubmit={handleSubmit} className="p-5 space-y-4 overflow-y-auto flex-1">
          <h4 className="text-[10px] font-bold uppercase tracking-widest text-foreground border-b border-border/40 pb-1">Client Profile</h4>
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Full Name *</label>
              <input type="text" required value={name} onChange={e => setName(e.target.value)} disabled className="flex h-10 w-full rounded-none border border-border/60 bg-secondary/50 px-3 py-1 text-sm shadow-sm opacity-60" />
            </div>
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Email *</label>
              <input type="email" required value={email} onChange={e => setEmail(e.target.value)} disabled className="flex h-10 w-full rounded-none border border-border/60 bg-secondary/50 px-3 py-1 text-sm shadow-sm opacity-60" />
            </div>
          </div>
          <div className="space-y-1.5">
            <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Phone *</label>
            <input type="tel" required value={phone} onChange={e => setPhone(e.target.value)} disabled className="flex h-10 w-full rounded-none border border-border/60 bg-secondary/50 px-3 py-1 text-sm shadow-sm opacity-60" />
          </div>

          <h4 className="text-[10px] font-bold uppercase tracking-widest text-foreground border-b border-border/40 pb-1 mt-6">Subscription Configuration</h4>
          <div className="flex items-center gap-3 py-2">
            <input type="checkbox" id="reminder_only_edit" checked={isReminderOnly} onChange={e => setIsReminderOnly(e.target.checked)} className="h-4 w-4 rounded-none border-border/60 accent-foreground" />
            <label htmlFor="reminder_only_edit" className="text-sm font-medium text-foreground">Reminder only (no payment collection)</label>
          </div>

          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Preferred Channel</label>
              <select value={preferredChannel} onChange={e => setPreferredChannel(e.target.value)} className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors">
                <option value="">Auto (Both)</option>
                <option value="EMAIL">Email Only</option>
                <option value="WHATSAPP">WhatsApp Only</option>
              </select>
            </div>
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Next Renewal Date</label>
              <input type="date" value={nextRenewalDate} onChange={e => setNextRenewalDate(e.target.value)} className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors" />
            </div>
          </div>

          <div className="space-y-1.5">
            <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Admin Notes</label>
            <textarea value={notes} onChange={e => setNotes(e.target.value)} rows={3} placeholder="Internal notes..." className="flex w-full rounded-none border border-border/60 bg-background px-3 py-2 text-sm shadow-sm transition-colors resize-y" />
          </div>

          <div className="flex items-center justify-between pt-4">
            <button type="button" onClick={onClose} className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground hover:text-foreground transition-colors px-2 py-1">Cancel</button>
            <button type="submit" disabled={isSubmitting} className="h-10 px-6 bg-foreground text-background text-xs font-bold tracking-wide uppercase rounded-none hover:bg-foreground/90 disabled:opacity-50 transition-colors shadow-[4px_4px_0px_0px_rgba(0,0,0,0.1)] hover:shadow-none hover:translate-y-[2px] hover:translate-x-[2px] active:scale-95 flex items-center gap-2">
              {isSubmitting && <Loader2 size={14} className="animate-spin" />}
              {isSubmitting ? "Saving..." : "Save Changes"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
