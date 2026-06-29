import { ArrowRight, ShoppingCart, CheckCircle, ExternalLink, Users, Box, Zap } from "lucide-react";
import { cn } from "../../../lib/utils";

interface FulfillmentFlowchartProps {
  priceLabel: string;
  targets: string[];
  className?: string;
}

export default function FulfillmentFlowchart({ priceLabel, targets, className }: FulfillmentFlowchartProps) {
  const hasCommunity = targets.some(t => t.includes("community") || t.includes("spaces") || t.startsWith("internal:community"));
  const hasVault = targets.some(t => t.includes("vault") || t.includes("assets") || t.startsWith("internal:vault"));
  const hasWebhook = targets.some(t => t.startsWith("http"));

  return (
    <div className={cn("border border-[#e5e5e5] bg-[#fafafa]/50 p-4 rounded-md space-y-4 font-sans select-none", className)}>
      <div className="flex items-center justify-between gap-2 flex-wrap sm:flex-nowrap">
        <div className="flex-1 min-w-[100px] bg-white border border-[#e5e5e5] p-3 rounded-sm flex flex-col items-center text-center shadow-sm">
          <ShoppingCart size={16} className="text-[#71717a] mb-1.5" />
          <span className="text-[10px] font-bold uppercase tracking-widest text-[#71717a]">1. Checkout</span>
          <span className="text-[11px] font-mono font-bold text-[#09090b] mt-0.5">{priceLabel}</span>
        </div>

        <ArrowRight size={14} className="text-[#a1a1aa] shrink-0 hidden sm:block" />

        <div className="flex-1 min-w-[100px] bg-white border border-[#e5e5e5] p-3 rounded-sm flex flex-col items-center text-center shadow-sm">
          <CheckCircle size={16} className="text-emerald-600 mb-1.5" />
          <span className="text-[10px] font-bold uppercase tracking-widest text-emerald-800">2. Success</span>
          <span className="text-[11px] text-[#71717a] mt-0.5 font-medium">Verify & Record</span>
        </div>

        <ArrowRight size={14} className="text-[#a1a1aa] shrink-0 hidden sm:block" />

        <div className="flex-1 min-w-[100px] bg-[#09090b] text-white p-3 rounded-sm flex flex-col items-center text-center shadow-sm">
          <ExternalLink size={16} className="text-white/80 mb-1.5" />
          <span className="text-[10px] font-bold uppercase tracking-widest text-white/75">3. Fulfill</span>
          <span className="text-[11px] font-bold mt-0.5 truncate max-w-[100px]">Access Grant</span>
        </div>
      </div>

      {targets.length > 0 ? (
        <div className="border-t border-[#e5e5e5] pt-3.5 space-y-2">
          <span className="text-[9px] font-bold uppercase tracking-widest text-[#71717a] block">Active Pipelines</span>
          <div className="space-y-1.5">
            {hasCommunity && (
              <div className="flex items-center gap-2 p-2 bg-white border border-blue-100 rounded-sm text-[11px] text-blue-700">
                <Users size={12} className="shrink-0" />
                <span>Unlocks private group discussion and Zoom schedules.</span>
              </div>
            )}
            {hasVault && (
              <div className="flex items-center gap-2 p-2 bg-white border border-orange-100 rounded-sm text-[11px] text-orange-700">
                <Box size={12} className="shrink-0" />
                <span>Delivers secure R2 digital download links.</span>
              </div>
            )}
            {hasWebhook && (
              <div className="flex items-center gap-2 p-2 bg-white border border-emerald-100 rounded-sm text-[11px] text-emerald-700">
                <Zap size={12} className="shrink-0" />
                <span>Dispatches instant B2B developer webhooks.</span>
              </div>
            )}
          </div>
        </div>
      ) : (
        <div className="border-t border-[#e5e5e5] pt-3 p-2 bg-white border border-[#e5e5e5] rounded-sm text-[11px] text-[#a1a1aa] text-center">
          No automated delivery pipelines configured for this link.
        </div>
      )}
    </div>
  );
}
