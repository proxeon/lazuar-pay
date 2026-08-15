import { useState, useEffect } from "react";
import { useParams, useNavigate } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { client } from "../../../lib/api-client";
import { gatewaySupportsOffSession } from "../../../lib/utils";
import PageLayout from "../../core/components/PageLayout";
import CampaignSettingsPanel from "../components/dunning/CampaignSettingsPanel";
import CampaignTimeline from "../components/dunning/CampaignTimeline";
import type { LocalStepState } from "../components/dunning/types";

export default function CampaignBuilderPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const isNew = !id || id === "new";

  const [isActionLoading, setIsActionLoading] = useState(false);
  
  const [name, setName] = useState("");
  const [isActive, setIsActive] = useState(true);
  const [finalAction, setFinalAction] = useState("CANCEL");
  const [gracePeriodDays, setGracePeriodDays] = useState(3);
  const [priorityOrder, setPriorityOrder] = useState(0);
  const [targetProductIds, setTargetProductIds] = useState<string[]>([]);
  const [targetPaymentMethods, setTargetPaymentMethods] = useState<string[]>([]);
  const [steps, setSteps] = useState<LocalStepState[]>([]);

  const { data: products } = useQuery({
    queryKey: ["commerce-products"],
    queryFn: async () => {
      const { data } = await client.GET("/admin/commerce/products");
      return data || [];
    }
  });

  const { data: campaigns, isLoading: isCampaignsLoading } = useQuery({
    queryKey: ["commerce-dunning-campaigns"],
    queryFn: async () => {
      const { data, error } = await client.GET("/admin/commerce/dunning-campaigns");
      if (error) throw new Error(error.detail);
      return data;
    },
    enabled: !isNew
  });

  const campaign = !isNew ? campaigns?.find(c => c.id === id) : null;

  useEffect(() => {
    if (campaign) {
      setName(campaign.name);
      setIsActive(campaign.is_active);
      setFinalAction(campaign.final_action);
      setGracePeriodDays(campaign.grace_period_days);
      setPriorityOrder(campaign.priority_order || 0);
      setTargetProductIds(campaign.target_product_ids || []);
      setTargetPaymentMethods(campaign.target_payment_methods || []);
      setSteps(campaign.steps ? campaign.steps.map(s => ({ 
        day_offset: String(s.day_offset), 
        action_type: s.action_type || "EMAIL",
        subject: s.subject || "",
        email_body: s.email_body || "",
        whatsapp_body: s.whatsapp_body || ""
      })) : []);
    } else if (isNew) {
      setName("");
      setIsActive(true);
      setFinalAction("CANCEL");
      setGracePeriodDays(3);
      setPriorityOrder(0);
      setTargetProductIds([]);
      setTargetPaymentMethods([]);
      setSteps([]);
    }
  }, [campaign, isNew]);

  const allowAutoCharge = (() => {
    const manualOnly = targetPaymentMethods.length > 0
      && targetPaymentMethods.every(m => m === "MANUAL");
    if (manualOnly) return false;

    const catalog = products || [];
    if (targetProductIds.length === 0) {
      if (catalog.length === 0) return true;
      return catalog.some(p => gatewaySupportsOffSession(p.gateway_name, p.supports_off_session));
    }

    return catalog
      .filter(p => targetProductIds.includes(p.id))
      .some(p => gatewaySupportsOffSession(p.gateway_name, p.supports_off_session));
  })();

  const saveMutation = useMutation({
    mutationFn: async () => {
      if (!name.trim()) throw new Error("Campaign name is required.");
      if (gracePeriodDays < 0) throw new Error("Grace period cannot be negative.");

      if (steps.some(s => s.action_type === "EMAIL" && (!s.subject.trim() || !s.email_body.trim()))) {
        throw new Error("All Email steps require a subject and body.");
      }
      if (steps.some(s => s.action_type === "WHATSAPP" && !s.whatsapp_body.trim())) {
        throw new Error("All WhatsApp steps require a message body.");
      }
      if (steps.some(s => s.action_type === "AUTO_CHARGE") && !allowAutoCharge) {
        throw new Error("AUTO_CHARGE is not available for Billplz / reminder-only products");
      }

      const formattedSteps = steps.map(s => ({
        day_offset: parseInt(s.day_offset, 10),
        action_type: s.action_type,
        subject: s.action_type === "EMAIL" ? s.subject.trim() : undefined,
        email_body: s.action_type === "EMAIL" ? s.email_body.trim() : undefined,
        whatsapp_body: s.action_type === "WHATSAPP" ? s.whatsapp_body.trim() : undefined
      })).sort((a, b) => a.day_offset - b.day_offset);

      const payload = {
        name: name.trim(),
        final_action: finalAction,
        grace_period_days: gracePeriodDays,
        priority_order: priorityOrder,
        target_product_ids: targetProductIds.length > 0 ? targetProductIds : undefined,
        target_payment_methods: targetPaymentMethods.length > 0 ? targetPaymentMethods : undefined,
        steps: formattedSteps,
        is_active: isActive
      };

      if (!isNew && campaign) {
        const { error } = await client.PUT("/admin/commerce/dunning-campaigns/{id}", {
          params: { path: { id: campaign.id } },
          body: payload
        });
        if (error) throw new Error(error.detail);
      } else {
        const { error } = await client.POST("/admin/commerce/dunning-campaigns", {
          body: payload
        });
        if (error) throw new Error(error.detail);
      }
    },
    onMutate: () => setIsActionLoading(true),
    onSettled: () => setIsActionLoading(false),
    onSuccess: () => {
      toast.success(`Campaign ${!isNew ? "updated" : "created"} successfully.`);
      queryClient.invalidateQueries({ queryKey: ["commerce-dunning-campaigns"] });
      navigate("/commerce/dunning-campaigns");
    },
    onError: (err: any) => toast.error("Failed to save campaign", { description: err.message })
  });

  const deleteMutation = useMutation({
    mutationFn: async () => {
      if (!campaign) return;
      const { error } = await client.DELETE("/admin/commerce/dunning-campaigns/{id}", {
        params: { path: { id: campaign.id } }
      });
      if (error) throw new Error(error.detail);
    },
    onMutate: () => setIsActionLoading(true),
    onSettled: () => setIsActionLoading(false),
    onSuccess: () => {
      toast.success("Campaign deleted.");
      queryClient.invalidateQueries({ queryKey: ["commerce-dunning-campaigns"] });
      navigate("/commerce/dunning-campaigns");
    },
    onError: (err: any) => toast.error("Failed to delete campaign", { description: err.message })
  });

  if (!isNew && isCampaignsLoading) {
    return (
      <PageLayout title="Loading Campaign..." breadcrumbs={[]}>
        <div className="flex items-center justify-center p-12">
          <Loader2 className="animate-spin text-[#a1a1aa]" />
        </div>
      </PageLayout>
    );
  }

  const actionButtons = (
    <div className="flex items-center gap-2">
      {!isNew && campaign && (
        <button 
          type="button" 
          onClick={() => { if(window.confirm("Delete this campaign?")) deleteMutation.mutate(); }} 
          disabled={isActionLoading} 
          className="h-9 px-4 border border-rose-200 bg-rose-50 text-rose-700 text-[11px] font-bold uppercase tracking-widest hover:bg-rose-100 transition-colors flex items-center gap-1.5 rounded-sm disabled:opacity-50"
        >
          <Trash2 size={13} /> Delete
        </button>
      )}
      <button 
        type="button" 
        onClick={() => navigate("/commerce/dunning-campaigns")} 
        disabled={isActionLoading} 
        className="h-9 px-4 border border-[#e5e5e5] bg-white text-[#71717a] text-[11px] font-bold uppercase tracking-widest hover:bg-[#f4f4f5] hover:text-[#09090b] transition-colors rounded-sm disabled:opacity-50"
      >
        Cancel
      </button>
      <button 
        type="button"
        onClick={() => saveMutation.mutate()}
        disabled={isActionLoading} 
        className="h-9 px-6 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest hover:bg-[#27272a] disabled:opacity-50 flex items-center gap-1.5 rounded-sm"
      >
        {isActionLoading && <Loader2 size={13} className="animate-spin" />} Save Campaign
      </button>
    </div>
  );

  return (
    <PageLayout
      title={isNew ? "Build Dunning Campaign" : "Edit Dunning Campaign"}
      description="Configure automated recovery sequences for failed payments."
      breadcrumbs={[
        { label: "Commerce", href: "/commerce/dashboard" },
        { label: "Dunning Campaigns", href: "/commerce/dunning-campaigns" },
        { label: isNew ? "New Campaign" : "Edit Campaign" }
      ]}
      actionButton={actionButtons}
    >
      <form onSubmit={(e) => { e.preventDefault(); saveMutation.mutate(); }} className="grid grid-cols-1 lg:grid-cols-12 gap-8 items-start relative pb-20">
        <CampaignSettingsPanel
          isNew={isNew}
          isActionLoading={isActionLoading}
          products={products || []}
          name={name} setName={setName}
          isActive={isActive} setIsActive={setIsActive}
          priorityOrder={priorityOrder} setPriorityOrder={setPriorityOrder}
          targetProductIds={targetProductIds} setTargetProductIds={setTargetProductIds}
          targetPaymentMethods={targetPaymentMethods} setTargetPaymentMethods={setTargetPaymentMethods}
          finalAction={finalAction} setFinalAction={setFinalAction}
          gracePeriodDays={gracePeriodDays} setGracePeriodDays={setGracePeriodDays}
        />
        <CampaignTimeline
          steps={steps}
          setSteps={setSteps}
          isActionLoading={isActionLoading}
          allowAutoCharge={allowAutoCharge}
        />
      </form>
    </PageLayout>
  );
}
