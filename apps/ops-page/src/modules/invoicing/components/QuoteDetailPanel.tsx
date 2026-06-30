import { useState } from "react";
import { useMutation, useQueryClient, useQuery } from "@tanstack/react-query";
import { Loader2, FileText, Link as LinkIcon, CheckCircle2 } from "lucide-react";
import { toast } from "sonner";
import { useOutletContext } from "react-router-dom";
import { client, type components, type EntitlementDto } from "../../../lib/api-client";
import { cn } from "../../../lib/utils";
import SidePanel from "../../core/components/SidePanel";
import QuickCopy from "../../core/components/QuickCopy";

type CustomCheckoutDto = components["schemas"]["Commerce.CustomCheckoutDto"];

interface QuoteDetailPanelProps {
  request: CustomCheckoutDto | null;
  onClose: () => void;
  onUpdate: (request: CustomCheckoutDto | null) => void;
}

export default function QuoteDetailPanel({ request, onClose, onUpdate }: QuoteDetailPanelProps) {
  const queryClient = useQueryClient();
  const { activeWorkspaceId } = useOutletContext<{ activeWorkspaceId: string | null }>();
  const [isActionLoading, setIsActionLoading] = useState(false);

  const { data: entitlements } = useQuery({
    queryKey: ["entitlements"],
    queryFn: async () => {
      const { data } = await client.GET("/one/me/entitlements");
      return data as EntitlementDto[];
    }
  });

  const activeWorkspaceSlug = entitlements?.find(e => e.workspace_id === activeWorkspaceId)?.workspace_slug;

  const generatePaymentUrl = (sessionId: string) => {
    if (!activeWorkspaceSlug) return "";
    const baseUrl = import.meta.env.VITE_PORTAL_URL || "http://localhost:3004";
    return `${baseUrl}/${activeWorkspaceSlug}/pay/${sessionId}`;
  };

  const copyPaymentLink = (sessionId: string) => {
    const url = generatePaymentUrl(sessionId);
    if (!url) {
      toast.error("Could not resolve workspace slug.");
      return;
    }
    navigator.clipboard.writeText(url);
    toast.success("Quote link copied to clipboard.");
  };

  const markPaidMutation = useMutation({
    mutationFn: async () => {
      const { error } = await client.POST("/admin/commerce/checkouts/{id}/mark-paid", {
        params: { path: { id: request!.id } }
      });
      if (error) throw new Error(error.detail);
    },
    onMutate: () => setIsActionLoading(true),
    onSettled: () => setIsActionLoading(false),
    onSuccess: () => {
      toast.success("Quote marked as paid. Official receipt generation triggered.");
      queryClient.invalidateQueries({ queryKey: ["custom-checkouts"] });
      onUpdate(request ? { ...request, status: "COMPLETED" } : null);
    },
    onError: (err: any) => toast.error("Failed to mark as paid", { description: err.message })
  });

  if (!request) return null;

  const isCompleted = request.status === "COMPLETED";
  const isExpired = request.status === "EXPIRED" || new Date(request.expires_at).getTime() < Date.now();

  return (
    <SidePanel
      isOpen={!!request}
      onClose={onClose}
      title="Quote Detail"
      disableOutsideClick={isActionLoading}
    >
      <div className="space-y-8 animate-in fade-in duration-200">
        
        <div className="flex items-start justify-between border-b border-[#f4f4f5] pb-6">
          <div>
            <h3 className="text-2xl font-bold tracking-tight font-mono text-[#09090b]">
              RM {request.total_amount.toFixed(2)}
            </h3>
            <div className="flex items-center gap-2 mt-1.5">
              <span className={cn(
                "text-[10px] px-2 py-0.5 border font-bold uppercase tracking-widest whitespace-nowrap",
                isCompleted ? "bg-emerald-50 text-emerald-700 border-emerald-200" :
                isExpired ? "bg-rose-50 text-rose-700 border-rose-200" :
                "bg-amber-50 text-amber-700 border-amber-200"
              )}>
                {request.status}
              </span>
              <span className="text-[11px] text-[#71717a] font-mono">
                {new Date(request.created_at).toLocaleDateString('en-GB')}
              </span>
            </div>
          </div>
          <div className="h-12 w-12 bg-[#f4f4f5] border border-[#e5e5e5] flex items-center justify-center rounded-none shrink-0">
             <FileText size={20} className="text-[#09090b]" />
          </div>
        </div>

        <div className="space-y-4">
          <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Client Information</h4>
          <div className="space-y-3 text-[12px]">
            <div>
              <span className="text-[#a1a1aa] block mb-0.5">Name</span>
              <span className="font-semibold text-[#09090b] text-[13px]">{request.client_name || "Unknown"}</span>
            </div>
            <div>
              <span className="text-[#a1a1aa] block mb-0.5">Email Address</span>
              <div className="flex items-center gap-2">
                <a href={`mailto:${request.client_email}`} className="font-medium text-blue-600 hover:opacity-85 transition-opacity underline underline-offset-2">
                  {request.client_email}
                </a>
                <QuickCopy text={request.client_email || ""} iconSize={11} className="hover:bg-[#fafafa]" />
              </div>
            </div>
          </div>
        </div>

        <div className="space-y-4">
          <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Line Items</h4>
          <div className="border border-[#e5e5e5] rounded-sm overflow-hidden">
            <table className="w-full text-left text-[12px]">
              <thead className="bg-[#fafafa] border-b border-[#e5e5e5]">
                <tr>
                  <th className="px-3 py-2 font-semibold text-[#71717a]">Description</th>
                  <th className="px-3 py-2 font-semibold text-[#71717a] text-right">Qty</th>
                  <th className="px-3 py-2 font-semibold text-[#71717a] text-right">Unit Price</th>
                  <th className="px-3 py-2 font-semibold text-[#71717a] text-right">Total</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#f4f4f5]">
                {request.line_items.map((item, idx) => (
                  <tr key={idx} className="bg-white">
                    <td className="px-3 py-2.5 font-medium text-[#09090b]">{item.description}</td>
                    <td className="px-3 py-2.5 text-[#52525b] text-right">{item.quantity}</td>
                    <td className="px-3 py-2.5 font-mono text-[#52525b] text-right">{item.unit_price.toFixed(2)}</td>
                    <td className="px-3 py-2.5 font-mono font-bold text-[#09090b] text-right">{(item.quantity * item.unit_price).toFixed(2)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>

        <div className="space-y-4">
          <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Quote Link & Expiration</h4>
          <div className="space-y-4">
            <div>
              <span className="text-[11px] text-[#a1a1aa] block mb-0.5">Secure Quote URL</span>
              <div className="flex items-center gap-2 bg-[#fafafa] border border-[#e5e5e5] p-2 rounded-sm">
                <a href={generatePaymentUrl(request.id)} target="_blank" rel="noopener noreferrer" className="text-[11px] font-mono text-blue-600 hover:opacity-80 underline underline-offset-2 truncate max-w-[280px]">
                  {generatePaymentUrl(request.id) || "Loading..."}
                </a>
                {activeWorkspaceSlug && <QuickCopy text={generatePaymentUrl(request.id)} iconSize={12} className="bg-white border border-[#e5e5e5] hover:bg-[#f4f4f5]" />}
              </div>
            </div>
            <div className="grid grid-cols-2 gap-4 text-[12px]">
              <div>
                <span className="text-[#a1a1aa] block mb-1">Expires At</span>
                <span className={cn("font-mono font-bold", isExpired ? "text-rose-600" : "text-[#09090b]")}>
                  {new Date(request.expires_at).toLocaleString('en-GB')}
                </span>
              </div>
              <div>
                <span className="text-[#a1a1aa] block mb-1">LHDN e-Invoice Status</span>
                <span className="font-semibold text-[#09090b]">{request.is_b2b_required ? "Required" : "B2C / Not Required"}</span>
              </div>
            </div>
          </div>
        </div>

        {!isCompleted && !isExpired && (
          <div className="space-y-4 pt-4">
            <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Operations</h4>
            <div className="grid grid-cols-1 gap-3">
              <button 
                onClick={() => copyPaymentLink(request.id)} 
                disabled={isActionLoading || !activeWorkspaceSlug} 
                className="h-9 w-full border border-[#e5e5e5] bg-white text-[11px] font-bold uppercase tracking-widest text-[#09090b] hover:bg-[#f4f4f5] transition-colors disabled:opacity-50 flex items-center justify-center gap-1.5 rounded-sm"
              >
                <LinkIcon size={14} /> Copy Quote Link
              </button>
              
              <button 
                onClick={() => { if(window.confirm("Mark this invoice as paid manually via bank transfer? This will record the revenue and generate the final receipt.")) markPaidMutation.mutate(); }} 
                disabled={isActionLoading} 
                className="h-9 w-full border border-[#09090b] bg-[#09090b] text-[11px] font-bold uppercase tracking-widest text-white hover:bg-[#27272a] transition-colors disabled:opacity-50 flex items-center justify-center gap-1.5 rounded-sm"
              >
                {isActionLoading ? <Loader2 size={14} className="animate-spin" /> : <CheckCircle2 size={14} />} Mark as Paid (Bank Transfer)
              </button>
            </div>
          </div>
        )}

        {isCompleted && (
          <div className="space-y-4 pt-4">
            <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Operations</h4>
            <div className="grid grid-cols-1 gap-3">
              <button 
                onClick={() => {
                  onClose();
                  // For Phase 4 we will wire this to the proper route
                  // navigate(`/invoicing/tax-invoices?search=${request.id}`);
                }} 
                className="h-9 w-full border border-[#e5e5e5] bg-white text-[11px] font-bold uppercase tracking-widest text-blue-600 hover:bg-blue-50 transition-colors flex items-center justify-center gap-1.5 rounded-sm"
              >
                <FileText size={14} /> View Official Tax Invoice →
              </button>
            </div>
          </div>
        )}

      </div>
    </SidePanel>
  );
}
