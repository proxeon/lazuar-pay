import { useState, useEffect } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2, Trash2, Edit2, Users, ExternalLink, Video } from "lucide-react";
import { toast } from "sonner";
import { client, type components } from "../../../lib/api-client";
import SidePanel from "../../core/components/SidePanel";

type AdminCommunitySpaceDto = components["schemas"]["Community.AdminCommunitySpaceDto"];
type ProductDto = components["schemas"]["Commerce.ProductDto"];

interface SpaceDetailPanelProps {
  space: AdminCommunitySpaceDto | null;
  products?: ProductDto[];
  onClose: () => void;
  onUpdate: (space: AdminCommunitySpaceDto | null) => void;
}

export default function SpaceDetailPanel({ space, products, onClose, onUpdate }: SpaceDetailPanelProps) {
  const queryClient = useQueryClient();
  const [isEditing, setIsEditing] = useState(false);
  const [isActionLoading, setIsActionLoading] = useState(false);

  const [name, setName] = useState("");
  const [telegramLink, setTelegramLink] = useState("");
  const [zoomLink, setZoomLink] = useState("");
  const [selectedProductIds, setSelectedProductIds] = useState<string[]>([]);

  useEffect(() => {
    if (space && isEditing) {
      setName(space.name);
      setTelegramLink(space.telegram_link || "");
      setZoomLink(space.zoom_link || "");
      setSelectedProductIds(space.product_ids || []);
    }
  }, [space, isEditing]);

  const editMutation = useMutation({
    mutationFn: async () => {
      const payload = {
        name: name.trim(),
        telegram_link: telegramLink.trim() || undefined,
        zoom_link: zoomLink.trim() || undefined,
        product_ids: selectedProductIds
      };

      const { error } = await client.PUT("/admin/community/spaces/{id}", {
        params: { path: { id: space!.id } },
        body: payload
      });

      if (error) throw new Error(error.detail);
      return payload;
    },
    onMutate: () => setIsActionLoading(true),
    onSettled: () => setIsActionLoading(false),
    onSuccess: (variables) => {
      toast.success("Space saved successfully");
      queryClient.invalidateQueries({ queryKey: ["community-spaces-list"] });
      onUpdate({ ...space!, ...variables });
      setIsEditing(false);
    },
    onError: (err: any) => toast.error("Failed to update space", { description: err.message })
  });

  const deleteMutation = useMutation({
    mutationFn: async (id: string) => {
      const { error } = await client.DELETE("/admin/community/spaces/{id}", {
        params: { path: { id } }
      });
      if (error) throw new Error(error.detail);
    },
    onMutate: () => setIsActionLoading(true),
    onSettled: () => setIsActionLoading(false),
    onSuccess: () => {
      toast.success("Space deleted successfully");
      queryClient.invalidateQueries({ queryKey: ["community-spaces-list"] });
      onUpdate(null);
    },
    onError: (err: any) => toast.error("Failed to delete space", { description: err.message })
  });

  const handleProductToggle = (id: string) => {
    setSelectedProductIds(prev => 
      prev.includes(id) ? prev.filter(p => p !== id) : [...prev, id]
    );
  };

  const handleClose = () => {
    setIsEditing(false);
    onClose();
  };

  return (
    <SidePanel
      isOpen={!!space}
      onClose={handleClose}
      title="Community Space Console"
      disableOutsideClick={isActionLoading || isEditing}
    >
      {space && !isEditing && (
        <div className="space-y-8 animate-in fade-in duration-200">
          <div className="flex items-start justify-between border-b border-[#f4f4f5] pb-4">
            <div>
              <h3 className="text-xl font-bold text-[#09090b] tracking-tight">{space.name}</h3>
              <div className="flex items-center gap-2 mt-1 text-[11px] font-mono text-[#71717a]">
                ID: {space.id.substring(0,8)}
              </div>
            </div>
            <div className="h-10 w-10 bg-indigo-50 border border-indigo-100 flex items-center justify-center rounded-none shrink-0">
               <Users size={20} className="text-indigo-600" />
            </div>
          </div>

          <div className="space-y-4">
            <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Access Links</h4>
            <div className="space-y-3">
              <div>
                <span className="text-[11px] text-[#a1a1aa] block mb-0.5">Telegram / Group Link</span>
                {space.telegram_link ? (
                  <a href={space.telegram_link} target="_blank" rel="noopener noreferrer" className="flex items-center gap-1.5 text-[12px] font-mono text-blue-600 hover:underline">
                    <ExternalLink size={12} /> {space.telegram_link}
                  </a>
                ) : (
                  <span className="text-[12px] text-[#a1a1aa] italic">Not configured</span>
                )}
              </div>
              <div>
                <span className="text-[11px] text-[#a1a1aa] block mb-0.5">Zoom / Meeting Link</span>
                {space.zoom_link ? (
                  <a href={space.zoom_link} target="_blank" rel="noopener noreferrer" className="flex items-center gap-1.5 text-[12px] font-mono text-indigo-600 hover:underline">
                    <Video size={12} /> {space.zoom_link}
                  </a>
                ) : (
                  <span className="text-[12px] text-[#a1a1aa] italic">Not configured</span>
                )}
              </div>
            </div>
          </div>

          <div className="space-y-4">
            <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Linked Products</h4>
            {space.product_ids && space.product_ids.length > 0 ? (
              <ul className="space-y-2">
                {space.product_ids.map(id => {
                  const matchedProduct = products?.find((p: ProductDto) => p.id === id);
                  return (
                    <li key={id} className="flex items-center justify-between p-2.5 bg-white border border-[#e5e5e5] rounded-sm text-[12px]">
                      <span className="font-semibold text-[#09090b]">{matchedProduct ? matchedProduct.name : "Unknown Product"}</span>
                    </li>
                  );
                })}
              </ul>
            ) : (
              <div className="flex items-center gap-2 p-3 bg-rose-50 border border-rose-200 rounded-sm">
                <span className="text-[12px] font-medium text-rose-800">No products linked. This space cannot be unlocked.</span>
              </div>
            )}
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
                onClick={() => { if(window.confirm("Permanently delete this space? Customers will immediately lose access links.")) deleteMutation.mutate(space.id); }} 
                disabled={isActionLoading} 
                className="h-8 border border-rose-200 bg-rose-50 text-[10px] font-bold uppercase tracking-widest text-rose-700 hover:bg-rose-100 transition-colors disabled:opacity-50 flex items-center justify-center gap-1.5"
              >
                {isActionLoading ? <Loader2 size={12} className="animate-spin" /> : <Trash2 size={12} />} Delete Space
              </button>
            </div>
          </div>
        </div>
      )}

      {space && isEditing && (
        <div className="absolute inset-0 bg-white z-10 flex flex-col animate-in slide-in-from-right duration-200">
          <div className="flex items-center justify-between p-5 border-b border-[#e5e5e5] bg-[#fafafa] shrink-0">
            <div>
              <h3 className="text-[15px] font-bold text-[#09090b]">Edit Space</h3>
              <p className="text-[11px] font-mono text-[#71717a] mt-0.5">{space.name}</p>
            </div>
          </div>
          
          <div className="flex-1 flex flex-col overflow-hidden bg-white min-h-0">
            <form onSubmit={(e) => { e.preventDefault(); editMutation.mutate(); }} className="flex flex-col flex-1 min-h-0">
              <div className="p-6 space-y-6 flex-1 overflow-y-auto">
                <div className="space-y-4">
                  <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">Space Identity</label>
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Space Name *</label>
                    <input required value={name} onChange={e => setName(e.target.value)} disabled={editMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
                  </div>
                </div>

                <div className="space-y-4">
                  <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">Access Links</label>
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Telegram/Group Link</label>
                    <input type="url" value={telegramLink} onChange={e => setTelegramLink(e.target.value)} disabled={editMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" placeholder="https://t.me/..." />
                  </div>
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Weekly Zoom Link</label>
                    <input type="url" value={zoomLink} onChange={e => setZoomLink(e.target.value)} disabled={editMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" placeholder="https://zoom.us/..." />
                  </div>
                </div>

                <div className="space-y-4">
                  <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">Unlocked By Products</label>
                  <div className="border border-[#e5e5e5] rounded-sm bg-white overflow-hidden">
                    <div className="max-h-[160px] overflow-y-auto p-2 space-y-1">
                      {products?.map((product: any) => (
                        <label key={product.id} className="flex items-center gap-2 p-1.5 hover:bg-[#fafafa] cursor-pointer rounded-sm">
                          <input 
                            type="checkbox" 
                            checked={selectedProductIds.includes(product.id)}
                            onChange={() => handleProductToggle(product.id)}
                            disabled={editMutation.isPending}
                            className="rounded-sm border-[#e5e5e5] text-[#09090b] focus:ring-[#09090b]"
                          />
                          <span className="text-[12px] text-[#09090b] font-medium">{product.name}</span>
                        </label>
                      ))}
                    </div>
                  </div>
                </div>
              </div>

              <div className="px-5 py-4 border-t border-[#e5e5e5] bg-[#fafafa]/50 flex items-center justify-end gap-2.5 shrink-0">
                <button type="button" onClick={() => setIsEditing(false)} disabled={editMutation.isPending} className="h-8 px-4 rounded-sm border border-[#e5e5e5] bg-white text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:bg-[#f4f4f5] hover:text-[#09090b] transition-colors disabled:opacity-50">Cancel</button>
                <button type="submit" disabled={editMutation.isPending} className="h-8 px-6 rounded-sm bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest flex items-center justify-center gap-2 hover:bg-[#27272a] transition-colors disabled:opacity-50">
                  {editMutation.isPending && <Loader2 size={13} className="animate-spin" />} Save Changes
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </SidePanel>
  );
}
