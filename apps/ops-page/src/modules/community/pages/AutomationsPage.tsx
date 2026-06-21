import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2, Plus, Zap, Megaphone, Check } from "lucide-react";
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

  const broadcastMutation = useMutation({
    mutationFn: async () => {
      // NOTE: Because SendBroadcastCommand uses AgentTool, it executes via the universal action executor 
      // instead of a dedicated endpoint to keep the API surface area small.
      const payload = {
        Subject: subject,
        Body: body,
        Channel: channel,
        TargetPlanId: targetPlanId || undefined
      };
      
      const { error } = await client.POST("/ops/execute-action", {
        body: {
          idempotency_key: crypto.randomUUID(),
          tool_name: "SendBroadcastCommand",
          intent_title: "Dispatch Broadcast",
          severity: "high",
          human_readable_summary: "Manual UI dispatch",
          command_payload: payload
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
      <div className="bg-white border border-[#e5e5e5] rounded-none flex flex-col min-h-[600px]">
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
          <div className="p-0 overflow-x-auto flex-1">
            <table className="w-full text-left text-[13px]">
              <thead className="bg-[#fafafa]/50 border-b border-[#f4f4f5]">
                <tr>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Timing Rule</th>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Template</th>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Channel</th>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Status</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#f4f4f5]">
                {isSchedulesLoading ? (
                  <tr><td colSpan={4} className="py-12 text-center"><Loader2 className="animate-spin text-[#a1a1aa] mx-auto" /></td></tr>
                ) : schedules?.length === 0 ? (
                  <tr><td colSpan={4} className="py-12 text-center text-[#71717a] text-sm">No reminder schedules configured.</td></tr>
                ) : (
                  schedules?.map((schedule) => (
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
                        <p className="text-[10px] text-[#71717a] uppercase">{schedule.plan_name || "All Plans"}</p>
                      </td>
                      <td className="px-5 py-3.5 text-[10px] font-bold uppercase tracking-widest text-[#52525b]">{schedule.channel}</td>
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
                  ))
                )}
              </tbody>
            </table>
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
    </PageLayout>
  );
}
