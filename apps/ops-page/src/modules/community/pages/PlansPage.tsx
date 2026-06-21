import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2, Link as LinkIcon, Plus, X } from "lucide-react";
import { toast } from "sonner";
import { client, type EntitlementDto } from "../../../lib/api-client";
import { useOutletContext } from "react-router-dom";
import PageLayout from "../../core/components/PageLayout";
import CreatePlanForm from "../../../components/forms/CreatePlanForm";

export default function PlansPage() {
  const queryClient = useQueryClient();
  const { activeWorkspaceId } = useOutletContext<{ activeWorkspaceId: string | null }>();
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);

  const { data: plans, isLoading } = useQuery({
    queryKey: ["community-plans"],
    queryFn: async () => {
      const { data, error } = await client.GET("/admin/community/plans");
      if (error) throw new Error(error.detail);
      return data;
    }
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

  const copyCheckoutLink = (planSlug: string) => {
    if (!activeWorkspaceSlug) {
      toast.error("Could not resolve workspace slug.");
      return;
    }
    const publicUrl = `http://localhost:3021/${activeWorkspaceSlug}/${planSlug}/checkout`;
    navigator.clipboard.writeText(publicUrl);
    toast.success("Checkout link copied to clipboard.");
  };

  return (
    <PageLayout 
      title="Plans & Products" 
      description="Manage your subscription tiers and pricing."
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
      {isLoading ? (
        <div className="flex justify-center p-12"><Loader2 className="animate-spin text-[#a1a1aa]" /></div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 gap-6">
          {plans?.map((plan) => (
            <div key={plan.id} className={`bg-white border border-[#e5e5e5] flex flex-col ${!plan.is_active ? 'opacity-60' : ''}`}>
              <div className="p-5 border-b border-[#f4f4f5] bg-[#fafafa]/50">
                <div className="flex items-start justify-between mb-2">
                  <span className="text-[9px] font-bold uppercase tracking-widest bg-zinc-100 text-zinc-600 px-1.5 py-0.5 border border-zinc-200">
                    {plan.audience}
                  </span>
                  {!plan.is_active && (
                    <span className="text-[9px] font-bold uppercase tracking-widest text-rose-600">Archived</span>
                  )}
                </div>
                <h3 className="text-lg font-bold text-[#09090b] mb-1">{plan.name}</h3>
                <div className="flex items-baseline gap-1 font-mono">
                  <span className="text-xl font-bold">RM {plan.price.toFixed(2)}</span>
                  <span className="text-xs text-[#71717a]">/ {plan.interval}</span>
                </div>
              </div>
              <div className="p-5 flex-1">
                <p className="text-[13px] text-[#52525b] leading-relaxed mb-4">{plan.short_description}</p>
                <div className="text-[11px] text-[#71717a] font-mono space-y-1">
                  <p>Enrolled: {plan.enrolled_count} {plan.max_capacity ? `/ ${plan.max_capacity}` : ''}</p>
                  <p>Grace Period: {plan.grace_period_days} days</p>
                </div>
              </div>
              <div className="p-4 border-t border-[#f4f4f5] bg-[#fafafa]/50 flex items-center justify-between">
                <button 
                  onClick={() => copyCheckoutLink(plan.slug)}
                  className="text-[11px] font-bold uppercase tracking-widest text-[#09090b] hover:text-blue-600 transition-colors flex items-center gap-1.5"
                >
                  <LinkIcon size={13} /> Copy Checkout Link
                </button>
              </div>
            </div>
          ))}
        </div>
      )}

      {isCreateModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/20 backdrop-blur-sm transition-opacity" onClick={() => setIsCreateModalOpen(false)} />
          <div className="relative bg-white border border-[#e5e5e5] shadow-xl w-full max-w-lg flex flex-col animate-in fade-in zoom-in-95 duration-200 max-h-[90vh]">
            <div className="flex items-center justify-between p-4 border-b border-[#e5e5e5] bg-[#fafafa]/50 shrink-0">
              <h3 className="text-[13px] font-bold uppercase tracking-widest text-[#09090b]">Create New Plan</h3>
              <button onClick={() => setIsCreateModalOpen(false)} className="text-[#a1a1aa] hover:bg-[#e5e5e5] hover:text-[#09090b] transition-colors p-1"><X size={16} /></button>
            </div>
            <div className="overflow-y-auto flex-1">
              <CreatePlanForm 
                onSubmit={(data) => createMutation.mutate(data)} 
                onCancel={() => setIsCreateModalOpen(false)} 
              />
            </div>
          </div>
        </div>
      )}
    </PageLayout>
  );
}
