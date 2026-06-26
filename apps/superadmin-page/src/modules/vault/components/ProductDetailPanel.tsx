// apps/ops-page/src/modules/vault/components/ProductDetailPanel.tsx
import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2, Archive, RotateCcw, Edit2, Link as LinkIcon } from "lucide-react";
import { toast } from "sonner";
import { client, type components } from "../../../lib/api-client";
import { cn } from "../../../lib/utils";
import SidePanel from "../../core/components/SidePanel";
import QuickCopy from "../../core/components/QuickCopy";
import DigitalProductForm from "./DigitalProductForm";

type CommunityPlanDto = components["schemas"]["Community.CommunityPlanDto"];

interface ProductDetailPanelProps {
  product: CommunityPlanDto | null;
  activeWorkspaceSlug?: string;
  onClose: () => void;
  onUpdate: (product: CommunityPlanDto | null) => void;
}

export default function ProductDetailPanel({ product, activeWorkspaceSlug, onClose, onUpdate }: ProductDetailPanelProps) {
  const queryClient = useQueryClient();
  const [isEditing, setIsEditing] = useState(false);
  const [isActionLoading, setIsActionLoading] = useState(false);

  const editMutation = useMutation({
    mutationFn: async (payload: any) => {
      const { id, ...body } = payload;
      const { error } = await client.PUT("/admin/community/plans/{id}", {
        params: { path: { id } },
        body
      });
      if (error) throw new Error(error.detail);
      return payload;
    },
    onMutate: () => setIsActionLoading(true),
    onSettled: () => setIsActionLoading(false),
    onSuccess: (variables) => {
      toast.success("Product saved successfully");
      queryClient.invalidateQueries({ queryKey: ["community-plans"] });
      onUpdate({ ...product!, ...variables, is_active: variables.is_active ?? product!.is_active });
      setIsEditing(false);
    },
    onError: (err: any) => toast.error("Failed to update product", { description: err.message })
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
      toast.success("Product archived successfully");
      queryClient.invalidateQueries({ queryKey: ["community-plans"] });
      onUpdate(product ? { ...product, is_active: false } : null);
    },
    onError: (err: any) => toast.error("Failed to archive product", { description: err.message })
  });

  const generateCheckoutUrl = (productSlug: string) => {
    if (!activeWorkspaceSlug) return "";
    const baseUrl = import.meta.env.VITE_PORTAL_URL || "https://portal.lazuar.com";
    return `${baseUrl}/${activeWorkspaceSlug}/vault/${productSlug}/checkout`;
  };

  const copyCheckoutLink = (productSlug: string) => {
    const url = generateCheckoutUrl(productSlug);
    if (!url) {
      toast.error("Could not resolve workspace slug.");
      return;
    }
    navigator.clipboard.writeText(url);
    toast.success("Checkout link copied to clipboard.");
  };

  const handleClose = () => {
    setIsEditing(false);
    onClose();
  };

  return (
    <SidePanel
      isOpen={!!product}
      onClose={handleClose}
      title="Product Console"
      disableOutsideClick={isActionLoading || isEditing}
    >
      {product && !isEditing && (
        <div className="space-y-8 animate-in fade-in duration-200">
          <div className="flex items-start justify-between border-b border-[#f4f4f5] pb-4">
            <div>
              <h3 className="text-xl font-bold text-[#09090b] tracking-tight">{product.name}</h3>
              <div className="flex items-center gap-2 mt-1">
                <span className="text-[11px] font-mono text-[#71717a]">{product.slug}</span>
                <QuickCopy text={product.slug} iconSize={11} className="hover:bg-[#fafafa]" />
              </div>
            </div>
            <span className={cn(
              "text-[10px] px-2 py-0.5 border font-bold uppercase tracking-widest whitespace-nowrap mt-1",
              product.is_active ? "bg-emerald-50 text-emerald-700 border-emerald-200" : "bg-zinc-100 text-zinc-500 border-zinc-200"
            )}>
              {product.is_active ? "Active" : "Archived"}
            </span>
          </div>

          <div className="space-y-4">
            <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Fulfillment</h4>
            <div>
              <span className="text-[11px] text-[#a1a1aa] block mb-0.5">Secure Download Link</span>
              <div className="flex items-center gap-2">
                <a href={product.fulfillment_file_url} target="_blank" rel="noopener noreferrer" className="text-[12px] font-mono text-indigo-600 hover:opacity-80 underline underline-offset-2 truncate max-w-[280px]">
                  {product.fulfillment_file_url}
                </a>
                <QuickCopy text={product.fulfillment_file_url || ""} iconSize={12} className="hover:bg-[#fafafa]" />
              </div>
              <p className="text-[10px] text-[#a1a1aa] mt-1">This link is automatically emailed to buyers upon successful payment.</p>
            </div>
          </div>

          <div className="space-y-4">
            <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Sales Integration</h4>
            <div>
              <span className="text-[11px] text-[#a1a1aa] block mb-0.5">Public Checkout URL</span>
              <div className="flex items-center gap-2">
                <a href={generateCheckoutUrl(product.slug)} target="_blank" rel="noopener noreferrer" className="text-[12px] font-mono text-blue-600 hover:opacity-80 underline underline-offset-2 truncate max-w-[280px]">
                  {generateCheckoutUrl(product.slug)}
                </a>
                <QuickCopy text={generateCheckoutUrl(product.slug)} iconSize={12} className="hover:bg-[#fafafa]" />
              </div>
            </div>
          </div>

          <div className="space-y-4">
            <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Pricing Configuration</h4>
            <div className="grid grid-cols-2 gap-4 text-[12px]">
              <div><span className="text-[#a1a1aa] block mb-1">Price</span><span className="font-mono font-bold text-[#09090b]">RM {product.price.toFixed(2)} <span className="font-sans font-normal text-[#71717a]">One-Time</span></span></div>
              <div><span className="text-[#a1a1aa] block mb-1">Purchases</span><span className="font-mono text-[#09090b] font-bold">{product.enrolled_count}</span></div>
              <div><span className="text-[#a1a1aa] block mb-1">Target Audience</span><span className="font-medium text-[#09090b]">{product.audience}</span></div>
            </div>
          </div>

          {product.admin_notes && (
            <div className="space-y-4">
              <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Internal Notes</h4>
              <p className="text-[13px] text-[#09090b] leading-relaxed whitespace-pre-wrap">{product.admin_notes}</p>
            </div>
          )}

          <div className="space-y-4">
            <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Operations</h4>
            <div className="grid grid-cols-2 gap-2">
              <button 
                onClick={() => setIsEditing(true)} 
                disabled={isActionLoading} 
                className="h-8 border border-[#e5e5e5] bg-white text-[10px] font-bold uppercase tracking-widest text-[#09090b] hover:bg-[#f4f4f5] transition-colors disabled:opacity-50 flex items-center justify-center gap-1.5"
              >
                <Edit2 size={12} /> Edit Details
              </button>
              <button 
                onClick={() => copyCheckoutLink(product.slug)} 
                disabled={isActionLoading} 
                className="h-8 border border-[#e5e5e5] bg-white text-[10px] font-bold uppercase tracking-widest text-[#09090b] hover:bg-[#f4f4f5] transition-colors disabled:opacity-50 flex items-center justify-center gap-1.5"
              >
                <LinkIcon size={12} /> Copy Link
              </button>
              
              {product.is_active ? (
                <button 
                  onClick={() => { if(window.confirm("Archive this product? New users will not be able to purchase it.")) softDeleteMutation.mutate(product.id); }} 
                  disabled={isActionLoading} 
                  className="h-8 col-span-2 border border-rose-200 bg-rose-50 text-[10px] font-bold uppercase tracking-widest text-rose-700 hover:bg-rose-100 transition-colors disabled:opacity-50 flex items-center justify-center gap-1.5"
                >
                  {isActionLoading ? <Loader2 size={12} className="animate-spin" /> : <Archive size={12} />} Archive Product
                </button>
              ) : (
                <button 
                  onClick={() => { if(window.confirm("Restore this product? It will become purchasable again.")) editMutation.mutate({ id: product.id, is_active: true }); }} 
                  disabled={isActionLoading} 
                  className="h-8 col-span-2 border border-[#09090b] bg-[#09090b] text-[10px] font-bold uppercase tracking-widest text-white hover:bg-[#27272a] transition-colors disabled:opacity-50 flex items-center justify-center gap-1.5"
                >
                  {isActionLoading ? <Loader2 size={12} className="animate-spin" /> : <RotateCcw size={12} />} Restore Product
                </button>
              )}
            </div>
          </div>
        </div>
      )}

      {product && isEditing && (
        <div className="absolute inset-0 bg-white z-10 flex flex-col animate-in slide-in-from-right duration-200">
          <div className="flex items-center justify-between p-5 border-b border-[#e5e5e5] bg-[#fafafa] shrink-0">
            <div>
              <h3 className="text-[15px] font-bold text-[#09090b]">Edit Product</h3>
              <p className="text-[11px] font-mono text-[#71717a] mt-0.5">{product.name}</p>
            </div>
          </div>
          
          <div className="flex-1 flex flex-col overflow-hidden bg-white min-h-0">
            <DigitalProductForm 
              initialData={product}
              onSubmit={(data) => editMutation.mutate({ id: product.id, ...data })} 
              onCancel={() => setIsEditing(false)} 
              isPending={editMutation.isPending}
              submitLabel="Save Changes"
            />
          </div>
        </div>
      )}
    </SidePanel>
  );
}
