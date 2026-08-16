import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2, Archive, RotateCcw, Edit2, Link as LinkIcon, Lock, Zap } from "lucide-react";
import { toast } from "sonner";
import { client, type components } from "../../../lib/api-client";
import { cn, collectionModeLabel, filterHiddenFulfillmentTargets } from "../../../lib/utils";
import SidePanel from "../../core/components/SidePanel";
import QuickCopy from "../../core/components/QuickCopy";
import ProductForm from "./ProductForm";

type ProductDto = components["schemas"]["Commerce.ProductDto"];
type UpdateProductRequestDto = components["schemas"]["Commerce.UpdateProductRequestDto"];

interface ProductDetailPanelProps {
  product: ProductDto | null;
  activeWorkspaceSlug?: string;
  onClose: () => void;
  onUpdate: (product: ProductDto | null) => void;
  hasValidEmailConfig: boolean;
}

export default function ProductDetailPanel({ product, activeWorkspaceSlug, onClose, onUpdate, hasValidEmailConfig }: ProductDetailPanelProps) {
  const queryClient = useQueryClient();
  const [isEditing, setIsEditing] = useState(false);
  const [isActionLoading, setIsActionLoading] = useState(false);
  const [activeSnippetTab, setActiveSnippetTab] = useState<"URL" | "HTML" | "REACT" | "MARKDOWN">("URL");
  const collectionMode = product
    ? collectionModeLabel(product.interval, product.gateway_name, product.supports_off_session)
    : null;

  const editMutation = useMutation({
    mutationFn: async (payload: { id: string } & UpdateProductRequestDto) => {
      const { id, ...body } = payload;
      const { error } = await client.PUT("/admin/commerce/products/{id}", {
        params: { path: { id } },
        body
      });
      if (error) throw new Error(error.detail);
      return payload;
    },
    onMutate: () => setIsActionLoading(true),
    onSettled: () => setIsActionLoading(false),
    onSuccess: (variables) => {
      toast.success("Checkout link saved successfully");
      queryClient.invalidateQueries({ queryKey: ["commerce-products"] });
      onUpdate({ ...product!, ...variables, is_active: variables.is_active ?? product!.is_active });
      setIsEditing(false);
    },
    onError: (err: any) => toast.error("Failed to update checkout link", { description: err.message })
  });

  const softDeleteMutation = useMutation({
    mutationFn: async (id: string) => {
      const { error } = await client.DELETE("/admin/commerce/products/{id}", {
        params: { path: { id } }
      });
      if (error) throw new Error(error.detail);
    },
    onMutate: () => setIsActionLoading(true),
    onSettled: () => setIsActionLoading(false),
    onSuccess: () => {
      toast.success("Checkout link archived successfully");
      queryClient.invalidateQueries({ queryKey: ["commerce-products"] });
      onUpdate(product ? { ...product, is_active: false } : null);
    },
    onError: (err: any) => toast.error("Failed to archive checkout link", { description: err.message })
  });

  const restoreMutation = useMutation({
    mutationFn: async (id: string) => {
      const { error } = await client.POST("/admin/commerce/products/{id}/restore", {
        params: { path: { id } }
      });
      if (error) throw new Error(error.detail);
    },
    onMutate: () => setIsActionLoading(true),
    onSettled: () => setIsActionLoading(false),
    onSuccess: () => {
      toast.success("Checkout link restored successfully");
      queryClient.invalidateQueries({ queryKey: ["commerce-products"] });
      onUpdate(product ? { ...product, is_active: true } : null);
    },
    onError: (err: any) => toast.error("Failed to restore checkout link", { description: err.message })
  });

  const generateCheckoutUrl = (productSlug: string) => {
    if (!activeWorkspaceSlug) return "";
    const baseUrl = import.meta.env.VITE_PORTAL_URL || "http://localhost:3004";
    return `${baseUrl}/${activeWorkspaceSlug}/checkout/${productSlug}`;
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

  const checkoutUrl = product ? generateCheckoutUrl(product.slug) : "";
  const htmlSnippet = product ? `<a href="${checkoutUrl}" style="display:inline-block; padding:12px 24px; background-color:#09090b; color:#ffffff; text-decoration:none; border-radius:4px; font-family:sans-serif; font-weight:bold;">Buy ${product.name}</a>` : "";
  const reactSnippet = product ? `import Link from 'next/link';\n\n<Link \n  href="${checkoutUrl}" \n  className="bg-black text-white px-6 py-3 rounded-md font-bold"\n>\n  Buy ${product.name}\n</Link>` : "";
  const markdownSnippet = product ? `[Buy ${product.name}](${checkoutUrl})` : "";

  const activeSnippetContent = 
    activeSnippetTab === "URL" ? checkoutUrl :
    activeSnippetTab === "HTML" ? htmlSnippet :
    activeSnippetTab === "REACT" ? reactSnippet : 
    markdownSnippet;

  const visibleFulfillmentTargets = filterHiddenFulfillmentTargets(product?.fulfillment_targets);

  return (
    <SidePanel
      isOpen={!!product}
      onClose={handleClose}
      title="Checkout Link Console"
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
              {product.is_active ? "Active" : "Archived/Draft"}
            </span>
          </div>

          <div className="space-y-4">
            <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Fulfillment Targets</h4>
            <div className="flex flex-col gap-2">
              {visibleFulfillmentTargets.length > 0 ? (
                visibleFulfillmentTargets.map((target, idx) => (
                  <div key={idx} className="flex items-center gap-2 text-[12px] p-2 bg-[#fafafa] border border-[#e5e5e5] rounded-sm">
                    <span className="font-mono text-[#52525b]">{target}</span>
                  </div>
                ))
              ) : (
                <span className="text-[11px] text-[#a1a1aa] italic">No fulfillment targets configured.</span>
              )}
            </div>
          </div>

          <div className="space-y-4">
            <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Headless Integrations</h4>
            <div className="border border-[#e5e5e5] rounded-sm bg-white overflow-hidden">
              <div className="flex items-center border-b border-[#e5e5e5] bg-[#fafafa]">
                {(["URL", "HTML", "REACT", "MARKDOWN"] as const).map((tab) => (
                  <button
                    key={tab}
                    onClick={() => setActiveSnippetTab(tab)}
                    className={cn(
                      "flex-1 py-2 text-[10px] font-bold uppercase tracking-widest transition-colors",
                      activeSnippetTab === tab ? "bg-white text-[#09090b] border-b-2 border-[#09090b]" : "text-[#71717a] hover:text-[#09090b]"
                    )}
                  >
                    {tab}
                  </button>
                ))}
              </div>
              <div className="p-3 bg-[#fafafa] flex items-start gap-3">
                <pre className="text-[11px] font-mono text-[#52525b] overflow-x-auto whitespace-pre-wrap flex-1 break-all">
                  {activeSnippetContent}
                </pre>
                <QuickCopy 
                  text={activeSnippetContent} 
                  className="bg-white border border-[#e5e5e5] hover:bg-[#f4f4f5]" 
                />
              </div>
            </div>
          </div>

          <div className="space-y-4">
            <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Pricing Configuration</h4>
            <div className="grid grid-cols-2 gap-4 text-[12px]">
              <div>
                <span className="text-[#a1a1aa] block mb-1">Pricing Model</span>
                <span className="font-mono font-bold text-[#09090b]">
                  {product.pricing_model === "PWYW" ? "Pay What You Want" : "Fixed Price"}
                </span>
              </div>
              <div>
                <span className="text-[#a1a1aa] block mb-1">Price</span>
                <span className="font-mono font-bold text-[#09090b]">RM {product.price.toFixed(2)}</span>
              </div>
              {product.pricing_model === "PWYW" && (
                <div>
                  <span className="text-[#a1a1aa] block mb-1">Minimum Price limit</span>
                  <span className="font-mono font-bold text-[#09090b]">RM {product.minimum_price.toFixed(2)}</span>
                </div>
              )}
              <div>
                <span className="text-[#a1a1aa] block mb-1">Interval</span>
                <span className="font-mono text-[#09090b] font-bold uppercase">{product.interval}</span>
              </div>
              <div className="col-span-2">
                <span className="text-[#a1a1aa] block mb-1">Payment Gateway</span>
                <div className="flex items-center gap-2 flex-wrap">
                  <span className="font-mono text-[#09090b] font-bold uppercase tracking-wider">{product.gateway_name}</span>
                  {collectionMode && (
                    <span className={cn(
                      "text-[9px] px-1.5 py-0.5 border font-bold uppercase tracking-widest",
                      collectionMode === "Auto-renew"
                        ? "bg-emerald-50 text-emerald-700 border-emerald-200"
                        : "bg-amber-50 text-amber-800 border-amber-200"
                    )}>
                      {collectionMode}
                    </span>
                  )}
                </div>
              </div>
            </div>
          </div>

          <div className="space-y-4">
            <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Checkout UX Toggles</h4>
            <div className="space-y-2">
              <div className="flex items-center gap-2 text-[12px]">
                {product.checkout_configuration.requires_address ? <Lock size={12} className="text-emerald-600" /> : <Lock size={12} className="text-[#e5e5e5]" />}
                <span className={cn(product.checkout_configuration.requires_address ? "text-[#09090b]" : "text-[#a1a1aa]")}>Requires Full Billing Address</span>
              </div>
              <div className="flex items-center gap-2 text-[12px]">
                {product.checkout_configuration.requires_phone ? <Lock size={12} className="text-emerald-600" /> : <Lock size={12} className="text-[#e5e5e5]" />}
                <span className={cn(product.checkout_configuration.requires_phone ? "text-[#09090b]" : "text-[#a1a1aa]")}>Requires WhatsApp Number</span>
              </div>
            </div>
          </div>

          <div className="space-y-4 pt-4">
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
                  onClick={() => { if(window.confirm("Archive this checkout link? It will instantly break and become unavailable for new buyers.")) softDeleteMutation.mutate(product.id); }} 
                  disabled={isActionLoading} 
                  className="h-8 col-span-2 border border-rose-200 bg-rose-50 text-[10px] font-bold uppercase tracking-widest text-rose-700 hover:bg-rose-100 transition-colors disabled:opacity-50 flex items-center justify-center gap-1.5"
                >
                  {isActionLoading ? <Loader2 size={12} className="animate-spin" /> : <Archive size={12} />} Archive Link
                </button>
              ) : (
                <button 
                  onClick={() => {
                    if (!hasValidEmailConfig) {
                      toast.error("You must configure a valid Resend API key before activating checkout links.");
                      return;
                    }
                    if(window.confirm("Restore this checkout link? It will become purchasable again.")) restoreMutation.mutate(product.id); 
                  }} 
                  disabled={isActionLoading || !hasValidEmailConfig} 
                  className="h-8 col-span-2 border border-[#09090b] bg-[#09090b] text-[10px] font-bold uppercase tracking-widest text-white hover:bg-[#27272a] transition-colors disabled:opacity-50 flex items-center justify-center gap-1.5"
                >
                  {isActionLoading ? <Loader2 size={12} className="animate-spin" /> : <RotateCcw size={12} />} Restore Link
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
              <h3 className="text-[15px] font-bold text-[#09090b]">Edit Checkout Link</h3>
              <p className="text-[11px] font-mono text-[#71717a] mt-0.5">{product.name}</p>
            </div>
          </div>
          
          <div className="flex-1 flex flex-col overflow-hidden bg-white min-h-0">
            <ProductForm 
              initialData={product}
              onSubmit={(data: UpdateProductRequestDto) => editMutation.mutate({ id: product.id, ...data })} 
              onCancel={() => setIsEditing(false)} 
              isPending={editMutation.isPending}
              submitLabel="Save Changes"
              hasValidEmailConfig={hasValidEmailConfig}
            />
          </div>
        </div>
      )}
    </SidePanel>
  );
}
