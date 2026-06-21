import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2, Plus, Zap, Megaphone, X } from "lucide-react";
import { toast } from "sonner";
import { client } from "../../../lib/api-client";
import { cn } from "../../../lib/utils";
import PageLayout from "../../core/components/PageLayout";

export default function AutomationsPage() {
  const queryClient = useQueryClient();
  const [activeTab, setActiveTab] = useState<"reminders" | "broadcast">("reminders");

  const [subject, setSubject] = useState("");
  const [body, setBody] = useState("");
  const [channel, setChannel] = useState("EMAIL");
  const [targetPlanId, setTargetPlanId] = useState("");

  const [isCreateScheduleModalOpen, setIsCreateScheduleModalOpen] = useState(false);
  const [schedulePlanId, setSchedulePlanId] = useState("");
  const [scheduleTemplateId, setScheduleTemplateId] = useState("");
  const [scheduleChannel, setScheduleChannel] = useState("EMAIL");
  const [scheduleDays, setScheduleDays] = useState(0);
  const [scheduleTime, setScheduleTime] = useState("08:00");

  const { data: schedules, isLoading: isSchedulesLoading } = useQuery({
    queryKey: ["community-reminder-schedules"],
    queryFn: async () => {
      const { data, error } = await client.GET("/admin/community/reminder-schedules");
      if (error) throw new Error(error.detail);
      return data;
    }
  });

  const { data: plans } = useQuery({
    queryKey: ["community-plans-lookup"],
    queryFn: async () => {
      const { data } = await client.GET("/admin/community/plans");
      return data || [];
    }
  });

  const { data: templates } = useQuery({
    queryKey: ["community-templates"],
    queryFn: async () => {
      const { data } = await client.GET("/admin/community/templates");
      return data || [];
    }
  });

  const toggleMutation = useMutation({
    mutationFn: async ({ id, is_enabled }: { id: string, is_enabled: boolean }) => {
      const { error } = await client.PUT("/admin/community/reminder-schedules/{id}", {
        params: { path: { id } },
        body: { is_enabled }
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["community-reminder-schedules"] });
    },
    onError: (err: any) => toast.error(err.message)
  });

  const createScheduleMutation = useMutation({
    mutationFn: async () => {
      if (!scheduleTemplateId) throw new Error("A message template is required.");

      const { error } = await client.POST("/admin/community/reminder-schedules", {
        body: {
          plan_id: schedulePlanId || undefined,
          template_id: scheduleTemplateId,
          channel: scheduleChannel,
          days_relative_to_due: Number(scheduleDays),
          time_of_day: scheduleTime,
          is_enabled: true
        }
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("Reminder schedule created successfully.");
      queryClient.invalidateQueries({ queryKey: ["community-reminder-schedules"] });
      setIsCreateScheduleModalOpen(false);
      setSchedulePlanId("");
      setScheduleTemplateId("");
      setScheduleChannel("EMAIL");
      setScheduleDays(0);
      setScheduleTime("08:00");
    },
    onError: (err: any) => toast.error("Failed to create schedule", { description: err.message })
  });

  const broadcastMutation = useMutation({
    mutationFn: async () => {
      const { error } = await client.POST("/admin/community/broadcasts", {
        body: {
          subject,
          body,
          channel,
          target_plan_id: targetPlanId || undefined,
          target_status: "ACTIVE"
        }
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("Broadcast dispatched to the background queue.");
      setSubject("");
      setBody("");
    },
    onError: (err: any) => toast.error(err.message)
  });

  return (
    <PageLayout 
      title="Automations & Broadcasts" 
      description="Manage scheduled reminders and manual mass announcements."
      breadcrumbs={[{ label: "Community", href: "/community/dashboard" }, { label: "Automations" }]}
    >
      <div className="bg-white border border-[#e5e5e5] rounded-none flex flex-col min-h-[600px] relative">
        <div className="flex border-b border-[#e5e5e5] bg-[#fafafa]">
          <button 
            onClick={() => setActiveTab("reminders")}
            className={cn("flex items-center gap-2 px-6 py-4 text-[11px] font-bold uppercase tracking-widest transition-colors border-b-2", activeTab === "reminders" ? "border-[#09090b] text-[#09090b] bg-white" : "border-transparent text-[#71717a] hover:text-[#09090b]")}
          >
            <Zap size={14} /> Dunning Schedules
          </button>
          <button 
            onClick={() => setActiveTab("broadcast")}
            className={cn("flex items-center gap-2 px-6 py-4 text-[11px] font-bold uppercase tracking-widest transition-colors border-b-2", activeTab === "broadcast" ? "border-[#09090b] text-[#09090b] bg-white" : "border-transparent text-[#71717a] hover:text-[#09090b]")}
          >
            <Megaphone size={14} /> Bulk Broadcast
          </button>
        </div>

        {activeTab === "reminders" && (
          <div className="flex flex-col flex-1">
            {/* Header toolbar containing the creation trigger */}
            <div className="flex items-center justify-end p-4 border-b border-[#f4f4f5]">
              <button 
                onClick={() => setIsCreateScheduleModalOpen(true)}
                className="h-8 px-3 bg-white border border-[#e5e5e5] text-[#09090b] text-[11px] font-bold uppercase tracking-widest flex items-center gap-2 hover:bg-[#fafafa] transition-colors"
              >
                <Plus size={14} /> Create Rule
              </button>
            </div>

            {isSchedulesLoading ? (
              <div className="flex items-center justify-center flex-1 py-12">
                <Loader2 className="animate-spin text-[#a1a1aa]" />
              </div>
            ) : schedules?.length === 0 ? (
              /* Rich Empty State replacing the empty table row */
              <div className="flex flex-col items-center justify-center flex-1 py-20 px-4 text-center">
                <div className="h-12 w-12 rounded-full bg-[#f4f4f5] flex items-center justify-center mb-4">
                  <Zap size={24} className="text-[#a1a1aa]" />
                </div>
                <h3 className="text-[14px] font-bold text-[#09090b] mb-2">No Dunning Rules Configured</h3>
                <p className="text-[13px] text-[#71717a] max-w-sm mb-6 leading-relaxed">
                  Automate your revenue collection by scheduling reminders before and after a subscription's due date.
                </p>
                <button
                  onClick={() => setIsCreateScheduleModalOpen(true)}
                  className="h-9 px-4 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest flex items-center gap-2 hover:bg-[#27272a] transition-colors"
                >
                  <Plus size={14} /> Create your first rule
                </button>
              </div>
            ) : (
              <div className="p-0 overflow-x-auto flex-1">
                <table className="w-full text-left text-[13px]">
                  <thead className="bg-[#fafafa]/50 border-b border-[#f4f4f5]">
                    <tr>
                      <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Timing Rule</th>
                      <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Template Content</th>
                      <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Target Plan</th>
                      <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Status</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-[#f4f4f5]">
                    {schedules?.map((schedule) => (
                      <tr key={schedule.id} className={cn("transition-colors", !schedule.is_enabled && "opacity-60 bg-[#fafafa]/50")}>
                        <td className="px-5 py-3.5">
                          <p className="font-bold text-[#09090b]">
                            {schedule.days_relative_to_due < 0 ? `${Math.abs(schedule.days_relative_to_due)} days before due` : 
                             schedule.days_relative_to_due === 0 ? "On due date" : 
                             `${schedule.days_relative_to_due} days after due`}
                          </p>
                          <p className="text-[11px] font-mono text-[#71717a] mt-0.5">at {schedule.time_of_day} UTC</p>
                        </td>
                        <td className="px-5 py-3.5">
                          <p className="text-[12px] font-medium text-[#09090b]">{schedule.template_name}</p>
                          <p className="text-[10px] text-[#71717a] uppercase">{schedule.channel}</p>
                        </td>
                        <td className="px-5 py-3.5 text-[10px] font-bold uppercase tracking-widest text-[#52525b]">{schedule.plan_name || "All Plans"}</td>
                        <td className="px-5 py-3.5">
                          <button 
                            onClick={() => toggleMutation.mutate({ id: schedule.id, is_enabled: !schedule.is_enabled })}
                            disabled={toggleMutation.isPending}
                            className={cn(
                              "relative inline-flex h-5 w-9 items-center rounded-full transition-colors focus:outline-none",
                              schedule.is_enabled ? "bg-[#09090b]" : "bg-[#e5e5e5]"
                            )}
                          >
                            <span className={cn("inline-block h-4 w-4 transform rounded-full bg-white transition-transform", schedule.is_enabled ? "translate-x-4" : "translate-x-1")} />
                          </button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        )}

        {activeTab === "broadcast" && (
          <div className="p-6 md:p-8 flex-1 bg-[#fafafa]/30">
            <form onSubmit={(e) => { e.preventDefault(); broadcastMutation.mutate(); }} className="max-w-2xl space-y-6">
              
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Target Audience</label>
                  <select value={targetPlanId} onChange={e => setTargetPlanId(e.target.value)} className="w-full h-10 border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b]">
                    <option value="">All Active Subscribers (Global)</option>
                    {plans?.map((p: any) => <option key={p.id} value={p.id}>Enrolled in: {p.name}</option>)}
                  </select>
                </div>
                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Delivery Channel</label>
                  <select value={channel} onChange={e => setChannel(e.target.value)} className="w-full h-10 border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b]">
                    <option value="EMAIL">Email Only</option>
                    <option value="WHATSAPP">WhatsApp Only</option>
                    <option value="ALL">Email & WhatsApp</option>
                  </select>
                </div>
              </div>

              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Subject Line (Email Only)</label>
                <input required value={subject} onChange={e => setSubject(e.target.value)} className="w-full h-10 border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b]" placeholder="e.g. Important update regarding next week's session" />
              </div>

              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Message Body (Markdown Supported)</label>
                <textarea required value={body} onChange={e => setBody(e.target.value)} rows={8} className="w-full p-3 border border-[#e5e5e5] bg-white text-[13px] focus:outline-none focus:border-[#09090b] resize-y font-mono" placeholder="Write your announcement here..." />
              </div>

              <div className="pt-2">
                <button type="submit" disabled={broadcastMutation.isPending} className="h-10 px-8 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest hover:bg-[#27272a] transition-colors flex items-center gap-2 disabled:opacity-50">
                  {broadcastMutation.isPending ? <Loader2 size={14} className="animate-spin" /> : <Megaphone size={14} />} 
                  Dispatch Broadcast
                </button>
              </div>

            </form>
          </div>
        )}
      </div>

      {/* Create Dunning Schedule Modal Overlay */}
      {isCreateScheduleModalOpen && (
        <div className="fixed inset-0 z-[60] flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/20 backdrop-blur-sm transition-opacity" onClick={() => !createScheduleMutation.isPending && setIsCreateScheduleModalOpen(false)} />
          <div className="relative bg-white border border-[#e5e5e5] shadow-xl w-full max-w-lg flex flex-col animate-in fade-in zoom-in-95 duration-200">
            <div className="flex items-center justify-between p-4 border-b border-[#e5e5e5] bg-[#fafafa]/50 shrink-0">
              <h3 className="text-[13px] font-bold uppercase tracking-widest text-[#09090b]">Create Dunning Rule</h3>
              <button onClick={() => setIsCreateScheduleModalOpen(false)} disabled={createScheduleMutation.isPending} className="text-[#a1a1aa] hover:bg-[#e5e5e5] hover:text-[#09090b] transition-colors p-1 disabled:opacity-50"><X size={16} /></button>
            </div>
            <form onSubmit={(e) => { e.preventDefault(); createScheduleMutation.mutate(); }}>
              <div className="p-5 space-y-4">
                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Target Plan</label>
                  <select value={schedulePlanId} onChange={e => setSchedulePlanId(e.target.value)} disabled={createScheduleMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50">
                    <option value="">All Plans (Global)</option>
                    {plans?.map((p: any) => <option key={p.id} value={p.id}>{p.name}</option>)}
                  </select>
                </div>
                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Message Template *</label>
                  <select required value={scheduleTemplateId} onChange={e => setScheduleTemplateId(e.target.value)} disabled={createScheduleMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50">
                    <option value="" disabled>Select a template...</option>
                    {templates?.map((t: any) => <option key={t.id} value={t.id}>{t.name}</option>)}
                  </select>
                </div>
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Channel</label>
                    <select value={scheduleChannel} onChange={e => setScheduleChannel(e.target.value)} disabled={createScheduleMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50">
                      <option value="EMAIL">Email</option>
                      <option value="WHATSAPP">WhatsApp</option>
                      <option value="ALL">Email & WhatsApp</option>
                    </select>
                  </div>
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Time of Day (UTC)</label>
                    <input type="time" required value={scheduleTime} onChange={e => setScheduleTime(e.target.value)} disabled={createScheduleMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
                  </div>
                </div>
                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Days Relative To Due Date *</label>
                  <input type="number" required value={scheduleDays} onChange={e => setScheduleDays(Number(e.target.value))} disabled={createScheduleMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
                  <p className="text-[10px] text-[#a1a1aa] mt-1">Negative numbers (-3) send before the due date. Positive numbers (3) send after.</p>
                </div>
              </div>
              <div className="px-5 py-3 border-t border-[#f4f4f5] bg-[#fafafa]/50 flex items-center justify-end gap-2.5">
                <button type="button" onClick={() => setIsCreateScheduleModalOpen(false)} disabled={createScheduleMutation.isPending} className="h-8 px-4 rounded-sm border border-[#e5e5e5] bg-white text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:bg-[#f4f4f5] hover:text-[#09090b] transition-colors disabled:opacity-50">Cancel</button>
                <button type="submit" disabled={createScheduleMutation.isPending} className="h-8 px-6 rounded-sm bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest flex items-center justify-center gap-2 hover:bg-[#27272a] transition-colors disabled:opacity-50">
                  {createScheduleMutation.isPending && <Loader2 size={13} className="animate-spin" />} Create Rule
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </PageLayout>
  );
}
