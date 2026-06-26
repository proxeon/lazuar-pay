import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2, X } from "lucide-react";
import { toast } from "sonner";
import { client } from "../../../../lib/api-client";

interface CreateScheduleModalProps {
  plans?: any[];
  templates?: any[];
  onClose: () => void;
}

export default function CreateScheduleModal({ plans, templates, onClose }: CreateScheduleModalProps) {
  const queryClient = useQueryClient();
  const [planId, setPlanId] = useState("");
  const [templateId, setTemplateId] = useState("");
  const [channel, setChannel] = useState("EMAIL");
  const [days, setDays] = useState<number | string>(0);
  const [time, setTime] = useState("08:00");

  const createScheduleMutation = useMutation({
    mutationFn: async () => {
      if (!templateId) throw new Error("A message template is required.");
      const { error } = await client.POST("/admin/community/reminder-schedules", {
        body: {
          plan_id: planId || undefined,
          template_id: templateId,
          channel,
          days_relative_to_due: Number(days),
          time_of_day: time,
          is_enabled: true
        }
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("Reminder schedule created successfully.");
      queryClient.invalidateQueries({ queryKey: ["community-reminder-schedules"] });
      onClose();
    },
    onError: (err: any) => toast.error("Failed to create schedule", { description: err.message })
  });

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/20 backdrop-blur-sm transition-opacity" onClick={() => !createScheduleMutation.isPending && onClose()} />
      <div className="relative bg-white border border-[#e5e5e5] shadow-xl w-full max-w-lg flex flex-col animate-in fade-in zoom-in-95 duration-200 max-h-[90vh]">
        <div className="flex items-center justify-between p-4 border-b border-[#e5e5e5] bg-[#fafafa]/50 shrink-0">
          <h3 className="text-[13px] font-bold uppercase tracking-widest text-[#09090b]">Create Dunning Rule</h3>
          <button onClick={onClose} disabled={createScheduleMutation.isPending} className="text-[#a1a1aa] hover:bg-[#e5e5e5] hover:text-[#09090b] transition-colors p-1 disabled:opacity-50"><X size={16} /></button>
        </div>
        <div className="overflow-y-auto flex-1 bg-[#fafafa]/30">
          <form onSubmit={(e) => { e.preventDefault(); createScheduleMutation.mutate(); }}>
            <div className="p-5 space-y-4">
              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Target Plan</label>
                <select value={planId} onChange={e => setPlanId(e.target.value)} disabled={createScheduleMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50">
                  <option value="">All Plans (Global)</option>
                  {plans?.map((p: any) => <option key={p.id} value={p.id}>{p.name}</option>)}
                </select>
              </div>
              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Message Template *</label>
                <select required value={templateId} onChange={e => setTemplateId(e.target.value)} disabled={createScheduleMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50">
                  <option value="" disabled>Select a template...</option>
                  {templates?.map((t: any) => <option key={t.id} value={t.id}>{t.name}</option>)}
                </select>
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Channel</label>
                  <select value={channel} onChange={e => setChannel(e.target.value)} disabled={createScheduleMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50">
                    <option value="EMAIL">Email</option>
                    <option value="WHATSAPP">WhatsApp</option>
                    <option value="ALL">Email & WhatsApp</option>
                  </select>
                </div>
                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Time of Day (UTC)</label>
                  <input type="time" required value={time} onChange={e => setTime(e.target.value)} disabled={createScheduleMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
                </div>
              </div>
              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Days Relative To Due Date *</label>
                <input type="number" required value={days} onChange={e => setDays(Number(e.target.value))} disabled={createScheduleMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
                <p className="text-[10px] text-[#a1a1aa] mt-1">Negative numbers (-3) send before the due date. Positive numbers (3) send after.</p>
              </div>
            </div>
            <div className="px-5 py-4 border-t border-[#f4f4f5] bg-[#fafafa]/50 flex items-center justify-end gap-2.5">
              <button type="button" onClick={onClose} disabled={createScheduleMutation.isPending} className="h-8 px-4 rounded-sm border border-[#e5e5e5] bg-white text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:bg-[#f4f4f5] hover:text-[#09090b] transition-colors disabled:opacity-50">Cancel</button>
              <button type="submit" disabled={createScheduleMutation.isPending} className="h-8 px-6 rounded-sm bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest flex items-center justify-center gap-2 hover:bg-[#27272a] transition-colors disabled:opacity-50">
                {createScheduleMutation.isPending && <Loader2 size={13} className="animate-spin" />} Create Rule
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>
  );
}
