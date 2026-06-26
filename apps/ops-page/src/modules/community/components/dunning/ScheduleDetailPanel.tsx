import { useState, useEffect } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2, Zap, Target, Clock, MessageSquare, Edit2, Pause, Play, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { client, type components } from "../../../../lib/api-client";
import { cn } from "../../../../lib/utils";
import SidePanel from "../../../core/components/SidePanel";

type CommunityReminderScheduleDto = components["schemas"]["Community.CommunityReminderScheduleDto"];

interface ScheduleDetailPanelProps {
  schedule: CommunityReminderScheduleDto | null;
  plans?: any[];
  templates?: any[];
  onClose: () => void;
  formatTiming: (days: number) => string;
}

export default function ScheduleDetailPanel({ schedule, plans, templates, onClose, formatTiming }: ScheduleDetailPanelProps) {
  const queryClient = useQueryClient();
  const [isEditing, setIsEditing] = useState(false);
  
  const [planId, setPlanId] = useState("");
  const [templateId, setTemplateId] = useState("");
  const [channel, setChannel] = useState("EMAIL");
  const [days, setDays] = useState<number | string>(0);
  const [time, setTime] = useState("08:00");

  useEffect(() => {
    if (schedule && isEditing) {
      setPlanId(schedule.plan_id || "");
      setTemplateId(schedule.template_id);
      setChannel(schedule.channel);
      setDays(schedule.days_relative_to_due);
      setTime(schedule.time_of_day);
    }
  }, [schedule, isEditing]);

  const toggleMutation = useMutation({
    mutationFn: async (is_enabled: boolean) => {
      if (!schedule) throw new Error("No schedule selected");
      const { error } = await client.PUT("/admin/community/reminder-schedules/{id}", {
        params: { path: { id: schedule.id } },
        body: { is_enabled }
      });
      if (error) throw new Error(error.detail);
      return is_enabled;
    },
    onSuccess: (is_enabled) => {
      queryClient.invalidateQueries({ queryKey: ["community-reminder-schedules"] });
      toast.success(`Rule ${is_enabled ? "enabled" : "paused"} successfully.`);
      onClose();
    },
    onError: (err: any) => toast.error(err.message)
  });

  const editMutation = useMutation({
    mutationFn: async () => {
      if (!schedule) throw new Error("No schedule selected");
      if (!templateId) throw new Error("A message template is required.");
      
      const { error } = await client.PUT("/admin/community/reminder-schedules/{id}", {
        params: { path: { id: schedule.id } },
        body: {
          plan_id: planId || undefined,
          template_id: templateId,
          channel,
          days_relative_to_due: Number(days),
          time_of_day: time,
          is_enabled: schedule.is_enabled
        }
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("Reminder schedule updated successfully.");
      queryClient.invalidateQueries({ queryKey: ["community-reminder-schedules"] });
      setIsEditing(false);
      onClose();
    },
    onError: (err: any) => toast.error("Failed to update schedule", { description: err.message })
  });

  const deleteMutation = useMutation({
    mutationFn: async () => {
      if (!schedule) throw new Error("No schedule selected");
      const { error } = await client.DELETE("/admin/community/reminder-schedules/{id}", {
        params: { path: { id: schedule.id } }
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("Reminder schedule deleted permanently.");
      queryClient.invalidateQueries({ queryKey: ["community-reminder-schedules"] });
      onClose();
    },
    onError: (err: any) => toast.error("Failed to delete schedule", { description: err.message })
  });

  const isActionLoading = toggleMutation.isPending || editMutation.isPending || deleteMutation.isPending;

  return (
    <SidePanel
      isOpen={!!schedule}
      onClose={() => { onClose(); setIsEditing(false); }}
      title="Dunning Rule"
      subtitle={schedule ? formatTiming(schedule.days_relative_to_due) : ""}
      disableOutsideClick={isActionLoading || isEditing}
    >
      {schedule && !isEditing && (
        <div className="space-y-8 animate-in fade-in duration-200">
          <div className="flex items-center justify-between border-b border-[#f4f4f5] pb-6">
            <div className="flex items-center gap-3">
              <div className={cn("p-2 rounded-full", schedule.is_enabled ? "bg-emerald-50" : "bg-zinc-100")}>
                <Zap size={18} className={schedule.is_enabled ? "text-emerald-600" : "text-zinc-400"} />
              </div>
              <div>
                <span className="text-[14px] font-bold text-[#09090b] block">{schedule.is_enabled ? "Rule is Active" : "Rule is Paused"}</span>
                <span className="text-[11px] text-[#71717a] font-mono">ID: {schedule.id.substring(0,8)}</span>
              </div>
            </div>
          </div>

          <div className="space-y-4">
            <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Targeting Scope</h4>
            <div className="flex items-center gap-3 p-3 bg-[#fafafa] border border-[#e5e5e5] rounded-sm text-[12px]">
              <Target size={15} className="text-[#a1a1aa] shrink-0" />
              <span className="font-semibold text-[#09090b]">{schedule.plan_name || "All Plans (Global)"}</span>
            </div>
          </div>

          <div className="space-y-4">
            <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Messaging Configuration</h4>
            <div className="grid grid-cols-2 gap-4 text-[12px]">
              <div>
                <span className="text-[#a1a1aa] block mb-1">Time of Day</span>
                <div className="flex items-center gap-1.5 font-mono font-bold text-[#09090b]">
                  <Clock size={13} className="text-[#71717a]" /> {schedule.time_of_day} UTC
                </div>
              </div>
              <div>
                <span className="text-[#a1a1aa] block mb-1">Delivery Channel</span>
                <span className="font-medium text-[#09090b]">{schedule.channel}</span>
              </div>
              <div className="col-span-2">
                <span className="text-[#a1a1aa] block mb-1">Assigned Template</span>
                <div className="flex items-center gap-1.5 font-medium text-[#09090b]">
                  <MessageSquare size={13} className="text-[#71717a]" /> {schedule.template_name}
                </div>
              </div>
            </div>
          </div>

          <div className="space-y-4 pt-4">
            <div className="grid grid-cols-2 gap-2">
              <button 
                onClick={() => setIsEditing(true)} 
                disabled={isActionLoading} 
                className="h-8 border border-[#e5e5e5] bg-white text-[10px] font-bold uppercase tracking-widest text-[#09090b] hover:bg-[#f4f4f5] transition-colors disabled:opacity-50 flex items-center justify-center gap-1.5"
              >
                <Edit2 size={12} /> Edit Config
              </button>
              
              <button 
                onClick={() => toggleMutation.mutate(!schedule.is_enabled)}
                disabled={isActionLoading} 
                className="h-8 border border-[#e5e5e5] bg-white text-[10px] font-bold uppercase tracking-widest text-[#09090b] hover:bg-[#f4f4f5] transition-colors disabled:opacity-50 flex items-center justify-center gap-1.5"
              >
                {toggleMutation.isPending ? <Loader2 size={12} className="animate-spin" /> : schedule.is_enabled ? <Pause size={12} /> : <Play size={12} />}
                {schedule.is_enabled ? "Pause Rule" : "Resume Rule"}
              </button>

              <button 
                onClick={() => { if(window.confirm("Permanently delete this dunning rule?")) deleteMutation.mutate(); }} 
                disabled={isActionLoading} 
                className="h-8 col-span-2 border border-rose-200 bg-rose-50 text-[10px] font-bold uppercase tracking-widest text-rose-700 hover:bg-rose-100 transition-colors disabled:opacity-50 flex items-center justify-center gap-1.5"
              >
                {deleteMutation.isPending ? <Loader2 size={12} className="animate-spin" /> : <Trash2 size={12} />} Delete Rule
              </button>
            </div>
          </div>
        </div>
      )}

      {schedule && isEditing && (
        <div className="absolute inset-0 bg-white z-10 flex flex-col animate-in slide-in-from-right duration-200">
          <div className="flex items-center justify-between p-5 border-b border-[#e5e5e5] bg-[#fafafa] shrink-0">
            <div>
              <h3 className="text-[15px] font-bold text-[#09090b]">Edit Dunning Rule</h3>
              <p className="text-[11px] font-mono text-[#71717a] mt-0.5">ID: {schedule.id.substring(0,8)}</p>
            </div>
          </div>
          
          <div className="flex-1 overflow-y-auto">
            <form onSubmit={(e) => { e.preventDefault(); editMutation.mutate(); }}>
              <div className="p-6 space-y-5">
                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Target Plan</label>
                  <select value={planId} onChange={e => setPlanId(e.target.value)} disabled={editMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50">
                    <option value="">All Plans (Global)</option>
                    {plans?.map((p: any) => <option key={p.id} value={p.id}>{p.name}</option>)}
                  </select>
                </div>
                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Message Template *</label>
                  <select required value={templateId} onChange={e => setTemplateId(e.target.value)} disabled={editMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50">
                    <option value="" disabled>Select a template...</option>
                    {templates?.map((t: any) => <option key={t.id} value={t.id}>{t.name}</option>)}
                  </select>
                </div>
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Channel</label>
                    <select value={channel} onChange={e => setChannel(e.target.value)} disabled={editMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50">
                      <option value="EMAIL">Email</option>
                      <option value="WHATSAPP">WhatsApp</option>
                      <option value="ALL">Email & WhatsApp</option>
                    </select>
                  </div>
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Time of Day (UTC)</label>
                    <input type="time" required value={time} onChange={e => setTime(e.target.value)} disabled={editMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
                  </div>
                </div>
                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Days Relative To Due Date *</label>
                  <input type="number" required value={days} onChange={e => setDays(Number(e.target.value))} disabled={editMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
                  <p className="text-[10px] text-[#a1a1aa] mt-1">Negative numbers (-3) send before the due date. Positive numbers (3) send after.</p>
                </div>
              </div>

              <div className="px-5 py-4 border-t border-[#e5e5e5] bg-[#fafafa]/50 flex items-center justify-end gap-2.5 shrink-0 absolute bottom-0 left-0 right-0">
                <button type="button" onClick={() => setIsEditing(false)} disabled={editMutation.isPending} className="h-8 px-4 rounded-sm border border-[#e5e5e5] bg-white text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:bg-[#f4f4f5] hover:text-[#09090b] transition-colors disabled:opacity-50">Cancel</button>
                <button type="submit" disabled={editMutation.isPending} className="h-8 px-6 rounded-sm bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest flex items-center justify-center gap-2 hover:bg-[#27272a] transition-colors disabled:opacity-50">
                  {editMutation.isPending && <Loader2 size={13} className="animate-spin" />} Save Changes
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </SidePanel>
  );
}
