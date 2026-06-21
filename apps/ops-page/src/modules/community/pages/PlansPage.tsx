import { useState, useEffect } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2, Plus, X, AlertTriangle, MoreHorizontal, Link as LinkIcon, Edit2, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { client, type EntitlementDto } from "../../../lib/api-client";
import { useOutletContext } from "react-router-dom";
import PageLayout from "../../core/components/PageLayout";
import CreatePlanForm from "../../../components/forms/CreatePlanForm";
import { cn } from "../../../lib/utils";

export default function PlansPage() {
  const queryClient = useQueryClient();
  const { activeWorkspaceId } = useOutletContext<{ activeWorkspaceId: string | null }>();
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [openMenuId, setOpenMenuId] = useState<string | null>(null);

  // Robust global click-outside listener that prevents React-to-Native event conflicts
  useEffect(() => {
    const closeMenu = (event: MouseEvent) => {
      const target = event.target as HTMLElement;
      // If clicking a trigger button or its nested SVG/path, bypass the generic document closure
      if (target.closest("[data-menu-trigger]")) {
        return;
      }
      setOpenMenuId(null);
    };
    
    document.addEventListener("click", closeMenu);
    return () => document.removeEventListener("click", closeMenu);
  }, []);

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

  const copyCheckoutLink = (planSlug: string) => {
    if (!activeWorkspaceSlug) {
      toast.error("Could not resolve workspace slug.");
      return;
    }
    const publicUrl = `http://localhost:3021/${activeWorkspaceSlug}/${planSlug}/checkout`;
    navigator.clipboard.writeText(publicUrl);
    toast.success("Checkout link copied to clipboard.");
  };

  const handleSoftDelete = (plan: any) => {
    if (window.confirm(`Are you sure you want to archive / soft delete "${plan.name}"?`)) {
      toast.info(`Archived "${plan.name}" (UI Simulator)`);
      queryClient.invalidateQueries({ queryKey: ["community-plans"] });
    }
  };

  const isGatewayActive = paymentConfig?.is_active ?? false;

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
      {/* Gateway Status Banner */}
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

      {/* Main Tabular Plans List */}
      {isLoading ? (
        <div className="flex justify-center p-12"><Loader2 className="animate-spin text-[#a1a1aa]" /></div>
      ) : (
        <div className="bg-white border border-[#e5e5e5] rounded-none overflow-hidden">
          {/* min-h-[320px] ensures vertical space for absolute dropdowns without container clipping */}
          <div className="w-full overflow-x-auto min-h-[320px]">
            <table className="w-full text-left text-[13px] min-w-[750px]">
              <thead className="bg-[#fafafa] border-b border-[#e5e5e5] select-none">
                <tr>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[35%]">Plan Details</th>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[20%]">Price (MYR)</th>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[15%]">Enrolled</th>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[15%]">Grace Period</th>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[10%]">Status</th>
                  <th className="px-5 py-3 w-[5%]"></th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#f4f4f5]">
                {plans?.length === 0 ? (
                  <tr>
                    <td colSpan={6} className="py-12 text-center text-[13px] text-[#71717a]">
                      No community plans configured. Click "Create Plan" to begin.
                    </td>
                  </tr>
                ) : (
                  plans?.map((plan) => (
                    <tr 
                      key={plan.id} 
                      className={cn(
                        "hover:bg-[#fafafa]/50 transition-colors group",
                        !plan.is_active && "opacity-60 bg-[#fafafa]/30"
                      )}
                    >
                      {/* Name, Description, and Audience Badge */}
                      <td className="px-5 py-3.5">
                        <div className="flex items-center gap-2 mb-1">
                          <span className="font-bold text-[#09090b] text-[13px]">{plan.name}</span>
                          <span className="text-[8px] font-bold uppercase tracking-wider bg-zinc-100 text-zinc-600 px-1.5 py-0.5 border border-zinc-200">
                            {plan.audience}
                          </span>
                        </div>
                        <p className="text-[12px] text-[#71717a] line-clamp-1 max-w-sm">
                          {plan.short_description || "No description provided."}
                        </p>
                      </td>

                      {/* Pricing Tier Details */}
                      <td className="px-5 py-3.5">
                        <div className="font-mono text-[#09090b]">
                          <span className="font-bold">RM {plan.price.toFixed(2)}</span>
                          <span className="text-[11px] text-[#71717a]"> / {plan.interval}</span>
                        </div>
                      </td>

                      {/* Enrollment Limits */}
                      <td className="px-5 py-3.5">
                        <div className="font-mono text-[#52525b]">
                          <span className="font-bold text-[#09090b]">{plan.enrolled_count}</span>
                          <span className="text-[11px] text-[#a1a1aa]"> / {plan.max_capacity || "∞"}</span>
                        </div>
                      </td>

                      {/* Grace Period Duration */}
                      <td className="px-5 py-3.5">
                        <span className="text-[12px] text-[#52525b] font-medium">{plan.grace_period_days} days</span>
                      </td>

                      {/* Visibility Status */}
                      <td className="px-5 py-3.5">
                        <span className={cn(
                          "text-[9px] px-1.5 py-0.5 border font-bold uppercase tracking-widest whitespace-nowrap",
                          plan.is_active 
                            ? "bg-emerald-50 text-emerald-700 border-emerald-200" 
                            : "bg-zinc-100 text-zinc-500 border-zinc-200"
                        )}>
                          {plan.is_active ? "Active" : "Archived"}
                        </span>
                      </td>

                      {/* Ellipsis Actions Dropdown Trigger (Flat design - shadow removed) */}
                      <td className="px-5 py-3.5 text-right relative">
                        <button 
                          data-menu-trigger={plan.id}
                          onClick={() => setOpenMenuId(openMenuId === plan.id ? null : plan.id)}
                          className="p-1.5 text-[#a1a1aa] hover:text-[#09090b] hover:bg-[#f4f4f5] transition-colors rounded-sm focus:outline-none"
                        >
                          <MoreHorizontal size={15} />
                        </button>
                        {openMenuId === plan.id && (
                          <div className="absolute right-5 top-full mt-1 w-44 bg-white border border-[#e5e5e5] rounded-none py-1 z-50 text-left animate-in fade-in slide-in-from-top-1 duration-150">
                            <button 
                              onClick={() => toast.info(`Prefilling Edit Form for ${plan.name} (Simulator)`)}
                              className="w-full text-left px-3 py-1.5 text-[11px] font-medium text-[#09090b] hover:bg-[#f4f4f5] transition-colors flex items-center gap-2"
                            >
                              <Edit2 size={12} className="text-[#71717a]" /> Edit Plan
                            </button>
                            <button 
                              onClick={() => copyCheckoutLink(plan.slug)}
                              className="w-full text-left px-3 py-1.5 text-[11px] font-medium text-[#09090b] hover:bg-[#f4f4f5] transition-colors flex items-center gap-2"
                            >
                              <LinkIcon size={12} className="text-[#71717a]" /> Copy Checkout Link
                            </button>
                            <div className="h-px w-full bg-[#f4f4f5] my-1" />
                            <button 
                              onClick={() => handleSoftDelete(plan)}
                              className="w-full text-left px-3 py-1.5 text-[11px] font-semibold text-rose-600 hover:bg-rose-50 transition-colors flex items-center gap-2"
                            >
                              <Trash2 size={12} className="text-rose-500" /> Soft Delete
                            </button>
                          </div>
                        )}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      )}

      {/* Create Plan Modal Overlay Container */}
      {isCreateModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/20 backdrop-blur-sm transition-opacity" onClick={() => !createMutation.isPending && setIsCreateModalOpen(false)} />
          <div className="relative bg-white border border-[#e5e5e5] shadow-xl w-full max-w-lg flex flex-col animate-in fade-in zoom-in-95 duration-200 max-h-[90vh]">
            <div className="flex items-center justify-between p-4 border-b border-[#e5e5e5] bg-[#fafafa]/50 shrink-0">
              <h3 className="text-[13px] font-bold uppercase tracking-widest text-[#09090b]">Create New Plan</h3>
              <button 
                onClick={() => setIsCreateModalOpen(false)} 
                disabled={createMutation.isPending}
                className="text-[#a1a1aa] hover:bg-[#e5e5e5] hover:text-[#09090b] transition-colors p-1 disabled:opacity-50"
              >
                <X size={16} />
              </button>
            </div>
            <div className="overflow-y-auto flex-1">
              <CreatePlanForm 
                onSubmit={(data) => createMutation.mutate(data)} 
                onCancel={() => setIsCreateModalOpen(false)} 
              />
            </div>
            {createMutation.isPending && (
              <div className="absolute inset-0 bg-white/50 backdrop-blur-[1px] flex items-center justify-center z-10">
                <Loader2 className="animate-spin text-[#09090b] h-8 w-8" />
              </div>
            )}
          </div>
        </div>
      )}
    </PageLayout>
  );
}
