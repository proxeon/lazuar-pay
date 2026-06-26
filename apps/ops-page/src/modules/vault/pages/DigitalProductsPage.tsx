// apps/ops-page/src/modules/vault/pages/DigitalProductsPage.tsx
import { useState, useRef } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2, Plus, Archive, RotateCcw, Edit2, Link as LinkIcon, UploadCloud, FileText } from "lucide-react";
import { toast } from "sonner";
import { client, type EntitlementDto, type components, API_URL } from "../../../lib/api-client";
import { useOutletContext } from "react-router-dom";
import PageLayout from "../../core/components/PageLayout";
import SidePanel from "../../core/components/SidePanel";
import QuickCopy from "../../core/components/QuickCopy";
import { cn } from "../../../lib/utils";

type CommunityPlanDto = components["schemas"]["Community.CommunityPlanDto"];

export default function DigitalProductsPage() {
  const queryClient = useQueryClient();
  const { activeWorkspaceId } = useOutletContext<{ activeWorkspaceId: string | null }>();
  
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [selectedProduct, setSelectedProduct] = useState<CommunityPlanDto | null>(null);
  const [isEditingInSlider, setIsEditingInSlider] = useState(false);
  const [isActionLoading, setIsActionLoading] = useState(false);

  const fileInputRef = useRef<HTMLInputElement>(null);
  const [isUploading, setIsUploading] = useState(false);
  const [uploadedUrl, setUploadedUrl] = useState("");

  const [name, setName] = useState("");
  const [slug, setSlug] = useState("");
  const [price, setPrice] = useState(0);
  const [audience, setAudience] = useState("General");
  const [displayOrder, setDisplayOrder] = useState(1);
  const [adminNotes, setAdminNotes] = useState("");

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

  const digitalProducts = plans?.filter(p => p.product_type === "VAULT") || [];
  const activeWorkspaceSlug = entitlements?.find(e => e.workspace_id === activeWorkspaceId)?.workspace_slug;

  const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setIsUploading(true);
    const formData = new FormData();
    formData.append("file", file);

    try {
      const response = await fetch(`${API_URL}/admin/community/vault/upload`, {
        method: "POST",
        body: formData,
        headers: {
          "X-Tenant-Id": activeWorkspaceId || ""
        }
      });

      if (!response.ok) throw new Error("File upload failed.");
      
      const result = await response.json();
      setUploadedUrl(result.url);
      toast.success("File uploaded successfully to Cloudflare R2.");
    } catch (err: any) {
      toast.error(err.message);
    } finally {
      setIsUploading(false);
    }
  };

  const createMutation = useMutation({
    mutationFn: async () => {
      if (!uploadedUrl) throw new Error("You must upload a file for digital products.");
      const { error } = await client.POST("/admin/community/plans", {
        body: {
          name: name.trim(),
          slug: slug.trim(),
          price: Number(price),
          interval: "one_time",
          pricing_model: "FLAT_RATE",
          audience: audience.trim(),
          grace_period_days: 0,
          display_order: Number(displayOrder),
          admin_notes: adminNotes.trim() || undefined,
          product_type: "VAULT",
          fulfillment_file_url: uploadedUrl
        }
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("Digital product created successfully");
      queryClient.invalidateQueries({ queryKey: ["community-plans"] });
      setIsCreateModalOpen(false);
      resetForm();
    },
    onError: (err: any) => toast.error("Failed to create product", { description: err.message })
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
      toast.success("Product saved successfully");
      queryClient.invalidateQueries({ queryKey: ["community-plans"] });
      
      setSelectedProduct(prev => {
        if (!prev) return null;
        if (variables.is_active !== undefined) return { ...prev, is_active: variables.is_active };
        return { ...prev, ...variables }; 
      });
      
      setIsEditingInSlider(false);
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
      setSelectedProduct(prev => prev ? { ...prev, is_active: false } : null);
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

  const resetForm = () => {
    setName("");
    setSlug("");
    setPrice(0);
    setAudience("General");
    setDisplayOrder(1);
    setAdminNotes("");
    setUploadedUrl("");
  };

  const openCreateModal = () => {
    resetForm();
    setIsCreateModalOpen(true);
  };

  const startEditMode = () => {
    if (!selectedProduct) return;
    setName(selectedProduct.name);
    setSlug(selectedProduct.slug);
    setPrice(selectedProduct.price);
    setAudience(selectedProduct.audience);
    setDisplayOrder(selectedProduct.display_order);
    setAdminNotes(selectedProduct.admin_notes || "");
    setUploadedUrl(selectedProduct.fulfillment_file_url || "");
    setIsEditingInSlider(true);
  };

  const handleEditSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedProduct) return;
    editMutation.mutate({
      id: selectedProduct.id,
      name: name.trim(),
      slug: slug.trim(),
      price: Number(price),
      interval: "one_time",
      pricing_model: "FLAT_RATE",
      audience: audience.trim(),
      grace_period_days: 0,
      display_order: Number(displayOrder),
      admin_notes: adminNotes.trim() || undefined,
      product_type: "VAULT",
      fulfillment_file_url: uploadedUrl || undefined
    });
  };

  return (
    <PageLayout 
      title="Digital Products" 
      description="Upload and sell PDFs, templates, and zip files."
      breadcrumbs={[{ label: "Vault", href: "/vault/products" }, { label: "Digital Products" }]}
      actionButton={
        <button 
          onClick={openCreateModal}
          className="h-9 px-4 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest flex items-center gap-2 hover:bg-[#27272a] transition-colors"
        >
          <Plus size={14} /> New Product
        </button>
      }
    >
      <div className="bg-white border border-[#e5e5e5] rounded-none overflow-hidden">
        <div className="w-full overflow-x-auto min-h-[320px]">
          <table className="w-full text-left text-[13px] min-w-[700px]">
            <thead className="bg-[#fafafa] border-b border-[#e5e5e5] select-none">
              <tr>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Product Details</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Price (MYR)</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Purchases</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Status</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#f4f4f5]">
              {isLoading ? (
                <tr><td colSpan={4} className="py-12 text-center text-[#a1a1aa]"><Loader2 size={20} className="animate-spin mx-auto" /></td></tr>
              ) : digitalProducts.length === 0 ? (
                <tr>
                  <td colSpan={4} className="py-12 text-center text-[13px] text-[#71717a]">
                    No digital products found. Click "New Product" to upload a file.
                  </td>
                </tr>
              ) : (
                digitalProducts.map((product) => (
                  <tr 
                    key={product.id} 
                    onClick={() => setSelectedProduct(product)}
                    className={cn(
                      "hover:bg-[#fafafa] transition-colors cursor-pointer group",
                      !product.is_active && "opacity-60 bg-[#fafafa]/30"
                    )}
                  >
                    <td className="px-5 py-3.5">
                      <div className="flex items-center gap-2 mb-1">
                        <FileText size={14} className="text-[#a1a1aa]" />
                        <span className="font-bold text-[#09090b] text-[13px] group-hover:text-blue-600 transition-colors">{product.name}</span>
                      </div>
                      <p className="text-[11px] font-mono text-[#71717a] pl-5">{product.slug}</p>
                    </td>
                    <td className="px-5 py-3.5 font-mono text-[#09090b]">
                      RM {product.price.toFixed(2)}
                    </td>
                    <td className="px-5 py-3.5 font-mono font-bold text-[#09090b]">
                      {product.enrolled_count}
                    </td>
                    <td className="px-5 py-3.5">
                      <span className={cn(
                        "text-[9px] px-1.5 py-0.5 border font-bold uppercase tracking-widest whitespace-nowrap",
                        product.is_active ? "bg-emerald-50 text-emerald-700 border-emerald-200" : "bg-zinc-100 text-zinc-500 border-zinc-200"
                      )}>
                        {product.is_active ? "Active" : "Archived"}
                      </span>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      <SidePanel
        isOpen={!!selectedProduct}
        onClose={() => setSelectedProduct(null)}
        title="Product Console"
        disableOutsideClick={isActionLoading || isEditingInSlider}
      >
        {selectedProduct && !isEditingInSlider && (
          <div className="space-y-8 animate-in fade-in duration-200">
            
            <div className="flex items-start justify-between border-b border-[#f4f4f5] pb-4">
              <div>
                <h3 className="text-xl font-bold text-[#09090b] tracking-tight">{selectedProduct.name}</h3>
                <div className="flex items-center gap-2 mt-1">
                  <span className="text-[11px] font-mono text-[#71717a]">{selectedProduct.slug}</span>
                  <QuickCopy text={selectedProduct.slug} iconSize={11} className="hover:bg-[#fafafa]" />
                </div>
              </div>
              <span className={cn(
                "text-[10px] px-2 py-0.5 border font-bold uppercase tracking-widest whitespace-nowrap mt-1",
                selectedProduct.is_active ? "bg-emerald-50 text-emerald-700 border-emerald-200" : "bg-zinc-100 text-zinc-500 border-zinc-200"
              )}>
                {selectedProduct.is_active ? "Active" : "Archived"}
              </span>
            </div>

            <div className="space-y-4">
              <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Fulfillment</h4>
              <div>
                <span className="text-[11px] text-[#a1a1aa] block mb-0.5">Secure Download Link</span>
                <div className="flex items-center gap-2">
                  <a href={selectedProduct.fulfillment_file_url} target="_blank" rel="noopener noreferrer" className="text-[12px] font-mono text-indigo-600 hover:opacity-80 underline underline-offset-2 truncate max-w-[280px]">
                    {selectedProduct.fulfillment_file_url}
                  </a>
                  <QuickCopy text={selectedProduct.fulfillment_file_url || ""} iconSize={12} className="hover:bg-[#fafafa]" />
                </div>
                <p className="text-[10px] text-[#a1a1aa] mt-1">This link is automatically emailed to buyers upon successful payment.</p>
              </div>
            </div>

            <div className="space-y-4">
              <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Sales Integration</h4>
              <div>
                <span className="text-[11px] text-[#a1a1aa] block mb-0.5">Public Checkout URL</span>
                <div className="flex items-center gap-2">
                  <a href={generateCheckoutUrl(selectedProduct.slug)} target="_blank" rel="noopener noreferrer" className="text-[12px] font-mono text-blue-600 hover:opacity-80 underline underline-offset-2 truncate max-w-[280px]">
                    {generateCheckoutUrl(selectedProduct.slug)}
                  </a>
                  <QuickCopy text={generateCheckoutUrl(selectedProduct.slug)} iconSize={12} className="hover:bg-[#fafafa]" />
                </div>
              </div>
            </div>

            <div className="space-y-4">
              <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Pricing Configuration</h4>
              <div className="grid grid-cols-2 gap-4 text-[12px]">
                <div><span className="text-[#a1a1aa] block mb-1">Price</span><span className="font-mono font-bold text-[#09090b]">RM {selectedProduct.price.toFixed(2)} <span className="font-sans font-normal text-[#71717a]">One-Time</span></span></div>
                <div><span className="text-[#a1a1aa] block mb-1">Purchases</span><span className="font-mono text-[#09090b] font-bold">{selectedProduct.enrolled_count}</span></div>
                <div><span className="text-[#a1a1aa] block mb-1">Target Audience</span><span className="font-medium text-[#09090b]">{selectedProduct.audience}</span></div>
              </div>
            </div>

            {selectedProduct.admin_notes && (
              <div className="space-y-4">
                <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Internal Notes</h4>
                <p className="text-[13px] text-[#09090b] leading-relaxed whitespace-pre-wrap">{selectedProduct.admin_notes}</p>
              </div>
            )}

            <div className="space-y-4">
              <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Operations</h4>
              <div className="grid grid-cols-2 gap-2">
                <button 
                  onClick={startEditMode} 
                  disabled={isActionLoading} 
                  className="h-8 border border-[#e5e5e5] bg-white text-[10px] font-bold uppercase tracking-widest text-[#09090b] hover:bg-[#f4f4f5] transition-colors disabled:opacity-50 flex items-center justify-center gap-1.5"
                >
                  <Edit2 size={12} /> Edit Details
                </button>
                <button 
                  onClick={() => copyCheckoutLink(selectedProduct.slug)} 
                  disabled={isActionLoading} 
                  className="h-8 border border-[#e5e5e5] bg-white text-[10px] font-bold uppercase tracking-widest text-[#09090b] hover:bg-[#f4f4f5] transition-colors disabled:opacity-50 flex items-center justify-center gap-1.5"
                >
                  <LinkIcon size={12} /> Copy Link
                </button>
                
                {selectedProduct.is_active ? (
                  <button 
                    onClick={() => { if(window.confirm("Archive this product? New users will not be able to purchase it.")) softDeleteMutation.mutate(selectedProduct.id); }} 
                    disabled={isActionLoading} 
                    className="h-8 col-span-2 border border-rose-200 bg-rose-50 text-[10px] font-bold uppercase tracking-widest text-rose-700 hover:bg-rose-100 transition-colors disabled:opacity-50 flex items-center justify-center gap-1.5"
                  >
                    {isActionLoading ? <Loader2 size={12} className="animate-spin" /> : <Archive size={12} />} Archive Product
                  </button>
                ) : (
                  <button 
                    onClick={() => { if(window.confirm("Restore this product? It will become purchasable again.")) editMutation.mutate({ id: selectedProduct.id, is_active: true }); }} 
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

        {selectedProduct && isEditingInSlider && (
          <div className="absolute inset-0 bg-white z-10 flex flex-col animate-in slide-in-from-right duration-200">
            <div className="flex items-center justify-between p-5 border-b border-[#e5e5e5] bg-[#fafafa] shrink-0">
              <div>
                <h3 className="text-[15px] font-bold text-[#09090b]">Edit Product</h3>
                <p className="text-[11px] font-mono text-[#71717a] mt-0.5">{selectedProduct.name}</p>
              </div>
            </div>
            
            <div className="flex-1 overflow-y-auto">
              <form onSubmit={handleEditSubmit}>
                <div className="p-6 space-y-6">
                  <div className="space-y-4">
                    <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">File Upload</label>
                    <div className="flex flex-col gap-3 p-4 border border-dashed border-[#a1a1aa] bg-[#fafafa] rounded-sm text-center items-center justify-center relative hover:bg-[#f4f4f5] transition-colors group cursor-pointer">
                      <input 
                        type="file" 
                        ref={fileInputRef}
                        onChange={handleFileUpload}
                        className="absolute inset-0 w-full h-full opacity-0 cursor-pointer disabled:cursor-not-allowed"
                        disabled={isUploading || editMutation.isPending}
                      />
                      {isUploading ? (
                        <>
                          <Loader2 className="animate-spin text-[#a1a1aa] mb-1" size={24} />
                          <span className="text-[11px] font-medium text-[#71717a]">Uploading to secure storage...</span>
                        </>
                      ) : (
                        <>
                          <UploadCloud className="text-[#a1a1aa] group-hover:text-[#09090b] transition-colors mb-1" size={24} />
                          <span className="text-[11px] font-medium text-[#71717a] group-hover:text-[#09090b]">Click or drag file to replace existing download</span>
                        </>
                      )}
                    </div>
                    {uploadedUrl && (
                      <p className="text-[10px] font-mono text-emerald-600 truncate bg-emerald-50 p-2 border border-emerald-200">Attached: {uploadedUrl.split('/').pop()}</p>
                    )}
                  </div>

                  <div className="space-y-4">
                    <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">Basic Details</label>
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Product Name *</label>
                      <input required value={name} onChange={e => setName(e.target.value)} disabled={editMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
                    </div>
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Slug Identifier *</label>
                      <input required value={slug} onChange={e => setSlug(e.target.value)} disabled={editMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] font-mono disabled:opacity-50" />
                    </div>
                    <div className="grid grid-cols-2 gap-4">
                      <div className="space-y-1.5">
                        <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Price (MYR) *</label>
                        <input type="number" step="0.01" required value={price} onChange={e => setPrice(Number(e.target.value))} disabled={editMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
                      </div>
                      <div className="space-y-1.5">
                        <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Target Audience *</label>
                        <input required value={audience} onChange={e => setAudience(e.target.value)} disabled={editMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
                      </div>
                    </div>
                  </div>

                  <div className="space-y-4">
                    <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">Internal Operations</label>
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Display Order</label>
                      <input type="number" required value={displayOrder} onChange={e => setDisplayOrder(Number(e.target.value))} disabled={editMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
                    </div>
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Admin Notes</label>
                      <textarea value={adminNotes} onChange={e => setAdminNotes(e.target.value)} disabled={editMutation.isPending} rows={3} className="w-full rounded-sm border border-[#e5e5e5] bg-white p-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] resize-y disabled:opacity-50" />
                    </div>
                  </div>
                </div>

                <div className="px-5 py-4 border-t border-[#e5e5e5] bg-[#fafafa]/50 flex items-center justify-end gap-2.5 shrink-0 absolute bottom-0 left-0 right-0">
                  <button type="button" onClick={() => setIsEditingInSlider(false)} disabled={editMutation.isPending} className="h-8 px-4 rounded-sm border border-[#e5e5e5] bg-white text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:bg-[#f4f4f5] hover:text-[#09090b] transition-colors disabled:opacity-50">Cancel</button>
                  <button type="submit" disabled={editMutation.isPending} className="h-8 px-6 rounded-sm bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest flex items-center justify-center gap-2 hover:bg-[#27272a] transition-colors disabled:opacity-50">
                    {editMutation.isPending && <Loader2 size={13} className="animate-spin" />} Save
                  </button>
                </div>
              </form>
            </div>
          </div>
        )}
      </SidePanel>

      {/* Create Modal */}
      {isCreateModalOpen && (
        <div className="fixed inset-0 z-[60] flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/20 backdrop-blur-sm transition-opacity" onClick={() => !createMutation.isPending && setIsCreateModalOpen(false)} />
          <div className="relative bg-white border border-[#e5e5e5] shadow-2xl w-full max-w-lg flex flex-col animate-in fade-in zoom-in-95 duration-200 max-h-[90vh]">
            <div className="flex items-center justify-between p-4 border-b border-[#e5e5e5] bg-[#fafafa] shrink-0">
              <h3 className="text-[13px] font-bold uppercase tracking-widest text-[#09090b]">New Digital Product</h3>
              <button onClick={() => setIsCreateModalOpen(false)} disabled={createMutation.isPending} className="text-[#a1a1aa] hover:bg-[#e5e5e5] hover:text-[#09090b] transition-colors p-1 disabled:opacity-50"><X size={16} /></button>
            </div>
            
            <div className="overflow-y-auto flex-1 bg-white">
              <form onSubmit={(e) => { e.preventDefault(); createMutation.mutate(); }}>
                <div className="p-6 space-y-6">

                  <div className="space-y-4">
                    <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">File Upload *</label>
                    <div className="flex flex-col gap-3 p-6 border border-dashed border-[#a1a1aa] bg-[#fafafa] rounded-sm text-center items-center justify-center relative hover:bg-[#f4f4f5] transition-colors group cursor-pointer">
                      <input 
                        type="file" 
                        ref={fileInputRef}
                        onChange={handleFileUpload}
                        className="absolute inset-0 w-full h-full opacity-0 cursor-pointer disabled:cursor-not-allowed"
                        disabled={isUploading || createMutation.isPending}
                        required={!uploadedUrl}
                      />
                      {isUploading ? (
                        <>
                          <Loader2 className="animate-spin text-[#a1a1aa] mb-2" size={28} />
                          <span className="text-[11px] font-medium text-[#71717a]">Uploading to secure storage...</span>
                        </>
                      ) : uploadedUrl ? (
                        <>
                          <FileText className="text-emerald-600 mb-2" size={28} />
                          <span className="text-[11px] font-medium text-emerald-700">File uploaded successfully. Click to replace.</span>
                        </>
                      ) : (
                        <>
                          <UploadCloud className="text-[#a1a1aa] group-hover:text-[#09090b] transition-colors mb-2" size={28} />
                          <span className="text-[11px] font-medium text-[#71717a] group-hover:text-[#09090b]">Click or drag PDF/ZIP file here</span>
                        </>
                      )}
                    </div>
                  </div>

                  <div className="space-y-4">
                    <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">Product Details</label>
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Name *</label>
                      <input required value={name} onChange={e => setName(e.target.value)} disabled={createMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
                    </div>
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Slug Identifier *</label>
                      <input required value={slug} onChange={e => setSlug(e.target.value)} disabled={createMutation.isPending} placeholder="e.g. startup-guide-pdf" className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 font-mono text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
                    </div>
                    <div className="grid grid-cols-2 gap-4">
                      <div className="space-y-1.5">
                        <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Price (MYR) *</label>
                        <input type="number" step="0.01" required value={price} onChange={e => setPrice(Number(e.target.value))} disabled={createMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
                      </div>
                      <div className="space-y-1.5">
                        <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Audience *</label>
                        <input required value={audience} onChange={e => setAudience(e.target.value)} disabled={createMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
                      </div>
                    </div>
                  </div>

                </div>

                <div className="px-5 py-4 border-t border-[#e5e5e5] bg-[#fafafa]/50 flex justify-end gap-2">
                  <button type="button" onClick={() => setIsCreateModalOpen(false)} disabled={createMutation.isPending} className="px-4 h-8 text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:bg-[#e5e5e5] hover:text-[#09090b] border border-[#e5e5e5] bg-white transition-colors disabled:opacity-50 rounded-sm">Cancel</button>
                  <button type="submit" disabled={createMutation.isPending || !uploadedUrl} className="px-6 h-8 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest hover:bg-[#27272a] disabled:opacity-50 flex items-center gap-1.5 rounded-sm">
                    {createMutation.isPending && <Loader2 size={13} className="animate-spin" />} Create Product
                  </button>
                </div>
              </form>
            </div>

          </div>
        </div>
      )}
    </PageLayout>
  );
}
