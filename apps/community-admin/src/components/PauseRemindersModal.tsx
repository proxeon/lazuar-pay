// apps/community-admin/src/components/PauseRemindersModal.tsx

import { useState } from "react";
import { X, Loader2, BellOff, Calendar } from "lucide-react";
import { toast } from "sonner";
import { api } from "../lib/api";
import type { Subscriber } from "../lib/api";

interface PauseRemindersModalProps {
  sub: Subscriber;
  onClose: () => void;
  onSuccess: () => void;
}

export default function PauseRemindersModal({ sub, onClose, onSuccess }: PauseRemindersModalProps) {
  const [mode, setMode] = useState<"PRESET" | "CUSTOM">("PRESET");
  const [presetDuration, setPresetDuration] = useState("7");
  const [customDate, setCustomDate] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  const currentlyPaused = sub.reminders_paused_until 
    ? new Date(sub.reminders_paused_until).getTime() > Date.now() 
    : false;

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);

    try {
      let pauseUntil: string | null = null;

      if (mode === "PRESET") {
        const days = parseInt(presetDuration, 10);
        const date = new Date();
        date.setDate(date.getDate() + days);
        pauseUntil = date.toISOString();
      } else {
        if (!customDate) throw new Error("Please select a date.");
        pauseUntil = new Date(`${customDate}T23:59:59Z`).toISOString();
      }

      await api.pauseReminders(sub.id, { pause_until: pauseUntil });
      toast.success("Reminders paused successfully.");
      onSuccess();
    } catch (err: any) {
      toast.error(err.message || "Failed to pause reminders.");
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleResume = async () => {
    setIsSubmitting(true);
    try {
      await api.pauseReminders(sub.id, { pause_until: null });
      toast.success("Reminders resumed.");
      onSuccess();
    } catch (err: any) {
      toast.error(err.message || "Failed to resume reminders.");
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative bg-card border border-border/60 rounded-none shadow-[8px_8px_0px_0px_rgba(0,0,0,0.1)] w-full max-w-md overflow-hidden flex flex-col animate-in fade-in zoom-in-95 duration-200">
        
        <div className="flex items-center justify-between p-5 border-b border-border/60 shrink-0">
          <div>
            <h3 className="text-sm font-bold uppercase tracking-widest text-foreground flex items-center gap-2">
              <BellOff size={16} /> Pause Reminders
            </h3>
            <p className="text-[11px] text-muted-foreground mt-1">Suspend automated messages for {sub.customer_name}.</p>
          </div>
          <button onClick={onClose} className="text-muted-foreground hover:bg-secondary rounded-none transition-colors p-1"><X size={16} /></button>
        </div>

        <div className="p-5 space-y-5">
          {currentlyPaused && (
            <div className="p-3 bg-amber-50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-900 flex flex-col gap-2">
              <p className="text-xs font-semibold text-amber-800 dark:text-amber-500">
                Reminders are currently paused until {new Date(sub.reminders_paused_until!).toLocaleDateString("en-MY")}.
              </p>
              <button 
                type="button" 
                onClick={handleResume}
                disabled={isSubmitting}
                className="text-[11px] font-bold uppercase tracking-widest bg-white dark:bg-black border border-amber-200 dark:border-amber-800 text-amber-700 dark:text-amber-400 py-1.5 hover:bg-amber-100 transition-colors w-full"
              >
                {isSubmitting ? "Resuming..." : "Resume Now"}
              </button>
            </div>
          )}

          <form id="pause-form" onSubmit={handleSubmit} className="space-y-4">
            
            <div className="flex bg-secondary/50 border border-border/60 p-1 rounded-none">
              <button 
                type="button"
                onClick={() => setMode("PRESET")}
                className={`flex-1 text-[10px] font-bold uppercase tracking-widest py-1.5 transition-colors ${mode === "PRESET" ? "bg-background border border-border/60 shadow-sm text-foreground" : "text-muted-foreground hover:text-foreground"}`}
              >
                Quick Select
              </button>
              <button 
                type="button"
                onClick={() => setMode("CUSTOM")}
                className={`flex-1 text-[10px] font-bold uppercase tracking-widest py-1.5 transition-colors ${mode === "CUSTOM" ? "bg-background border border-border/60 shadow-sm text-foreground" : "text-muted-foreground hover:text-foreground"}`}
              >
                Custom Date
              </button>
            </div>

            {mode === "PRESET" ? (
              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Pause Duration</label>
                <select 
                  value={presetDuration} 
                  onChange={e => setPresetDuration(e.target.value)} 
                  className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                >
                  <option value="7">1 Week</option>
                  <option value="14">2 Weeks</option>
                  <option value="30">1 Month</option>
                  <option value="90">3 Months</option>
                </select>
              </div>
            ) : (
              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Pause Until (End Date)</label>
                <input 
                  type="date" 
                  required 
                  min={new Date().toISOString().split('T')[0]}
                  value={customDate} 
                  onChange={e => setCustomDate(e.target.value)} 
                  className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring" 
                />
              </div>
            )}
          </form>
        </div>

        <div className="flex items-center justify-between p-5 border-t border-border/60 shrink-0">
          <button type="button" onClick={onClose} className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground hover:text-foreground transition-colors px-2 py-1">Cancel</button>
          <button type="submit" form="pause-form" disabled={isSubmitting || (mode === "CUSTOM" && !customDate)} className="h-10 px-6 bg-foreground text-background text-xs font-bold tracking-wide uppercase rounded-none hover:bg-foreground/90 disabled:opacity-50 transition-colors shadow-[4px_4px_0px_0px_rgba(0,0,0,0.1)] hover:shadow-none hover:translate-y-[2px] hover:translate-x-[2px] active:scale-95 flex items-center gap-2">
            {isSubmitting ? <Loader2 size={14} className="animate-spin" /> : <Calendar size={14} />}
            {isSubmitting ? "Saving..." : "Pause Reminders"}
          </button>
        </div>
      </div>
    </div>
  );
}
