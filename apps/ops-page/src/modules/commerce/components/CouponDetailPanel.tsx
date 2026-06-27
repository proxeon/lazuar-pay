import { useState, useEffect } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2, Edit2, Archive, RotateCcw } from "lucide-react";
import { toast } from "sonner";
import { client, type components } from "../../../lib/api-client";
import { cn } from "../../../lib/utils";
import SidePanel from "../../core/components/SidePanel";
import QuickCopy from "../../core/components/QuickCopy";

type CouponDto = components["schemas"]["Commerce.CouponDto"];
type ProductDto = components["schemas"]["Commerce.ProductDto"];

interface CouponDetailPanelProps {
  coupon: CouponDto | null;
  products?: ProductDto[];
  onClose: () => void;
  onUpdate: (coupon: CouponDto | null) => void;
}

export default function CouponDetailPanel({ coupon, products, onClose, onUpdate }: CouponDetailPanelProps) {
  const queryClient = useQueryClient();
  
  const [isEditingInSlider, setIsEditingInSlider] = useState(false);
  const [isActionLoading, setIsActionLoading] = useState(false);

  const [code, setCode] = useState("");
  const [discountType, setDiscountType] = useState("PERCENTAGE");
  const [amount, setAmount] = useState(0);
  const [maxUses, setMaxUses] = useState(0);
  const [minPrice, setMinPrice] = useState(0);
  const [expiresAt, setExpiresAt] = useState("");
  const [applicableProducts, setApplicableProducts] = useState<string[]>([]);

  useEffect(() => {
    if (coupon && isEditingInSlider) {
      setCode(coupon.code);
      setDiscountType(coupon.discount_type);
      setAmount(coupon.amount);
      setMaxUses(coupon.max_uses);
      setMinPrice(coupon.minimum_original_price);
      setExpiresAt(coupon.expires_at ? coupon.expires_at.slice(0, 16) : "");
      setApplicableProducts(coupon.applicable_product_ids || []);
    }
  }, [coupon, isEditingInSlider]);

  const editMutation = useMutation({
    mutationFn: async (payload: { id: string, code: string, discount_type: string, amount: number, max_uses: number, minimum_original_price: number, expires_at: string | null, applicable_product_ids: string[] | undefined, is_active?: boolean }) => {
      const { id, ...body } = payload;
      const { error } = await client.PUT("/admin/commerce/coupons/{id}", {
        params: { path: { id } },
        body: {
          code: body.code,
          discount_type: body.discount_type,
          amount: body.amount,
          max_uses: body.max_uses,
          minimum_original_price: body.minimum_original_price,
          expires_at: body.expires_at || undefined,
          applicable_product_ids: body.applicable_product_ids,
          is_active: body.is_active
        }
      });
      if (error) throw new Error(error.detail);
      return payload;
    },
    onMutate: () => setIsActionLoading(true),
    onSettled: () => setIsActionLoading(false),
    onSuccess: (variables) => {
      toast.success(variables.is_active ? "Promo code restored successfully" : "Promo configuration saved successfully");
      queryClient.invalidateQueries({ queryKey: ["commerce-coupons"] });
      
      onUpdate({
        ...coupon!,
        code: variables.code,
        discount_type: variables.discount_type,
        amount: variables.amount,
        max_uses: variables.max_uses,
        minimum_original_price: variables.minimum_original_price,
        expires_at: variables.expires_at || undefined,
        applicable_product_ids: variables.applicable_product_ids || [],
        is_active: variables.is_active ?? coupon!.is_active
      });
      
      setIsEditingInSlider(false);
    },
    onError: (err: any) => toast.error("Failed to update coupon", { description: err.message })
  });

  const softDeleteMutation = useMutation({
    mutationFn: async (id: string) => {
      const { error } = await client.DELETE("/admin/commerce/coupons/{id}", {
        params: { path: { id } }
      });
      if (error) throw new Error(error.detail);
    },
    onMutate: () => setIsActionLoading(true),
    onSettled: () => setIsActionLoading(false),
    onSuccess: () => {
      toast.success("Promo code soft-deleted and archived");
      queryClient.invalidateQueries({ queryKey: ["commerce-coupons"] });
      if (coupon) {
        onUpdate({ ...coupon, is_active: false });
      }
    },
    onError: (err: any) => toast.error("Failed to archive coupon", { description: err.message })
  });

  const handleProductToggle = (productId: string) => {
    setApplicableProducts(prev => 
      prev.includes(productId) 
        ? prev.filter(id => id !== productId)
        : [...prev, productId]
    );
  };

  const handleClose = () => {
    setIsEditingInSlider(false);
    onClose();
  };

  const isCouponExpired = (dateString?: string | null) => {
    if (!dateString) return false;
    return new Date(dateString).getTime() < Date.now();
  };

  const isLocked = coupon ? coupon.used_count > 0 : false;

  return (
    <SidePanel
      isOpen={!!coupon}
      onClose={handleClose}
      title="Promotion Console"
      disableOutsideClick={isActionLoading || isEditingInSlider}
    >
      {coupon && !isEditingInSlider && (
        <div className="space-y-8 animate-in fade-in duration-200">
          
          <div className="flex items-center justify-between border-b border-[#f4f4f5] pb-6">
            <div className="flex items-center gap-3">
              <span className={cn(
                "px-3 py-1 font-mono text-xl font-bold tracking-tight border",
                !coupon.is_active ? "bg-zinc-100 text-zinc-500 border-zinc-200" :
                isCouponExpired(coupon.expires_at) ? "bg-rose-50 text-rose-700 border-rose-200" : 
                "bg-[#09090b] text-white border-[#09090b]"
              )}>
                {coupon.code}
              </span>
              <QuickCopy text={coupon.code} iconSize={14} className="bg-[#f4f4f5] p-2 hover:bg-[#e5e5e5]" />
            </div>
            <span className="text-[14px] font-mono font-bold text-[#09090b]">
              {coupon.discount_type === "PERCENTAGE" ? `${coupon.amount}% OFF` : `RM ${coupon.amount.toFixed(2)} OFF`}
            </span>
          </div>

          <div className="space-y-4">
            <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Performance</h4>
            <div className="grid grid-cols-3 gap-4 text-[12px] text-center">
              <div className="bg-[#fafafa] border border-[#e5e5e5] p-3 rounded-sm">
                <span className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block mb-1">Redeemed</span>
                <span className="text-xl font-bold text-emerald-600 font-mono">{coupon.used_count}</span>
              </div>
              <div className="bg-[#fafafa] border border-[#e5e5e5] p-3 rounded-sm">
                <span className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block mb-1">In Carts</span>
                <span className="text-xl font-bold text-amber-600 font-mono">{coupon.reserved_count}</span>
              </div>
              <div className="bg-[#fafafa] border border-[#e5e5e5] p-3 rounded-sm">
                <span className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block mb-1">Limit</span>
                <span className="text-xl font-bold text-[#09090b] font-mono">{coupon.max_uses > 0 ? coupon.max_uses : "∞"}</span>
              </div>
            </div>
          </div>

          <div className="space-y-4">
            <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Applicable Scope</h4>
            {coupon.applicable_product_ids && coupon.applicable_product_ids.length > 0 ? (
              <ul className="space-y-2">
                {coupon.applicable_product_ids.map(id => {
                  const matchedProduct = products?.find((p: ProductDto) => p.id === id);
                  return (
                    <li key={id} className="flex items-center justify-between p-2.5 bg-white border border-[#e5e5e5] rounded-sm text-[12px]">
                      <span className="font-semibold text-[#09090b]">{matchedProduct ? matchedProduct.name : "Unknown Product"}</span>
                      {matchedProduct && <span className="font-mono text-[#71717a]">RM {matchedProduct.price.toFixed(2)}</span>}
                    </li>
                  );
                })}
              </ul>
            ) : (
              <div className="flex items-center gap-2 p-3 bg-indigo-50 border border-indigo-200 rounded-sm">
                <span className="text-[12px] font-medium text-indigo-800">Applies globally to all checkout products.</span>
              </div>
            )}
          </div>

          <div className="space-y-4">
            <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Constraints</h4>
            <div className="grid grid-cols-2 gap-4 text-[12px]">
              <div>
                <span className="text-[#a1a1aa] block mb-1">Expiration Date</span>
                <span className={cn("font-mono font-bold", isCouponExpired(coupon.expires_at) ? "text-rose-600" : "text-[#09090b]")}>
                  {coupon.expires_at ? new Date(coupon.expires_at).toLocaleString('en-GB') : "Never"}
                </span>
              </div>
              <div>
                <span className="text-[#a1a1aa] block mb-1">Min. Purchase Price</span>
                <span className="font-mono text-[#09090b]">RM {coupon.minimum_original_price.toFixed(2)}</span>
              </div>
            </div>
          </div>

          <div className="space-y-4 pt-4">
            <div className="grid grid-cols-2 gap-2">
              <button 
                onClick={() => setIsEditingInSlider(true)} 
                disabled={isActionLoading} 
                className="h-8 border border-[#e5e5e5] bg-white text-[10px] font-bold uppercase tracking-widest text-[#09090b] hover:bg-[#f4f4f5] transition-colors disabled:opacity-50 flex items-center justify-center gap-1.5"
              >
                <Edit2 size={12} /> Edit Config
              </button>
              
              {coupon.is_active ? (
                <button 
                  onClick={() => { if(window.confirm("Archive this promotion? Future checkouts will reject it immediately.")) softDeleteMutation.mutate(coupon.id); }} 
                  disabled={isActionLoading} 
                  className="h-8 border border-rose-200 bg-rose-50 text-[10px] font-bold uppercase tracking-widest text-rose-700 hover:bg-rose-100 transition-colors disabled:opacity-50 flex items-center justify-center gap-1.5"
                >
                  {isActionLoading ? <Loader2 size={12} className="animate-spin" /> : <Archive size={12} />} Archive Promo
                </button>
              ) : (
                <button 
                  onClick={() => { if(window.confirm("Restore this promotion? It will become valid immediately.")) editMutation.mutate({ id: coupon.id, code: coupon.code, discount_type: coupon.discount_type, amount: coupon.amount, max_uses: coupon.max_uses, minimum_original_price: coupon.minimum_original_price, expires_at: coupon.expires_at ? new Date(coupon.expires_at).toISOString() : null, applicable_product_ids: coupon.applicable_product_ids || [], is_active: true }); }} 
                  disabled={isActionLoading} 
                  className="h-8 border border-[#09090b] bg-[#09090b] text-[10px] font-bold uppercase tracking-widest text-white hover:bg-[#27272a] transition-colors disabled:opacity-50 flex items-center justify-center gap-1.5"
                >
                  {isActionLoading ? <Loader2 size={12} className="animate-spin" /> : <RotateCcw size={12} />} Restore Promo
                </button>
              )}
            </div>
          </div>
        </div>
      )}

      {coupon && isEditingInSlider && (
        <div className="absolute inset-0 bg-white z-10 flex flex-col animate-in slide-in-from-right duration-200">
          <div className="flex items-center justify-between p-5 border-b border-[#e5e5e5] bg-[#fafafa] shrink-0">
            <div>
              <h3 className="text-[15px] font-bold text-[#09090b]">Edit Configuration</h3>
              <p className="text-[11px] font-mono text-[#71717a] mt-0.5">{coupon.code}</p>
            </div>
          </div>
          
          <div className="flex-1 overflow-y-auto">
            <form onSubmit={(e) => { 
              e.preventDefault(); 
              editMutation.mutate({ 
                id: coupon.id,
                code: code.trim().toUpperCase(),
                discount_type: discountType,
                amount: Number(amount),
                max_uses: Number(maxUses), 
                minimum_original_price: Number(minPrice), 
                expires_at: expiresAt ? new Date(expiresAt).toISOString() : null, 
                applicable_product_ids: applicableProducts.length > 0 ? applicableProducts : undefined,
                is_active: coupon.is_active
              }); 
            }}>
              <div className="p-6 space-y-6">

                <div className="space-y-4">
                  <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">Core Values</label>
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Coupon Code *</label>
                    <input required value={code} onChange={e => setCode(e.target.value.toUpperCase())} disabled={editMutation.isPending || isLocked} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 font-mono text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50 disabled:bg-[#f4f4f5]" />
                  </div>
                  <div className="grid grid-cols-2 gap-4">
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Type</label>
                      <select value={discountType} onChange={e => setDiscountType(e.target.value)} disabled={editMutation.isPending || isLocked} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50 disabled:bg-[#f4f4f5]">
                        <option value="PERCENTAGE">Percentage (%)</option>
                        <option value="FIXED">Fixed Amount (RM)</option>
                      </select>
                    </div>
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Amount *</label>
                      <input type="number" step="0.01" required value={amount} onChange={e => setAmount(Number(e.target.value))} disabled={editMutation.isPending || isLocked} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50 disabled:bg-[#f4f4f5]" />
                    </div>
                  </div>
                  {isLocked && <p className="text-[10px] text-amber-600 mt-1">Core values cannot be changed after a promo code has been redeemed.</p>}
                </div>
                
                <div className="space-y-4">
                  <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">Target Scope</label>
                  <div className="border border-[#e5e5e5] rounded-sm bg-white overflow-hidden">
                    <div className="max-h-[160px] overflow-y-auto p-2 space-y-1">
                      {products?.map((product: any) => (
                        <label key={product.id} className="flex items-center gap-2 p-1.5 hover:bg-[#fafafa] cursor-pointer rounded-sm">
                          <input 
                            type="checkbox" 
                            checked={applicableProducts.includes(product.id)}
                            onChange={() => handleProductToggle(product.id)}
                            disabled={editMutation.isPending}
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
                      <input type="number" value={maxUses} onChange={e => setMaxUses(Number(e.target.value))} disabled={editMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
                    </div>
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Min. Purchase Price</label>
                      <input type="number" step="0.01" value={minPrice} onChange={e => setMinPrice(Number(e.target.value))} disabled={editMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
                    </div>
                  </div>
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Expires At (Optional)</label>
                    <input type="datetime-local" value={expiresAt} onChange={e => setExpiresAt(e.target.value)} disabled={editMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
                    <p className="text-[10px] text-[#a1a1aa] mt-1">Leave empty for a never-expiring promo code.</p>
                  </div>
                </div>

              </div>

              <div className="px-5 py-4 border-t border-[#e5e5e5] bg-[#fafafa]/50 flex items-center justify-end gap-2.5 shrink-0 absolute bottom-0 left-0 right-0">
                <button type="button" onClick={() => setIsEditingInSlider(false)} disabled={editMutation.isPending} className="h-8 px-4 rounded-sm border border-[#e5e5e5] bg-white text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:bg-[#f4f4f5] hover:text-[#09090b] transition-colors disabled:opacity-50">Cancel</button>
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
