import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2, Plus, Zap } from "lucide-react";
import { toast } from "sonner";
import { client, type components } from "../../../../lib/api-client";
import { cn } from "../../../../lib/utils";
import CreateScheduleModal from "./CreateScheduleModal";
import ScheduleDetailPanel from "./ScheduleDetailPanel";

type CommunityReminderScheduleDto = components["schemas"]["Community.CommunityReminderScheduleDto"];

interface DunningSchedulesTabProps {
  plans?: any[];
  isCreateModalOpen: boolean;
  onOpenCreateModal: () => void;
  onCloseCreateModal: () => void;
}

export const formatTiming = (days: number) => {
  if (days < 0) return `${Math.abs(days)} Days Before Due`;
  if (days === 0) return "On Due Date";
  return `${days} Days After Due`;
};

export default function DunningSchedulesTab({ plans, isCreateModalOpen, onOpenCreateModal, onCloseCreateModal }: DunningSchedulesTabProps) {
  const queryClient = useQueryClient();
  const [selectedSchedule, setSelectedSchedule] = useState<CommunityReminderScheduleDto | null>(null);

  const { data: schedules, isLoading: isSchedulesLoading } = useQuery({
    queryKey: ["community-reminder-schedules"],
    queryFn: async () => {
      const { data, error } = await client.GET("/admin/community/reminder-schedules");
      if (error) throw new Error(error.detail);
      return data;
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
      return { id, is_enabled };
    },
    onSuccess: (data) => {
      queryClient.invalidateQueries({ queryKey: ["community-reminder-schedules"] });
      setSelectedSchedule(prev => prev && prev.id === data.id ? { ...prev, is_enabled: data.is_enabled } : prev);
      toast.success(`Rule ${data.is_enabled ? "enabled" : "paused"} successfully.`);
    },
    onError: (err: any) => toast.error(err.message)
  });

  return (
    <div className="flex flex-col flex-1">
      {isSchedulesLoading ? (
        <div className="flex items-center justify-center flex-1 py-12">
          <Loader2 className="animate-spin text-[#a1a1aa]" />
        </div>
      ) : schedules?.length === 0 ? (
        <div className="flex flex-col items-center justify-center flex-1 py-20 px-4 text-center">
          <div className="h-12 w-12 rounded-full bg-[#f4f4f5] flex items-center justify-center mb-4">
            <Zap size={24} className="text-[#a1a1aa]" />
          </div>
          <h3 className="text-[14px] font-bold text-[#09090b] mb-2">No Dunning Rules Configured</h3>
          <p className="text-[13px] text-[#71717a] max-w-sm mb-6 leading-relaxed">
            Automate your revenue collection by scheduling reminders before and after a subscription's due date.
          </p>
          <button
            onClick={onOpenCreateModal}
            className="h-9 px-4 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest flex items-center gap-2 hover:bg-[#27272a] transition-colors"
          >
            <Plus size={14} /> Create your first rule
          </button>
        </div>
      ) : (
        <div className="p-0 overflow-x-auto flex-1">
          <table className="w-full text-left text-[13px]">
            <thead className="bg-[#fafafa]/50 border-b border-[#f4f4f5] select-none">
              <tr>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Timing Rule</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Template Content</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Target Plan</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Status</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#f4f4f5]">
              {schedules?.map((schedule) => (
                <tr 
                  key={schedule.id} 
                  onClick={() => setSelectedSchedule(schedule)}
                  className={cn("transition-colors cursor-pointer group hover:bg-[#fafafa]", !schedule.is_enabled && "opacity-60 bg-[#fafafa]/50")}
                >
                  <td className="px-5 py-3.5">
                    <p className="font-bold text-[#09090b] group-hover:text-blue-600 transition-colors">
                      {formatTiming(schedule.days_relative_to_due)}
                    </p>
                    <p className="text-[11px] font-mono text-[#71717a] mt-0.5">at {schedule.time_of_day} UTC</p>
                  </td>
                  <td className="px-5 py-3.5">
                    <p className="text-[12px] font-medium text-[#09090b]">{schedule.template_name}</p>
                    <p className="text-[10px] text-[#71717a] uppercase">{schedule.channel}</p>
                  </td>
                  <td className="px-5 py-3.5 text-[10px] font-bold uppercase tracking-widest text-[#52525b]">
                    {schedule.plan_name || "All Plans"}
                  </td>
                  <td className="px-5 py-3.5">
                    <button 
                      onClick={(e) => {
                        e.stopPropagation();
                        toggleMutation.mutate({ id: schedule.id, is_enabled: !schedule.is_enabled });
                      }}
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

      {isCreateModalOpen && (
        <CreateScheduleModal 
          plans={plans} 
          templates={templates} 
          onClose={onCloseCreateModal} 
        />
      )}

      <ScheduleDetailPanel 
        schedule={selectedSchedule} 
        plans={plans} 
        templates={templates} 
        onClose={() => setSelectedSchedule(null)} 
        formatTiming={formatTiming}
      />
    </div>
  );
}
