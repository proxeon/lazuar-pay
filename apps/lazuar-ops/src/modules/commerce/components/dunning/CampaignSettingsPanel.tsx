import { AlertTriangle } from "lucide-react";

interface CampaignSettingsPanelProps {
  isNew: boolean;
  isActionLoading: boolean;
  products: any[];
  name: string; setName: (v: string) => void;
  isActive: boolean; setIsActive: (v: boolean) => void;
  priorityOrder: number; setPriorityOrder: (v: number) => void;
  targetProductIds: string[]; setTargetProductIds: React.Dispatch<React.SetStateAction<string[]>>;
  targetPaymentMethods: string[]; setTargetPaymentMethods: React.Dispatch<React.SetStateAction<string[]>>;
  finalAction: string; setFinalAction: (v: string) => void;
  gracePeriodDays: number; setGracePeriodDays: (v: number) => void;
}

export default function CampaignSettingsPanel({
  isNew, isActionLoading, products,
  name, setName,
  isActive, setIsActive,
  priorityOrder, setPriorityOrder,
  targetProductIds, setTargetProductIds,
  targetPaymentMethods, setTargetPaymentMethods,
  finalAction, setFinalAction,
  gracePeriodDays, setGracePeriodDays
}: CampaignSettingsPanelProps) {

  const toggleArrayItem = (setter: React.Dispatch<React.SetStateAction<string[]>>, item: string) => {
    setter(prev => prev.includes(item) ? prev.filter(i => i !== item) : [...prev, item]);
  };

  return (
    <div className="lg:col-span-5 space-y-6 lg:sticky lg:top-4">
      <div className="bg-white border border-[#e5e5e5] rounded-sm p-6 space-y-4 shadow-sm">
        <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1.5">1. Campaign Identity</h4>
        <div className="space-y-4">
          <div className="space-y-1.5">
            <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Campaign Name *</label>
            <input required value={name} onChange={e => setName(e.target.value)} disabled={isActionLoading} placeholder="e.g. VIP High-Touch Recovery" className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
          </div>
          <div className="space-y-1.5">
            <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Evaluation Priority</label>
            <input type="number" required value={priorityOrder} onChange={e => setPriorityOrder(Number(e.target.value))} disabled={isActionLoading} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
            <p className="text-[10px] text-[#71717a]">Higher numbers evaluate first. Determines which campaign runs if a user overlaps targets.</p>
          </div>
          {!isNew && (
            <label className="flex items-center gap-2 cursor-pointer w-fit mt-2">
              <input type="checkbox" checked={isActive} onChange={e => setIsActive(e.target.checked)} disabled={isActionLoading} className="rounded-sm border-[#e5e5e5] text-[#09090b] focus:ring-[#09090b]" />
              <span className="text-[12px] font-medium text-[#09090b]">Campaign is Active</span>
            </label>
          )}
        </div>
      </div>

      <div className="bg-white border border-[#e5e5e5] rounded-sm p-6 space-y-4 shadow-sm">
        <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1.5">2. Targeting Rules</h4>
        <div className="space-y-4">
          <div className="space-y-2">
            <span className="text-[11px] font-bold text-[#09090b]">Target Products (Optional)</span>
            <div className="max-h-[160px] overflow-y-auto space-y-1 border border-[#e5e5e5] bg-[#fafafa]/50 p-2 rounded-sm">
              {products?.map((p: any) => (
                <label key={p.id} className="flex items-center gap-2 p-1.5 hover:bg-white cursor-pointer text-[12px] rounded-sm">
                  <input type="checkbox" checked={targetProductIds.includes(p.id)} onChange={() => toggleArrayItem(setTargetProductIds, p.id)} disabled={isActionLoading} className="rounded-sm border-[#e5e5e5]" />
                  {p.name}
                </label>
              ))}
            </div>
            <p className="text-[10px] text-[#71717a]">Leave empty to apply to all products globally.</p>
          </div>

          <div className="space-y-2 pt-2 border-t border-[#f4f4f5]">
            <span className="text-[11px] font-bold text-[#09090b]">Target Payment Methods (Optional)</span>
            <div className="flex flex-col gap-2 text-[12px]">
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

      <div className="bg-white border border-[#e5e5e5] rounded-sm p-5 shadow-sm relative ml-4 mt-8">
        <div className="space-y-4">
          <div className="flex items-start justify-between border-b border-[#f4f4f5] pb-3">
            <div>
              <h3 className="text-[14px] font-bold text-[#09090b]">Terminal Escalation</h3>
              <p className="text-[11px] text-[#71717a] mt-0.5">Executes automatically after Grace Period ends.</p>
            </div>
          </div>
          <div className="flex items-start gap-2 bg-rose-50 border border-rose-200 p-3 rounded-sm">
            <AlertTriangle size={16} className="text-rose-600 mt-0.5 shrink-0" />
            <p className="text-[11px] text-rose-800 leading-relaxed">
              If the invoice remains unpaid after the final grace period, execute this action to protect your revenue.
            </p>
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Final Action</label>
              <select value={finalAction} onChange={e => setFinalAction(e.target.value)} disabled={isActionLoading} className="flex h-10 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50">
                <option value="CANCEL">Cancel Subscription</option>
                <option value="SUSPEND">Suspend Access (Pause)</option>
                <option value="NONE">Do Nothing (Leave Unpaid)</option>
              </select>
            </div>
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Grace Period (Days) *</label>
              <input type="number" min="0" required value={gracePeriodDays} onChange={e => setGracePeriodDays(Number(e.target.value))} disabled={isActionLoading} className="flex h-10 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50" />
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
