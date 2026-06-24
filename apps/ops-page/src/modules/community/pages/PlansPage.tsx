import { useState, useEffect } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2, Plus, AlertTriangle, Link as LinkIcon, Edit2, Archive, RotateCcw, MessagesSquare, CheckCircle2 } from "lucide-react";
import { toast } from "sonner";
import { client, type EntitlementDto, type components } from "../../../lib/api-client";
import { useOutletContext } from "react-router-dom";
import PageLayout from "../../core/components/PageLayout";
import SidePanel from "../../core/components/SidePanel";
import QuickCopy from "../../core/components/QuickCopy";
import CreatePlanForm from "../../../components/forms/CreatePlanForm";
import { cn } from "../../../lib/utils";

type CommunityPlanDto = components["schemas"]["Community.CommunityPlanDto"];

export default function PlansPage() {
  const queryClient = useQueryClient();
  const { activeWorkspaceId } = useOutletContext<{ activeWorkspaceId: string | null }>();
  
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [selectedPlan, setSelectedPlan] = useState<CommunityPlanDto | null>(null);
  const [isEditingInSlider, setIsEditingInSlider] = useState(false);
  const [isActionLoading, setIsActionLoading] = useState(false);

  const { data: plans, isLoading } = useQuery({
    queryKey: ["community-plans"],
    queryFn: async () => {
      const { data, error } = await client.GET("/admin/community/plans");
      if (error) throw new Error(error.detail);
      return data;
    }
  });

  const { data: paymentConfig, isLoading: isConfigLoading } = useQuery({
    queryKey: ["payment-config"],
    queryFn: async () => {
      const { data, error } = await client.GET("/admin/community/payment-config");
      if (error && error.status !== 404) throw new Error(error.detail);
      return data || null;
    },
    retry: false
  });

  const { data: entitlements } = useQuery({
    queryKey: ["entitlements"],
    queryFn: async () => {
      const { data } = await client.GET("/one/me/entitlements");
      return data as EntitlementDto[];
    }
  });

  const activeWorkspaceSlug = entitlements?.find(e => e.workspace_id === activeWorkspaceId)?.workspace_slug;

  const createMutation = useMutation({
    mutationFn: async (payload: any) => {
      const { error } = await client.POST("/admin/community/plans", { body: payload });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("Plan created successfully");
      queryClient.invalidateQueries({ queryKey: ["community-plans"] });
      setIsCreateModalOpen(false);
    },
    onError: (err: any) => toast.error("Failed to create plan", { description: err.message })
  });

  const editMutation = useMutation({
    mutationFn: async (payload: any) => {
      const { id, ...body } = payload;
      const { error } = await client.PUT("/admin/community/plans/{id}", {
        params: { path: { id } },
        body
      });
      if (error) throw new Error(error.detail);
    },
    onMutate: () => setIsActionLoading(true),
    onSettled: () => setIsActionLoading(false),
    onSuccess: (_, variables) => {
      toast.success("Plan configuration saved successfully");
      queryClient.invalidateQueries({ queryKey: ["community-plans"] });
      
      setSelectedPlan(prev => {
        if (!prev) return null;
        if (variables.is_active !== undefined) return { ...prev, is_active: variables.is_active };
        return { ...prev, ...variables }; 
      });
      
      setIsEditingInSlider(false);
    },
    onError: (err: any) => toast.error("Failed to update plan", { description: err.message })
  });

  const softDeleteMutation = useMutation({
    mutationFn: async (id: string) => {
      const { error } = await client.DELETE("/admin/community/plans/{id}", {
        params: { path: { id } }
      });
      if (error) throw new Error(error.detail);
    },
    onMutate: () => setIsActionLoading(true),
    onSettled: () => setIsActionLoading(false),
    onSuccess: () => {
      toast.success("Plan archived successfully");
      queryClient.invalidateQueries({ queryKey: ["community-plans"] });
      setSelectedPlan(prev => prev ? { ...prev, is_active: false } : null);
    },
    onError: (err: any) => toast.error("Failed to archive plan", { description: err.message })
  });

  // Replaced brittle client.fetch check with standard DOM window check
  const generateCheckoutUrl = (planSlug: string) => {
    if (!activeWorkspaceSlug) return "";
    const baseUrl = window.location.hostname === "localhost" ? "http://localhost:3021" : "https://community.lazuar.com";
    return `${baseUrl}/${activeWorkspaceSlug}/${planSlug}/checkout`;
  };

  const copyCheckoutLink = (planSlug: string) => {
    const url = generateCheckoutUrl(planSlug);
    if (!url) {
      toast.error("Could not resolve workspace slug.");
      return;
    }
    navigator.clipboard.writeText(url);
    toast.success("Checkout link copied to clipboard.");
  };

  const isGatewayActive = paymentConfig?.is_active ?? false;

  return (
    <PageLayout 
      title="Plans & Products" 
      description="Manage your subscription tiers, limits, and pricing."
      breadcrumbs={[{ label: "Community", href: "/community/dashboard" }, { label: "Plans" }]}
      actionButton={
        <button 
          onClick={() => setIsCreateModalOpen(true)}
          className="h-9 px-4 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest flex items-center gap-2 hover:bg-[#27272a] transition-colors"
        >
          <Plus size={14} /> Create Plan
        </button>
      }
    >
      {!isConfigLoading && !isGatewayActive && (
        <div className="mb-6 bg-amber-50 border border-amber-200 p-4 flex items-start gap-3">
          <AlertTriangle className="text-amber-600 mt-0.5 shrink-0" size={16} />
          <div>
            <h4 className="text-[12px] font-bold text-amber-800 uppercase tracking-widest">Gateway Not Configured</h4>
            <p className="text-[13px] text-amber-700 mt-1 leading-relaxed">
              Your payment gateway is currently disabled or missing API keys. Users will not be able to complete public checkouts until you configure it in Settings.
            </p>
          </div>
        </div>
      )}

      {isLoading ? (
        <div className="flex justify-center p-12"><Loader2 className="animate-spin text-[#a1a1aa]" /></div>
      ) : (
        <div className="bg-white border border-[#e5e5e5] rounded-none overflow-hidden">
          <div className="w-full overflow-x-auto min-h-[320px]">
            <table className="w-full text-left text-[13px] min-w-[750px]">
              <thead className="bg-[#fafafa] border-b border-[#e5e5e5] select-none">
                <tr>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[35%]">Plan Details</th>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[20%]">Price (MYR)</th>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[15%]">Enrolled</th>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[15%]">Grace Period</th>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[10%]">Status</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#f4f4f5]">
                {plans?.length === 0 ? (
                  <tr>
                    <td colSpan={5} className="py-12 text-center text-[13px] text-[#71717a]">
                      No community plans configured. Click "Create Plan" to begin.
                    </td>
                  </tr>
                ) : (
                  plans?.map((plan) => (
                    <tr 
                      key={plan.id} 
                      onClick={() => setSelectedPlan(plan)}
                      className={cn(
                        "hover:bg-[#fafafa] transition-colors cursor-pointer group",
                        !plan.is_active && "opacity-60 bg-[#fafafa]/30"
                      )}
                    >
                      <td className="px-5 py-3.5">
                        <div className="flex items-center gap-2 mb-1">
                          <span className="font-bold text-[#09090b] text-[13px] group-hover:text-blue-600 transition-colors">{plan.name}</span>
                          <span className="text-[8px] font-bold uppercase tracking-wider bg-zinc-100 text-zinc-600 px-1.5 py-0.5 border border-zinc-200">
                            {plan.audience}
                          </span>
                        </div>
                        <p className="text-[12px] text-[#71717a] line-clamp-1 max-w-sm">
                          {plan.short_description || "No description provided."}
                        </p>
                      </td>
                      <td className="px-5 py-3.5">
                        <div className="flex items-center gap-1.5">
                          <div className="font-mono text-[#09090b]">
                            <span className="font-bold">RM {plan.price.toFixed(2)}</span>
                            <span className="text-[11px] text-[#71717a]"> / {plan.interval}</span>
                          </div>
                          <QuickCopy text={plan.price.toString()} iconSize={10} className="opacity-0 group-hover:opacity-100" />
                        </div>
                      </td>
                      <td className="px-5 py-3.5">
                        <div className="font-mono text-[#52525b]">
                          <span className="font-bold text-[#09090b]">{plan.enrolled_count}</span>
                          <span className="text-[11px] text-[#a1a1aa]"> / {plan.max_capacity || "∞"}</span>
                        </div>
                      </td>
                      <td className="px-5 py-3.5">
                        <span className="text-[12px] text-[#52525b] font-medium">{plan.grace_period_days} days</span>
                      </td>
                      <td className="px-5 py-3.5">
                        <span className={cn(
                          "text-[9px] px-1.5 py-0.5 border font-bold uppercase tracking-widest whitespace-nowrap",
                          plan.is_active ? "bg-emerald-50 text-emerald-700 border-emerald-200" : "bg-zinc-100 text-zinc-500 border-zinc-200"
                        )}>
                          {plan.is_active ? "Active" : "Archived"}
                        </span>
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      <SidePanel
        isOpen={!!selectedPlan}
        onClose={() => setSelectedPlan(null)}
        title="Plan Console"
        disableOutsideClick={isActionLoading || isEditingInSlider}
      >
        {selectedPlan && !isEditingInSlider && (
          <div className="space-y-8 animate-in fade-in duration-200">
            
            <div className="flex items-start justify-between border-b border-[#f4f4f5] pb-4">
              <div>
                <h3 className="text-xl font-bold text-[#09090b] tracking-tight">{selectedPlan.name}</h3>
                <div className="flex items-center gap-2 mt-1">
                  <span className="text-[11px] font-mono text-[#71717a]">{selectedPlan.slug}</span>
                  <QuickCopy text={selectedPlan.slug} iconSize={11} className="hover:bg-[#fafafa]" />
                </div>
              </div>
              <span className={cn(
                "text-[10px] px-2 py-0.5 border font-bold uppercase tracking-widest whitespace-nowrap mt-1",
                selectedPlan.is_full ? "bg-rose-50 text-rose-700 border-rose-200" :
                selectedPlan.is_active ? "bg-emerald-50 text-emerald-700 border-emerald-200" : 
                "bg-zinc-100 text-zinc-500 border-zinc-200"
              )}>
                {selectedPlan.is_full ? "Full Capacity" : selectedPlan.is_active ? "Active" : "Archived"}
              </span>
            </div>

            <div className="space-y-4">
              <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Fulfillment Links</h4>
              <div className="space-y-3">
                <div>
                  <span className="text-[11px] text-[#a1a1aa] block mb-0.5">Public Checkout URL</span>
                  <div className="flex items-center gap-2">
                    <a href={generateCheckoutUrl(selectedPlan.slug)} target="_blank" rel="noopener noreferrer" className="text-[12px] font-mono text-blue-600 hover:opacity-80 underline underline-offset-2 truncate max-w-[280px]">
                      {generateCheckoutUrl(selectedPlan.slug)}
                    </a>
                    <QuickCopy text={generateCheckoutUrl(selectedPlan.slug)} iconSize={12} className="hover:bg-[#fafafa]" />
                  </div>
                </div>
                <div>
                  <span className="text-[11px] text-[#a1a1aa] block mb-0.5">Telegram Invite Link</span>
                  {selectedPlan.telegram_invite_link ? (
                    <div className="flex items-center gap-2">
                      <a href={selectedPlan.telegram_invite_link} target="_blank" rel="noopener noreferrer" className="text-[12px] font-mono text-[#52525b] hover:text-blue-600 underline underline-offset-2 truncate max-w-[280px]">
                        {selectedPlan.telegram_invite_link}
                      </a>
                      <QuickCopy text={selectedPlan.telegram_invite_link} iconSize={12} className="hover:bg-[#fafafa]" />
                    </div>
                  ) : <span className="text-[12px] text-[#a1a1aa] italic">Not configured</span>}
                </div>
                <div>
                  <span className="text-[11px] text-[#a1a1aa] block mb-0.5">Zoom Meeting Link</span>
                  {selectedPlan.weekly_meeting_link ? (
                    <div className="flex items-center gap-2">
                      <a href={selectedPlan.weekly_meeting_link} target="_blank" rel="noopener noreferrer" className="text-[12px] font-mono text-[#52525b] hover:text-blue-600 underline underline-offset-2 truncate max-w-[280px]">
                        {selectedPlan.weekly_meeting_link}
                      </a>
                      <QuickCopy text={selectedPlan.weekly_meeting_link} iconSize={12} className="hover:bg-[#fafafa]" />
                    </div>
                  ) : <span className="text-[12px] text-[#a1a1aa] italic">Not configured</span>}
                </div>
              </div>
            </div>

            <div className="space-y-4">
              <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Configuration Rules</h4>
              <div className="grid grid-cols-2 gap-4 text-[12px]">
                <div><span className="text-[#a1a1aa] block mb-1">Pricing</span><span className="font-mono font-bold text-[#09090b]">RM {selectedPlan.price.toFixed(2)} <span className="font-sans font-normal text-[#71717a]">/ {selectedPlan.interval}</span></span></div>
                <div><span className="text-[#a1a1aa] block mb-1">Grace Period</span><span className="font-medium text-[#09090b]">{selectedPlan.grace_period_days} days</span></div>
                <div><span className="text-[#a1a1aa] block mb-1">Target Audience</span><span className="font-medium text-[#09090b]">{selectedPlan.audience}</span></div>
                <div><span className="text-[#a1a1aa] block mb-1">Capacity</span><span className="font-mono text-[#52525b]"><span className="font-bold text-[#09090b]">{selectedPlan.enrolled_count}</span> / {selectedPlan.max_capacity || "∞"}</span></div>
              </div>
            </div>

            <div className="space-y-4">
              <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Marketing Content</h4>
              <div className="space-y-4 text-[13px]">
                <div>
                  <span className="text-[11px] text-[#a1a1aa] block mb-1">Description</span>
                  <p className="text-[#09090b] leading-relaxed whitespace-pre-wrap">{selectedPlan.long_description}</p>
                </div>
                {selectedPlan.features && selectedPlan.features.length > 0 && (
                  <div>
                    <span className="text-[11px] text-[#a1a1aa] block mb-1.5">Included Features</span>
                    <ul className="space-y-1.5">
                      {selectedPlan.features.map((f, i) => (
                        <li key={i} className="flex items-start gap-2 text-[#09090b]">
                          <CheckCircle2 size={14} className="text-emerald-500 mt-0.5 shrink-0" />
                          <span>{f}</span>
                        </li>
                      ))}
                    </ul>
                  </div>
                )}
                {selectedPlan.faq && selectedPlan.faq.length > 0 && (
                  <div className="flex items-center gap-2 p-3 bg-[#fafafa] border border-[#e5e5e5] rounded-sm">
                    <MessagesSquare size={16} className="text-[#71717a]" />
                    <span className="font-medium text-[#09090b]">{selectedPlan.faq.length} FAQs attached to this plan.</span>
                  </div>
                )}
              </div>
            </div>

            <div className="space-y-4">
              <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Operations</h4>
              <div className="grid grid-cols-2 gap-2">
                <button 
                  onClick={() => setIsEditingInSlider(true)} 
                  disabled={isActionLoading} 
                  className="h-8 border border-[#e5e5e5] bg-white text-[10px] font-bold uppercase tracking-widest text-[#09090b] hover:bg-[#f4f4f5] transition-colors disabled:opacity-50 flex items-center justify-center gap-1.5"
                >
                  <Edit2 size={12} /> Edit Config
                </button>
                <button 
                  onClick={() => copyCheckoutLink(selectedPlan.slug)} 
                  disabled={isActionLoading} 
                  className="h-8 border border-[#e5e5e5] bg-white text-[10px] font-bold uppercase tracking-widest text-[#09090b] hover:bg-[#f4f4f5] transition-colors disabled:opacity-50 flex items-center justify-center gap-1.5"
                >
                  <LinkIcon size={12} /> Share Link
                </button>
                
                {selectedPlan.is_active ? (
                  <button 
                    onClick={() => { if(window.confirm("Archive this plan? New users will not be able to checkout.")) softDeleteMutation.mutate(selectedPlan.id); }} 
                    disabled={isActionLoading} 
                    className="h-8 col-span-2 border border-rose-200 bg-rose-50 text-[10px] font-bold uppercase tracking-widest text-rose-700 hover:bg-rose-100 transition-colors disabled:opacity-50 flex items-center justify-center gap-1.5"
                  >
                    {isActionLoading ? <Loader2 size={12} className="animate-spin" /> : <Archive size={12} />} Archive Plan
                  </button>
                ) : (
                  <button 
                    onClick={() => { if(window.confirm("Restore this plan? It will become public and purchasable again.")) editMutation.mutate({ id: selectedPlan.id, is_active: true }); }} 
                    disabled={isActionLoading} 
                    className="h-8 col-span-2 border border-[#09090b] bg-[#09090b] text-[10px] font-bold uppercase tracking-widest text-white hover:bg-[#27272a] transition-colors disabled:opacity-50 flex items-center justify-center gap-1.5"
                  >
                    {isActionLoading ? <Loader2 size={12} className="animate-spin" /> : <RotateCcw size={12} />} Restore Plan
                  </button>
                )}
              </div>
            </div>
          </div>
        )}

        {selectedPlan && isEditingInSlider && (
          <div className="absolute inset-0 bg-white z-10 flex flex-col animate-in slide-in-from-right duration-200">
            <div className="flex items-center justify-between p-5 border-b border-[#e5e5e5] bg-[#fafafa] shrink-0">
              <div>
                <h3 className="text-[15px] font-bold text-[#09090b]">Edit Configuration</h3>
                <p className="text-[11px] font-mono text-[#71717a] mt-0.5">{selectedPlan.name}</p>
              </div>
            </div>
            <div className="flex-1 overflow-y-auto">
              <CreatePlanForm 
                prefillData={selectedPlan} 
                onSubmit={(data) => editMutation.mutate({ id: selectedPlan.id, ...data })} 
                onCancel={() => setIsEditingInSlider(false)} 
              />
            </div>
          </div>
        )}
      </SidePanel>

      {/* Global Create Modal */}
      {isCreateModalOpen && (
        <div className="fixed inset-0 z-[60] flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/20 backdrop-blur-sm transition-opacity" onClick={() => !createMutation.isPending && setIsCreateModalOpen(false)} />
          <div className="relative bg-white border border-[#e5e5e5] shadow-2xl w-full max-w-2xl flex flex-col animate-in fade-in zoom-in-95 duration-200 max-h-[90vh]">
            <div className="flex items-center justify-between p-5 border-b border-[#e5e5e5] bg-[#fafafa] shrink-0">
              <h3 className="text-[15px] font-bold text-[#09090b]">Create New Plan</h3>
            </div>
            <div className="overflow-y-auto flex-1 bg-[#fafafa]/30">
              <CreatePlanForm onSubmit={(data) => createMutation.mutate(data)} onCancel={() => setIsCreateModalOpen(false)} />
            </div>
          </div>
        </div>
      )}
    </PageLayout>
  );
}
