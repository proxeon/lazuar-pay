import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Zap, Megaphone } from "lucide-react";
import { client } from "../../../lib/api-client";
import { cn } from "../../../lib/utils";
import PageLayout from "../../core/components/PageLayout";
import DunningSchedulesTab from "../components/automations/DunningSchedulesTab";
import BulkBroadcastTab from "../components/automations/BulkBroadcastTab";

export default function AutomationsPage() {
  const [activeTab, setActiveTab] = useState<"reminders" | "broadcast">("reminders");

  const { data: plans } = useQuery({
    queryKey: ["community-plans-lookup"],
    queryFn: async () => {
      const { data } = await client.GET("/admin/community/plans");
      return data || [];
    }
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

        {activeTab === "reminders" && <DunningSchedulesTab plans={plans} />}
        {activeTab === "broadcast" && <BulkBroadcastTab plans={plans} />}
      </div>
    </PageLayout>
  );
}
