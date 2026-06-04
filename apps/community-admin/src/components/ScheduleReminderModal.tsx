// apps/community-admin/src/components/ScheduleReminderModal.tsx

import { useState, useMemo } from "react";
import { useQuery } from "@tanstack/react-query";
import { X, Loader2, CalendarClock } from "lucide-react";
import { toast } from "sonner";
import { api } from "../lib/api";
import type { Subscriber, MessageTemplate } from "../lib/api";

interface ScheduleReminderModalProps {
  sub: Subscriber;
  onClose: () => void;
}

export default function ScheduleReminderModal({ sub, onClose }: ScheduleReminderModalProps) {
  const [mode, setMode] = useState<"TEMPLATE" | "CUSTOM">("TEMPLATE");
  const [templateName, setTemplateName] = useState("Community Renewal (3 Days)");
  const [customMessage, setCustomMessage] = useState("");
  const [channel, setChannel] = useState("DEFAULT");
  
  // Schedule state
  const [date, setDate] = useState("");
  const [time, setTime] = useState("");
  
  const [isScheduling, setIsScheduling] = useState(false);

  const { data: templates } = useQuery<MessageTemplate[]>({
    queryKey: ["messaging-templates"],
    queryFn: api.getTemplates,
  });

  const communityTemplates = useMemo(() => {
    return templates?.filter(t => t.name.startsWith("Community ")) || [];
  }, [templates]);

  const selectedTemplate = useMemo(() => {
    return communityTemplates.find(t => t.name === templateName);
  }, [communityTemplates, templateName]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!date || !time) {
      toast.error("Please provide both date and time.");
      return;
    }

    setIsScheduling(true);

    try {
      // Create local Date and convert to UTC ISO string
      const localDate = new Date(`${date}T${time}`);
      if (isNaN(localDate.getTime())) {
          toast.error("Invalid date or time selected.");
          setIsScheduling(false);
          return;
      }

      if (localDate.getTime() < Date.now()) {
          toast.error("Scheduled time must be in the future.");
          setIsScheduling(false);
          return;
      }

      const scheduled_at = localDate.toISOString();
      const reqChannel = channel === "DEFAULT" ? undefined : channel;
      
      const payload = mode === "TEMPLATE" 
        ? { subscriber_id: sub.id, template_name: templateName, channel: reqChannel, scheduled_at }
        : { subscriber_id: sub.id, custom_message: customMessage.trim(), channel: reqChannel, scheduled_at };

      await api.scheduleOneOff(payload);
      
      toast.success(`Reminder scheduled successfully for ${localDate.toLocaleString()}.`);
      onClose();
    } catch (err: any) {
      toast.error(err.message || "Failed to schedule reminder.");
    } finally {
      setIsScheduling(false);
    }
  };

  const generatePreview = () => {
    let text = mode === "TEMPLATE" ? (selectedTemplate?.body || "") : customMessage;
    if (!text) return "Select a template or type a message to see preview...";

    const dummyLink = `https://community.lazuar.com/...`;
    return text
      .replace(/{{customer_name}}/gi, sub.customer_name)
      .replace(/{{plan_name}}/gi, sub.plan_name)
      .replace(/{{total_price}}/gi, sub.plan_price.toFixed(2))
      .replace(/{{renewal_link}}/gi, dummyLink)
      .replace(/{{group_link}}/gi, dummyLink)
      .replace(/{{meeting_link}}/gi, dummyLink)
      .replace(/{{business_name}}/gi, "Your Business");
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative bg-card border border-border/60 rounded-none shadow-[8px_8px_0px_0px_rgba(0,0,0,0.1)] w-full max-w-lg overflow-hidden flex flex-col animate-in fade-in zoom-in-95 duration-200 max-h-[90vh]">
        <div className="flex items-center justify-between p-5 border-b border-border/60 shrink-0">
          <div>
            <h3 className="text-sm font-bold uppercase tracking-widest text-foreground">Schedule Reminder</h3>
            <p className="text-[11px] text-muted-foreground mt-1">Queue a future message for {sub.customer_name}.</p>
          </div>
          <button onClick={onClose} className="text-muted-foreground hover:bg-secondary rounded-none transition-colors p-1"><X size={16} /></button>
        </div>

        <div className="p-5 flex-1 overflow-y-auto space-y-5">
          <form id="schedule-form" onSubmit={handleSubmit} className="space-y-4">

            <div className="grid grid-cols-2 gap-4">
               <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Send Date</label>
                  <input type="date" required value={date} onChange={e => setDate(e.target.value)} className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring" />
               </div>
               <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Send Time</label>
                  <input type="time" required value={time} onChange={e => setTime(e.target.value)} className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring" />
               </div>
            </div>

            <div className="flex bg-secondary/50 border border-border/60 p-1 rounded-none mt-2">
              <button 
                type="button"
                onClick={() => setMode("TEMPLATE")}
                className={`flex-1 text-xs font-bold uppercase tracking-widest py-2 transition-colors ${mode === "TEMPLATE" ? "bg-background border border-border/60 shadow-sm text-foreground" : "text-muted-foreground hover:text-foreground"}`}
              >
                Use Template
              </button>
              <button 
                type="button"
                onClick={() => setMode("CUSTOM")}
                className={`flex-1 text-xs font-bold uppercase tracking-widest py-2 transition-colors ${mode === "CUSTOM" ? "bg-background border border-border/60 shadow-sm text-foreground" : "text-muted-foreground hover:text-foreground"}`}
              >
                Custom Message
              </button>
            </div>
            
            {mode === "TEMPLATE" ? (
              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Select Template</label>
                <select 
                  required 
                  value={templateName} 
                  onChange={e => setTemplateName(e.target.value)} 
                  className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                >
                  <option value="" disabled>Select a template...</option>
                  {communityTemplates.map(t => (
                    <option key={t.id} value={t.name}>{t.name}</option>
                  ))}
                </select>
              </div>
            ) : (
              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Custom Message Body</label>
                <textarea 
                  required 
                  value={customMessage} 
                  onChange={e => setCustomMessage(e.target.value)} 
                  rows={4} 
                  placeholder="Type your message here. You can use {{customer_name}}, {{plan_name}}..." 
                  className="flex w-full rounded-none border border-border/60 bg-background px-3 py-2 text-sm shadow-sm transition-colors placeholder:text-muted-foreground focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring resize-y font-mono" 
                />
              </div>
            )}

            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Delivery Channel</label>
              <select 
                value={channel} 
                onChange={e => setChannel(e.target.value)} 
                className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
              >
                <option value="DEFAULT">Auto (Use their preference or both)</option>
                <option value="WHATSAPP">WhatsApp Only</option>
                <option value="EMAIL">Email Only</option>
                <option value="BOTH">Force Both (Email & WhatsApp)</option>
              </select>
            </div>

            {/* Preview Box */}
            <div className="space-y-1.5 pt-2">
              <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Message Preview</label>
              <div className="w-full min-h-[100px] rounded-none border border-border/60 bg-secondary/30 px-3 py-3 text-sm text-foreground shadow-inner font-mono whitespace-pre-wrap leading-relaxed">
                {generatePreview()}
              </div>
            </div>

          </form>
        </div>

        <div className="flex items-center justify-between p-5 border-t border-border/60 shrink-0">
          <button type="button" onClick={onClose} className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground hover:text-foreground transition-colors px-2 py-1">Cancel</button>
          <button type="submit" form="schedule-form" disabled={isScheduling || (mode === "CUSTOM" && !customMessage.trim()) || (mode === "TEMPLATE" && !templateName) || !date || !time} className="h-10 px-6 bg-foreground text-background text-xs font-bold tracking-wide uppercase rounded-none hover:bg-foreground/90 disabled:opacity-50 transition-colors shadow-[4px_4px_0px_0px_rgba(0,0,0,0.1)] hover:shadow-none hover:translate-y-[2px] hover:translate-x-[2px] active:scale-95 flex items-center gap-2">
            {isScheduling ? <Loader2 size={14} className="animate-spin" /> : <CalendarClock size={14} />}
            {isScheduling ? "Scheduling..." : "Schedule"}
          </button>
        </div>
      </div>
    </div>
  );
}
