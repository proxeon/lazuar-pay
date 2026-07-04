import { useState, useEffect } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2, Plus, Trash2, AlertTriangle, CreditCard, Mail, Smartphone } from "lucide-react";
import { toast } from "sonner";
import { client, type components } from "../../../lib/api-client";
import SidePanel from "../../core/components/SidePanel";

type DunningCampaignDto = components["schemas"]["Commerce.DunningCampaignDto"];

interface LocalStepState {
  day_offset: string;
  action_type: string;
  subject: string;
  email_body: string;
  whatsapp_body: string;
}

interface CampaignBuilderPanelProps {
  campaign: DunningCampaignDto | null;
  products?: any[];
  templates?: any[]; 
  onClose: () => void;
  onSuccess: () => void;
}

export default function CampaignBuilderPanel({ campaign, products, onClose, onSuccess }: CampaignBuilderPanelProps) {
  const queryClient = useQueryClient();
  const [isActionLoading, setIsActionLoading] = useState(false);

  const [name, setName] = useState("");
  const [isActive, setIsActive] = useState(true);
  const [finalAction, setFinalAction] = useState("CANCEL");
  const [gracePeriodDays, setGracePeriodDays] = useState(3);
  const [priorityOrder, setPriorityOrder] = useState(0);
  const [targetProductIds, setTargetProductIds] = useState<string[]>([]);
  const [targetPaymentMethods, setTargetPaymentMethods] = useState<string[]>([]);
  const [steps, setSteps] = useState<LocalStepState[]>([]);

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
    } else {
      setName("");
      setIsActive(true);
      setFinalAction("CANCEL");
      setGracePeriodDays(3);
      setPriorityOrder(0);
      setTargetProductIds([]);
      setTargetPaymentMethods([]);
      setSteps([]);
    }
  }, [campaign]);

  const addStep = () => {
    setSteps(prev => [...prev, { day_offset: "0", action_type: "EMAIL", subject: "", email_body: "", whatsapp_body: "" }]);
  };

  const removeStep = (index: number) => {
    setSteps(prev => prev.filter((_, i) => i !== index));
  };

  const updateStep = (index: number, field: keyof LocalStepState, value: any) => {
    setSteps(prev => prev.map((step, i) => i === index ? { ...step, [field]: value } : step));
  };

  const toggleArrayItem = (setter: React.Dispatch<React.SetStateAction<string[]>>, item: string) => {
    setter(prev => prev.includes(item) ? prev.filter(i => i !== item) : [...prev, item]);
  };

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

      if (campaign) {
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
      toast.success(`Campaign ${campaign ? "updated" : "created"} successfully.`);
      queryClient.invalidateQueries({ queryKey: ["commerce-dunning-campaigns"] });
      onSuccess();
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
      onSuccess();
    },
    onError: (err: any) => toast.error("Failed to delete campaign", { description: err.message })
  });

  return (
    <SidePanel
      isOpen={true} 
      onClose={onClose}
      title={campaign ? "Edit Dunning Campaign" : "Build Dunning Campaign"}
      disableOutsideClick={isActionLoading}
      footer={
        <div className="flex items-center justify-between w-full">
          {campaign ? (
            <button 
              type="button" 
              onClick={() => { if(window.confirm("Delete this campaign?")) deleteMutation.mutate(); }} 
              disabled={isActionLoading} 
              className="h-9 px-4 border border-rose-200 bg-rose-50 text-rose-700 text-[11px] font-bold uppercase tracking-widest hover:bg-rose-100 transition-colors flex items-center gap-1.5 rounded-sm disabled:opacity-50"
            >
              <Trash2 size={13} /> Delete
            </button>
          ) : (
            <div></div>
          )}
          <div className="flex gap-2">
            <button 
              type="button" 
              onClick={onClose} 
              disabled={isActionLoading} 
              className="h-9 px-4 border border-[#e5e5e5] bg-white text-[#71717a] text-[11px] font-bold uppercase tracking-widest hover:bg-[#f4f4f5] hover:text-[#09090b] transition-colors rounded-sm disabled:opacity-50"
            >
              Cancel
            </button>
            <button 
              type="submit" 
              form="campaign-form"
              disabled={isActionLoading} 
              className="h-9 px-6 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest hover:bg-[#27272a] disabled:opacity-50 flex items-center gap-1.5 rounded-sm"
            >
              {isActionLoading && <Loader2 size={13} className="animate-spin" />} Save Campaign
            </button>
          </div>
        </div>
      }
    >
      <form id="campaign-form" onSubmit={(e) => { e.preventDefault(); saveMutation.mutate(); }} className="space-y-8 pb-4">
        
        <div className="space-y-4">
          <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1.5">1. Campaign Identity</h4>
          <div className="grid grid-cols-1 sm:grid-cols-4 gap-4">
            <div className="space-y-1.5 sm:col-span-3">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Campaign Name *</label>
              <input required value={name} onChange={e => setName(e.target.value)} disabled={isActionLoading} placeholder="e.g. VIP High-Touch Recovery" className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
            </div>
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Priority</label>
              <input type="number" required value={priorityOrder} onChange={e => setPriorityOrder(Number(e.target.value))} disabled={isActionLoading} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
            </div>
          </div>
          {campaign && (
            <label className="flex items-center gap-2 cursor-pointer w-fit mt-2">
              <input type="checkbox" checked={isActive} onChange={e => setIsActive(e.target.checked)} disabled={isActionLoading} className="rounded-sm border-[#e5e5e5] text-[#09090b] focus:ring-[#09090b]" />
              <span className="text-[12px] font-medium text-[#09090b]">Campaign is Active</span>
            </label>
          )}
        </div>

        <div className="space-y-4">
          <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1.5">2. Targeting Rules</h4>
          <div className="space-y-3 p-4 border border-[#e5e5e5] bg-[#fafafa]/50 rounded-sm">
            <div className="space-y-2">
              <span className="text-[11px] font-bold text-[#09090b]">Target Products (Optional)</span>
              <div className="max-h-[120px] overflow-y-auto space-y-1 border border-[#e5e5e5] bg-white p-2">
                {products?.map((p: any) => (
                  <label key={p.id} className="flex items-center gap-2 p-1 hover:bg-[#fafafa] cursor-pointer text-[12px]">
                    <input type="checkbox" checked={targetProductIds.includes(p.id)} onChange={() => toggleArrayItem(setTargetProductIds, p.id)} disabled={isActionLoading} className="rounded-sm border-[#e5e5e5]" />
                    {p.name}
                  </label>
                ))}
              </div>
              <p className="text-[10px] text-[#71717a]">Leave empty to apply to all products globally.</p>
            </div>

            <div className="space-y-2 pt-2 border-t border-[#e5e5e5]">
              <span className="text-[11px] font-bold text-[#09090b]">Target Payment Methods (Optional)</span>
              <div className="flex gap-4 text-[12px]">
                <label className="flex items-center gap-2 cursor-pointer">
                  <input type="checkbox" checked={targetPaymentMethods.includes("ONLINE_GATEWAY")} onChange={() => toggleArrayItem(setTargetPaymentMethods, "ONLINE_GATEWAY")} disabled={isActionLoading} className="rounded-sm border-[#e5e5e5]" />
                  Online Gateways (Cards/FPX)
                </label>
                <label className="flex items-center gap-2 cursor-pointer">
                  <input type="checkbox" checked={targetPaymentMethods.includes("MANUAL")} onChange={() => toggleArrayItem(setTargetPaymentMethods, "MANUAL")} disabled={isActionLoading} className="rounded-sm border-[#e5e5e5]" />
                  Manual/Offline Transfers
                </label>
              </div>
              <p className="text-[10px] text-[#71717a]">Leave empty to apply to all payment methods.</p>
            </div>
          </div>
        </div>

        <div className="space-y-4">
          <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1.5 flex justify-between items-center">
            <span>3. Sequence Timeline</span>
            <button type="button" onClick={addStep} disabled={isActionLoading} className="text-[#09090b] hover:underline flex items-center gap-1"><Plus size={12}/> Add Step</button>
          </h4>
          
          {steps.length === 0 ? (
            <div className="p-6 border border-dashed border-[#e5e5e5] text-center text-[#71717a] text-[12px]">
              No actions will be executed in this campaign.
            </div>
          ) : (
            <div className="space-y-3">
              {steps.map((step, idx) => (
                <div key={idx} className="flex flex-col gap-3 p-3 border border-[#e5e5e5] bg-white rounded-sm relative group">
                  <div className="flex gap-3 items-start">
                    <div className="flex-1 grid grid-cols-2 gap-3">
                      <div className="space-y-1">
                        <label className="text-[10px] uppercase tracking-wider text-[#71717a]">Timing Offset</label>
                        <select 
                          value={step.day_offset} 
                          onChange={e => updateStep(idx, "day_offset", e.target.value)} 
                          disabled={isActionLoading} 
                          className="w-full h-8 px-2 border border-[#e5e5e5] text-[12px] focus:outline-none focus:border-[#09090b] disabled:opacity-50"
                        >
                          <option value="-14">14 Days Before Due</option>
                          <option value="-7">7 Days Before Due</option>
                          <option value="-3">3 Days Before Due</option>
                          <option value="-1">1 Day Before Due</option>
                          <option value="0">On Due Date</option>
                          <option value="1">1 Day After Due</option>
                          <option value="3">3 Days After Due</option>
                          <option value="5">5 Days After Due</option>
                          <option value="7">7 Days After Due</option>
                          <option value="14">14 Days After Due</option>
                          <option value="30">30 Days After Due</option>
                        </select>
                      </div>
                      <div className="space-y-1">
                        <label className="text-[10px] uppercase tracking-wider text-[#71717a]">Action Type</label>
                        <select 
                          value={step.action_type} 
                          onChange={e => updateStep(idx, "action_type", e.target.value)} 
                          disabled={isActionLoading} 
                          className="w-full h-8 px-2 border border-[#e5e5e5] text-[12px] focus:outline-none focus:border-[#09090b] disabled:opacity-50"
                        >
                          <option value="EMAIL">Send Email</option>
                          <option value="WHATSAPP">Send WhatsApp</option>
                          <option value="AUTO_CHARGE">Auto-Retry Card</option>
                        </select>
                      </div>
                    </div>
                    <button type="button" onClick={() => removeStep(idx)} disabled={isActionLoading} className="text-rose-400 hover:text-rose-600 p-1 transition-opacity mt-5"><Trash2 size={14} /></button>
                  </div>

                  {step.action_type === "AUTO_CHARGE" && (
                    <div className="p-3 bg-blue-50 border border-blue-200 flex items-start gap-2 rounded-sm mt-1">
                      <CreditCard size={14} className="text-blue-600 mt-0.5" />
                      <p className="text-[11px] text-blue-800 leading-relaxed">
                        The system will attempt to silently charge the customer's vaulted payment method. 
                        Max 4 retries per billing cycle to prevent gateway fraud flags.
                      </p>
                    </div>
                  )}

                  {step.action_type === "EMAIL" && (
                    <div className="space-y-3 mt-1 bg-[#fafafa] p-3 border border-[#e5e5e5] rounded-sm">
                      <div className="flex items-center gap-1.5 text-[11px] font-bold text-[#09090b] mb-1">
                        <Mail size={12} /> Email Configuration
                      </div>
                      <div className="space-y-1">
                        <label className="text-[10px] uppercase tracking-wider text-[#71717a]">Subject Line</label>
                        <input 
                          type="text" 
                          value={step.subject} 
                          onChange={e => updateStep(idx, "subject", e.target.value)} 
                          disabled={isActionLoading}
                          placeholder="e.g. Action Required: Your payment failed"
                          className="w-full h-8 px-2 border border-[#e5e5e5] text-[12px] focus:outline-none focus:border-[#09090b] disabled:opacity-50"
                        />
                      </div>
                      <div className="space-y-1">
                        <label className="text-[10px] uppercase tracking-wider text-[#71717a]">Email Body (HTML/Markdown)</label>
                        <textarea 
                          value={step.email_body} 
                          onChange={e => updateStep(idx, "email_body", e.target.value)} 
                          disabled={isActionLoading}
                          rows={4}
                          placeholder="Available variables: {{customer_name}}, {{plan_name}}, {{renewal_link}}"
                          className="w-full p-2 border border-[#e5e5e5] text-[12px] focus:outline-none focus:border-[#09090b] disabled:opacity-50 font-mono resize-y"
                        />
                      </div>
                    </div>
                  )}

                  {step.action_type === "WHATSAPP" && (
                    <div className="space-y-3 mt-1 bg-[#fafafa] p-3 border border-[#e5e5e5] rounded-sm">
                      <div className="flex items-center gap-1.5 text-[11px] font-bold text-[#09090b] mb-1">
                        <Smartphone size={12} /> WhatsApp Configuration
                      </div>
                      <div className="space-y-1">
                        <label className="text-[10px] uppercase tracking-wider text-[#71717a]">Message Body (Plain Text)</label>
                        <textarea 
                          value={step.whatsapp_body} 
                          onChange={e => updateStep(idx, "whatsapp_body", e.target.value)} 
                          disabled={isActionLoading}
                          rows={3}
                          placeholder="Available variables: {{customer_name}}, {{plan_name}}, {{renewal_link}}"
                          className="w-full p-2 border border-[#e5e5e5] text-[12px] focus:outline-none focus:border-[#09090b] disabled:opacity-50 resize-y"
                        />
                      </div>
                    </div>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>

        <div className="space-y-4">
          <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1.5">4. Terminal Escalation Action</h4>
          <div className="p-4 border border-rose-200 bg-rose-50 rounded-sm space-y-4">
            <div className="flex items-start gap-2">
              <AlertTriangle size={16} className="text-rose-600 mt-0.5 shrink-0" />
              <p className="text-[11px] text-rose-800 leading-relaxed">
                If the invoice remains unpaid after the final grace period, the Dunning Engine will execute the terminal action below to protect your revenue.
              </p>
            </div>
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Final Action</label>
                <select value={finalAction} onChange={e => setFinalAction(e.target.value)} disabled={isActionLoading} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50">
                  <option value="CANCEL">Cancel Subscription</option>
                  <option value="SUSPEND">Suspend Access (Pause)</option>
                  <option value="NONE">Do Nothing (Leave Unpaid)</option>
                </select>
              </div>
              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Grace Period (Days) *</label>
                <input type="number" min="0" required value={gracePeriodDays} onChange={e => setGracePeriodDays(Number(e.target.value))} disabled={isActionLoading} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50" />
              </div>
            </div>
          </div>
        </div>

      </form>
    </SidePanel>
  );
}
