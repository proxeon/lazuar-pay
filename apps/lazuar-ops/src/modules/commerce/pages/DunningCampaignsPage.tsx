import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2, Plus, Zap, ShieldCheck } from "lucide-react";
import { Link, useNavigate } from "react-router-dom";
import { toast } from "sonner";
import { client } from "../../../lib/api-client";
import { cn } from "../../../lib/utils";
import PageLayout from "../../core/components/PageLayout";

export default function DunningCampaignsPage() {
  const queryClient = useQueryClient();
  const navigate = useNavigate();

  const { data: campaigns, isLoading: isCampaignsLoading } = useQuery({
    queryKey: ["commerce-dunning-campaigns"],
    queryFn: async () => {
      const { data, error } = await client.GET("/admin/commerce/dunning-campaigns");
      if (error) throw new Error(error.detail);
      return data;
    }
  });

  const deployDefaultsMutation = useMutation({
    mutationFn: async () => {
      const { error } = await client.POST("/admin/commerce/dunning-campaigns/defaults");
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["commerce-dunning-campaigns"] });
      toast.success("Standard recovery strategy deployed.");
    },
    onError: (err: any) => toast.error("Failed to deploy strategy", { description: err.message })
  });

  return (
    <PageLayout 
      title="Dunning Campaigns" 
      description="Automate revenue recovery by defining communication sequences and subscription escalation rules for failed payments."
      breadcrumbs={[{ label: "Commerce", href: "/commerce/dashboard" }, { label: "Dunning Campaigns" }]}
      actionButton={
        <Link 
          to="/commerce/dunning-campaigns/new"
          className="h-9 px-4 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest flex items-center gap-2 hover:bg-[#27272a] transition-colors"
        >
          <Plus size={14} /> Create Campaign
        </Link>
      }
    >
      <div className="bg-white border border-[#e5e5e5] rounded-none flex flex-col min-h-[600px] relative">
        {isCampaignsLoading ? (
          <div className="flex items-center justify-center flex-1 py-12">
            <Loader2 className="animate-spin text-[#a1a1aa]" />
          </div>
        ) : campaigns?.length === 0 ? (
          <div className="flex flex-col items-center justify-center flex-1 py-20 px-4 text-center">
            <div className="h-16 w-16 rounded-full bg-emerald-50 border border-emerald-100 flex items-center justify-center mb-6 shadow-sm">
              <ShieldCheck size={28} className="text-emerald-600" />
            </div>
            <h3 className="text-[16px] font-bold text-[#09090b] mb-2">Revenue Recovery Engine</h3>
            <p className="text-[13px] text-[#71717a] max-w-md mb-8 leading-relaxed">
              Dunning Campaigns tell Lazuar exactly when to chase failing payments and when to cut off access. Deploy the standard strategy to minimize involuntary churn.
            </p>
            <div className="flex flex-col sm:flex-row items-center gap-3">
              <button
                onClick={() => deployDefaultsMutation.mutate()}
                disabled={deployDefaultsMutation.isPending}
                className="h-10 px-6 bg-emerald-600 text-white text-[11px] font-bold uppercase tracking-widest flex items-center gap-2 hover:bg-emerald-700 transition-colors disabled:opacity-50 shadow-sm"
              >
                {deployDefaultsMutation.isPending ? <Loader2 size={14} className="animate-spin" /> : <Zap size={14} />} 
                Deploy Recommended Strategy
              </button>
            </div>
          </div>
        ) : (
          <div className="p-0 overflow-x-auto flex-1">
            <table className="w-full text-left text-[13px] min-w-[950px]">
              <thead className="bg-[#fafafa]/50 border-b border-[#f4f4f5] select-none">
                <tr>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[20%]">Campaign</th>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[15%]">Configuration</th>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[15%]">Final Escalation</th>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[15%] text-right">Recovered Revenue</th>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[15%] text-center">Saved / Churned</th>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[10%] text-right">Status</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#f4f4f5]">
                {campaigns?.map((campaign) => (
                  <tr 
                    key={campaign.id} 
                    onClick={() => navigate(`/commerce/dunning-campaigns/${campaign.id}`)}
                    className={cn("transition-colors cursor-pointer group hover:bg-[#fafafa]", !campaign.is_active && "opacity-60 bg-[#fafafa]/50")}
                  >
                    <td className="px-5 py-4">
                      <p className="font-bold text-[#09090b] group-hover:text-blue-600 transition-colors">
                        {campaign.name}
                      </p>
                      <p className="text-[10px] font-mono text-[#71717a] mt-1">Priority: {campaign.priority_order}</p>
                    </td>
                    <td className="px-5 py-4">
                      <p className="text-[11px] font-bold text-[#52525b] uppercase tracking-wider mb-0.5">
                        {campaign.target_product_ids?.length ? `${campaign.target_product_ids.length} Products` : "All Products"}
                      </p>
                      <p className="text-[11px] text-[#71717a]">
                        {campaign.steps?.length || 0} Steps
                      </p>
                    </td>
                    <td className="px-5 py-4">
                      <p className={cn("text-[11px] font-bold uppercase tracking-widest", campaign.final_action === "CANCEL" ? "text-rose-600" : campaign.final_action === "SUSPEND" ? "text-amber-600" : "text-[#71717a]")}>
                        {campaign.final_action}
                      </p>
                      <p className="text-[11px] text-[#71717a] mt-0.5">After {campaign.grace_period_days} Days</p>
                    </td>
                    <td className="px-5 py-4 text-right">
                      <span className="font-mono font-bold text-emerald-600">RM {campaign.recovered_revenue.toFixed(2)}</span>
                    </td>
                    <td className="px-5 py-4 text-center">
                      <div className="flex items-center justify-center gap-2 text-[11px] font-mono">
                        <span className="text-emerald-600 font-bold" title="Saved Subscriptions">{campaign.saved_subscriptions}</span>
                        <span className="text-[#d4d4d8]">/</span>
                        <span className="text-rose-600 font-bold" title="Churned Subscriptions">{campaign.churned_subscriptions}</span>
                      </div>
                    </td>
                    <td className="px-5 py-4 text-right">
                       <span className={cn(
                          "text-[9px] px-1.5 py-0.5 border font-bold uppercase tracking-widest whitespace-nowrap inline-block",
                          campaign.is_active ? "bg-emerald-50 text-emerald-700 border-emerald-200" : "bg-zinc-100 text-zinc-500 border-zinc-200"
                        )}>
                          {campaign.is_active ? "Active" : "Archived"}
                        </span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </PageLayout>
  );
}
