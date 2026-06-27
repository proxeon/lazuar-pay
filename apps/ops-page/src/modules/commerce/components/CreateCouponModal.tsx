import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2, X } from "lucide-react";
import { toast } from "sonner";
import { client, type components } from "../../../lib/api-client";

type ProductDto = components["schemas"]["Commerce.ProductDto"];

interface CreateCouponModalProps {
  isOpen: boolean;
  onClose: () => void;
  products?: ProductDto[];
}

export default function CreateCouponModal({ isOpen, onClose, products }: CreateCouponModalProps) {
  const queryClient = useQueryClient();
  
  const [code, setCode] = useState("");
  const [discountType, setDiscountType] = useState("PERCENTAGE");
  const [amount, setAmount] = useState(0);
  const [maxUses, setMaxUses] = useState(0);
  const [minPrice, setMinPrice] = useState(0);
  const [expiresAt, setExpiresAt] = useState("");
  const [applicableProducts, setApplicableProducts] = useState<string[]>([]);

  const resetFormStates = () => {
    setCode("");
    setDiscountType("PERCENTAGE");
    setAmount(0);
    setMaxUses(0);
    setMinPrice(0);
    setExpiresAt("");
    setApplicableProducts([]);
  };

  const createMutation = useMutation({
    mutationFn: async () => {
      const { error } = await client.POST("/admin/commerce/coupons", {
        body: {
          code: code.trim().toUpperCase(),
          discount_type: discountType,
          amount: Number(amount),
          max_uses: Number(maxUses),
          minimum_original_price: Number(minPrice),
          expires_at: expiresAt ? new Date(expiresAt).toISOString() : undefined,
          applicable_product_ids: applicableProducts.length > 0 ? applicableProducts : undefined
        }
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("Coupon created successfully");
      queryClient.invalidateQueries({ queryKey: ["commerce-coupons"] });
      resetFormStates();
      onClose();
    },
    onError: (err: any) => toast.error("Failed to create coupon", { description: err.message })
  });

  const handleProductToggle = (productId: string) => {
    setApplicableProducts(prev => 
      prev.includes(productId) 
        ? prev.filter(id => id !== productId)
        : [...prev, productId]
    );
  };

  const handleClose = () => {
    if (!createMutation.isPending) {
      resetFormStates();
      onClose();
    }
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/20 backdrop-blur-sm transition-opacity" onClick={handleClose} />
      <div className="relative bg-white border border-[#e5e5e5] shadow-2xl w-full max-w-lg flex flex-col animate-in fade-in zoom-in-95 duration-200 max-h-[90vh]">
        <div className="flex items-center justify-between p-4 border-b border-[#e5e5e5] bg-[#fafafa]/50 shrink-0">
          <h3 className="text-[13px] font-bold uppercase tracking-widest text-[#09090b]">Create Promo Code</h3>
          <button onClick={handleClose} disabled={createMutation.isPending} className="text-[#a1a1aa] hover:bg-[#e5e5e5] hover:text-[#09090b] transition-colors p-1 disabled:opacity-50"><X size={16} /></button>
        </div>
        <div className="overflow-y-auto flex-1 bg-[#fafafa]/30">
          <form onSubmit={(e) => { e.preventDefault(); createMutation.mutate(); }}>
            <div className="p-6 space-y-6">
              
              <div className="space-y-4">
                <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">Value</label>
                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Coupon Code *</label>
                  <input required value={code} onChange={e => setCode(e.target.value.toUpperCase())} disabled={createMutation.isPending} placeholder="e.g. SUMMER20" className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 font-mono text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
                </div>
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Type</label>
                    <select value={discountType} onChange={e => setDiscountType(e.target.value)} disabled={createMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50">
                      <option value="PERCENTAGE">Percentage (%)</option>
                      <option value="FIXED">Fixed Amount (RM)</option>
                    </select>
                  </div>
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Amount *</label>
                    <input type="number" step="0.01" required value={amount} onChange={e => setAmount(Number(e.target.value))} disabled={createMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
                  </div>
                </div>
              </div>

              <div className="space-y-4">
                <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">Target Scope</label>
                <div className="border border-[#e5e5e5] rounded-sm bg-white overflow-hidden">
                  <div className="max-h-[120px] overflow-y-auto p-2 space-y-1">
                    {products?.map((product: any) => (
                      <label key={product.id} className="flex items-center gap-2 p-1.5 hover:bg-[#fafafa] cursor-pointer rounded-sm">
                        <input 
                          type="checkbox" 
                          checked={applicableProducts.includes(product.id)}
                          onChange={() => handleProductToggle(product.id)}
                          disabled={createMutation.isPending}
                          className="rounded-sm border-[#e5e5e5] text-[#09090b] focus:ring-[#09090b]"
                        />
                        <span className="text-[12px] text-[#09090b] font-medium">{product.name}</span>
                      </label>
                    ))}
                  </div>
                  <div className="bg-[#fafafa] border-t border-[#e5e5e5] px-3 py-2 text-[10px] text-[#71717a]">
                    {applicableProducts.length === 0 ? "Applies to ALL products (Global)" : `Applies to ${applicableProducts.length} specific product(s)`}
                  </div>
                </div>
              </div>

              <div className="space-y-4">
                <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">Constraints</label>
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Max Uses (0 = ∞)</label>
                    <input type="number" value={maxUses} onChange={e => setMaxUses(Number(e.target.value))} disabled={createMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
                  </div>
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Min. Purchase Price</label>
                    <input type="number" step="0.01" value={minPrice} onChange={e => setMinPrice(Number(e.target.value))} disabled={createMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
                  </div>
                </div>
                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Expires At (Optional)</label>
                  <input type="datetime-local" value={expiresAt} onChange={e => setExpiresAt(e.target.value)} disabled={createMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
                </div>
              </div>

            </div>
            
            <div className="px-5 py-4 border-t border-[#e5e5e5] bg-[#fafafa]/50 flex items-center justify-end gap-2.5 shrink-0">
              <button type="button" onClick={handleClose} disabled={createMutation.isPending} className="h-8 px-4 rounded-sm border border-[#e5e5e5] bg-white text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:bg-[#f4f4f5] hover:text-[#09090b] transition-colors disabled:opacity-50">Cancel</button>
              <button type="submit" disabled={createMutation.isPending} className="h-8 px-6 rounded-sm bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest flex items-center justify-center gap-2 hover:bg-[#27272a] transition-colors disabled:opacity-50">
                {createMutation.isPending && <Loader2 size={13} className="animate-spin" />} Create
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>
  );
}
