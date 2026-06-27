import { useState } from "react";
import { useMutation, useQueryClient, useQuery } from "@tanstack/react-query";
import { X, Loader2 } from "lucide-react";
import { toast } from "sonner";
import { client } from "../../../lib/api-client";

interface CreateSpaceModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export default function CreateSpaceModal({ isOpen, onClose }: CreateSpaceModalProps) {
  const queryClient = useQueryClient();

  const [name, setName] = useState("");
  const [telegramLink, setTelegramLink] = useState("");
  const [zoomLink, setZoomLink] = useState("");
  const [selectedProductIds, setSelectedProductIds] = useState<string[]>([]);

  const { data: products } = useQuery({
    queryKey: ["commerce-products"],
    queryFn: async () => {
      const { data } = await client.GET("/admin/commerce/products");
      return data || [];
    },
    enabled: isOpen
  });

  const createMutation = useMutation({
    mutationFn: async () => {
      if (selectedProductIds.length === 0) throw new Error("Select at least one Commerce Product to unlock this space.");

      const { error } = await client.POST("/admin/community/spaces", {
        body: {
          name: name.trim(),
          telegram_link: telegramLink.trim() || undefined,
          zoom_link: zoomLink.trim() || undefined,
          product_ids: selectedProductIds
        }
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("Community Space created successfully.");
      queryClient.invalidateQueries({ queryKey: ["community-spaces-list"] });
      onClose();
    },
    onError: (err: any) => toast.error(err.message)
  });

  const handleProductToggle = (id: string) => {
    setSelectedProductIds(prev => 
      prev.includes(id) ? prev.filter(p => p !== id) : [...prev, id]
    );
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/20 backdrop-blur-sm transition-opacity" onClick={() => !createMutation.isPending && onClose()} />
      <div className="relative bg-white border border-[#e5e5e5] shadow-2xl w-full max-w-lg flex flex-col animate-in fade-in zoom-in-95 duration-200 max-h-[90vh]">
        <div className="flex items-center justify-between p-4 border-b border-[#e5e5e5] bg-[#fafafa] shrink-0">
          <h3 className="text-[13px] font-bold uppercase tracking-widest text-[#09090b]">Create Community Space</h3>
          <button onClick={() => !createMutation.isPending && onClose()} disabled={createMutation.isPending} className="text-[#a1a1aa] hover:bg-[#e5e5e5] hover:text-[#09090b] transition-colors p-1 disabled:opacity-50">
            <X size={16} />
          </button>
        </div>
        
        <form onSubmit={(e) => { e.preventDefault(); createMutation.mutate(); }} className="flex flex-col flex-1 min-h-0">
          <div className="p-6 space-y-6 flex-1 overflow-y-auto bg-[#fafafa]/30">
            
            <div className="space-y-4">
              <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">Space Identity</label>
              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Space Name *</label>
                <input required value={name} onChange={e => setName(e.target.value)} disabled={createMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
              </div>
            </div>

            <div className="space-y-4">
              <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">Access Links (Provided after purchase)</label>
              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Telegram/Group Link</label>
                <input type="url" value={telegramLink} onChange={e => setTelegramLink(e.target.value)} disabled={createMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" placeholder="https://t.me/..." />
              </div>
              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Weekly Zoom Link</label>
                <input type="url" value={zoomLink} onChange={e => setZoomLink(e.target.value)} disabled={createMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" placeholder="https://zoom.us/..." />
              </div>
            </div>

            <div className="space-y-4">
              <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">Unlocked By Commerce Products *</label>
              <div className="border border-[#e5e5e5] rounded-sm bg-white overflow-hidden">
                <div className="max-h-[160px] overflow-y-auto p-2 space-y-1">
                  {products?.map((product: any) => (
                    <label key={product.id} className="flex items-center gap-2 p-1.5 hover:bg-[#fafafa] cursor-pointer rounded-sm">
                      <input 
                        type="checkbox" 
                        checked={selectedProductIds.includes(product.id)}
                        onChange={() => handleProductToggle(product.id)}
                        disabled={createMutation.isPending}
                        className="rounded-sm border-[#e5e5e5] text-[#09090b] focus:ring-[#09090b]"
                      />
                      <span className="text-[12px] text-[#09090b] font-medium">{product.name}</span>
                    </label>
                  ))}
                  {products?.length === 0 && <span className="text-[11px] text-[#a1a1aa] p-2">No Commerce Products found.</span>}
                </div>
              </div>
            </div>

          </div>

          <div className="px-5 py-4 border-t border-[#e5e5e5] bg-[#fafafa]/50 flex justify-end gap-2 shrink-0">
            <button type="button" onClick={onClose} disabled={createMutation.isPending} className="px-4 h-8 text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:bg-[#e5e5e5] hover:text-[#09090b] border border-[#e5e5e5] bg-white transition-colors disabled:opacity-50 rounded-sm">Cancel</button>
            <button type="submit" disabled={createMutation.isPending || selectedProductIds.length === 0} className="px-6 h-8 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest hover:bg-[#27272a] disabled:opacity-50 flex items-center gap-1.5 rounded-sm">
              {createMutation.isPending && <Loader2 size={13} className="animate-spin" />} Create Space
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
