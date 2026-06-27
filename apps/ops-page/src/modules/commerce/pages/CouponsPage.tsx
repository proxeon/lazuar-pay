import { useState } from "react";
import { useOutletContext } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2, Plus, Tag, Edit2, Archive, RotateCcw, Infinity } from "lucide-react";
import { toast } from "sonner";
import { client, type components } from "../../../lib/api-client";
import PageLayout from "../../core/components/PageLayout";
import SidePanel from "../../core/components/SidePanel";
import QuickCopy from "../../core/components/QuickCopy";
import { cn } from "../../../lib/utils";

type CouponDto = components["schemas"]["Commerce.CouponDto"];
type ProductDto = components["schemas"]["Commerce.ProductDto"];

export default function CouponsPage() {
  const { activeWorkspaceId } = useOutletContext<{ activeWorkspaceId: string | null }>();
  const queryClient = useQueryClient();
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  
  const [selectedCoupon, setSelectedCoupon] = useState<CouponDto | null>(null);
  const [isEditingInSlider, setIsEditingInSlider] = useState(false);
  const [isActionLoading, setIsActionLoading] = useState(false);

  const [code, setCode] = useState("");
  const [discountType, setDiscountType] = useState("PERCENTAGE");
  const [amount, setAmount] = useState(0);
  const [maxUses, setMaxUses] = useState(0);
  const [minPrice, setMinPrice] = useState(0);
  const [expiresAt, setExpiresAt] = useState("");
  const [applicableProducts, setApplicableProducts] = useState<string[]>([]);

  const { data: products } = useQuery({
    queryKey: ["commerce-products"],
    queryFn: async () => {
      const { data } = await client.GET("/admin/commerce/products");
      return data || [];
    },
    enabled: !!activeWorkspaceId
  });

  const { data: coupons, isLoading } = useQuery({
    queryKey: ["commerce-coupons"],
    queryFn: async () => {
      const { data, error } = await client.GET("/admin/commerce/coupons");
      if (error) throw new Error(error.detail);
      return data;
    },
    enabled: !!activeWorkspaceId
  });

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
      setIsCreateModalOpen(false);
      resetFormStates();
    },
    onError: (err: any) => toast.error("Failed to create coupon", { description: err.message })
  });

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
    },
    onMutate: () => setIsActionLoading(true),
    onSettled: () => setIsActionLoading(false),
    onSuccess: (_, variables) => {
      toast.success(variables.is_active ? "Promo code restored successfully" : "Promo configuration saved successfully");
      queryClient.invalidateQueries({ queryKey: ["commerce-coupons"] });
      
      setSelectedCoupon(prev => {
        if (!prev) return null;
        return {
          ...prev,
          code: variables.code,
          discount_type: variables.discount_type,
          amount: variables.amount,
          max_uses: variables.max_uses,
          minimum_original_price: variables.minimum_original_price,
          expires_at: variables.expires_at,
          applicable_product_ids: variables.applicable_product_ids || [],
          is_active: variables.is_active ?? prev.is_active
        };
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
      
      setSelectedCoupon(prev => {
        if (!prev) return null;
        return { ...prev, is_active: false };
      });
    },
    onError: (err: any) => toast.error("Failed to archive coupon", { description: err.message })
  });

  const resetFormStates = () => {
    setCode("");
    setDiscountType("PERCENTAGE");
    setAmount(0);
    setMaxUses(0);
    setMinPrice(0);
    setExpiresAt("");
    setApplicableProducts([]);
  };

  const openCreateModal = () => {
    resetFormStates();
    setIsCreateModalOpen(true);
  };

  const startEditingInSlider = () => {
    if (!selectedCoupon) return;
    setCode(selectedCoupon.code);
    setDiscountType(selectedCoupon.discount_type);
    setAmount(selectedCoupon.amount);
    setMaxUses(selectedCoupon.max_uses);
    setMinPrice(selectedCoupon.minimum_original_price);
    setExpiresAt(selectedCoupon.expires_at ? selectedCoupon.expires_at.slice(0, 16) : "");
    setApplicableProducts(selectedCoupon.applicable_product_ids || []);
    setIsEditingInSlider(true);
  };

  const handleProductToggle = (productId: string) => {
    setApplicableProducts(prev => 
      prev.includes(productId) 
        ? prev.filter(id => id !== productId)
        : [...prev, productId]
    );
  };

  const isCouponExpired = (dateString?: string | null) => {
    if (!dateString) return false;
    return new Date(dateString).getTime() < Date.now();
  };

  const isLocked = selectedCoupon ? selectedCoupon.used_count > 0 : false;

  return (
    <PageLayout 
      title="Promotions" 
      description="Create and track discount codes and limits."
      breadcrumbs={[{ label: "Commerce", href: "/commerce/dashboard" }, { label: "Promotions" }]}
      actionButton={
        <button 
          onClick={openCreateModal}
          className="h-9 px-4 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest flex items-center gap-2 hover:bg-[#27272a] transition-colors"
        >
          <Plus size={14} /> Create Coupon
        </button>
      }
    >
      <div className="bg-white border border-[#e5e5e5] rounded-none flex flex-col h-full overflow-hidden">
        <div className="w-full overflow-x-auto min-h-[320px]">
          <table className="w-full text-left text-[13px] min-w-[700px]">
            <thead className="bg-[#fafafa] border-b border-[#e5e5e5] select-none">
              <tr>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] whitespace-nowrap">Promo Code</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] whitespace-nowrap">Discount</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] whitespace-nowrap">Applies To</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] whitespace-nowrap">Usage Limit</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] whitespace-nowrap">Redeemed</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] whitespace-nowrap">Expiration</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#f4f4f5]">
              {isLoading ? (
                <tr><td colSpan={6} className="py-12 text-center text-[#a1a1aa]"><Loader2 size={20} className="animate-spin mx-auto" /></td></tr>
              ) : coupons?.length === 0 ? (
                <tr><td colSpan={6} className="py-12 text-center text-[12px] text-[#71717a]">No promotional codes found.</td></tr>
              ) : (
                coupons?.map((coupon) => {
                  const expired = isCouponExpired(coupon.expires_at);
                  const archived = !coupon.is_active;

                  return (
                    <tr 
                      key={coupon.id} 
                      onClick={() => setSelectedCoupon(coupon)}
                      className={cn(
                        "hover:bg-[#fafafa] transition-colors cursor-pointer group", 
                        (expired || archived) && "opacity-60 bg-[#fafafa]/30"
                      )}
                    >
                      <td className="px-5 py-3.5 whitespace-nowrap">
                        <div className="flex items-center gap-1.5">
                          <Tag size={13} className="text-[#a1a1aa] mr-0.5" />
                          <span className={cn(
                            "font-mono font-bold px-2 py-0.5 border text-[11px]", 
                            archived ? "bg-zinc-100 text-zinc-500 border-zinc-200" :
                            expired ? "bg-rose-50 text-rose-700 border-rose-200" : 
                            "bg-emerald-50 text-emerald-700 border-emerald-200"
                          )}>
                            {coupon.code}
                          </span>
                          <QuickCopy text={coupon.code} iconSize={10} className="opacity-0 group-hover:opacity-100 p-0.5" />
                        </div>
                      </td>
                      <td className="px-5 py-3.5 font-mono text-[#52525b] whitespace-nowrap font-medium text-[12px]">
                        {coupon.discount_type === "PERCENTAGE" ? `${coupon.amount}%` : `RM ${coupon.amount.toFixed(2)}`}
                      </td>
                      <td className="px-5 py-3.5 text-[#52525b] whitespace-nowrap">
                        {coupon.applicable_product_ids && coupon.applicable_product_ids.length > 0 ? (
                          <span className="text-[9px] font-bold uppercase tracking-widest bg-blue-50 text-blue-700 border border-blue-200 px-1.5 py-0.5 rounded-sm">
                            Specific ({coupon.applicable_product_ids.length})
                          </span>
                        ) : (
                          <span className="text-[9px] font-bold uppercase tracking-widest bg-indigo-50 text-indigo-700 border border-indigo-200 px-1.5 py-0.5 rounded-sm">
                            Global
                          </span>
                        )}
                      </td>
                      <td className="px-5 py-3.5 text-[#52525b] whitespace-nowrap">
                        {coupon.max_uses > 0 ? coupon.max_uses : <Infinity size={14} className="text-[#a1a1aa]" />}
                      </td>
                      <td className="px-5 py-3.5 text-[#52525b] whitespace-nowrap">
                        <span className="font-bold text-[#09090b]">{coupon.used_count}</span> <span className="text-[10px] text-[#71717a]">({coupon.reserved_count} pending)</span>
                      </td>
                      <td className="px-5 py-3.5 text-[#52525b] text-[11px] font-mono whitespace-nowrap">
                        {archived ? <span className="text-[#a1a1aa] font-semibold font-sans">Archived</span> :
                         expired ? <span className="text-rose-600 font-semibold font-sans">Expired</span> :
                         coupon.expires_at ? new Date(coupon.expires_at).toLocaleDateString('en-GB') : "Never"}
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </div>

      <SidePanel
        isOpen={!!selectedCoupon}
        onClose={() => { setSelectedCoupon(null); setIsEditingInSlider(false); }}
        title="Promotion Console"
        disableOutsideClick={isActionLoading || isEditingInSlider}
      >
        {selectedCoupon && !isEditingInSlider && (
          <div className="space-y-8 animate-in fade-in duration-200">
            
            <div className="flex items-center justify-between border-b border-[#f4f4f5] pb-6">
              <div className="flex items-center gap-3">
                <span className={cn(
                  "px-3 py-1 font-mono text-xl font-bold tracking-tight border",
                  !selectedCoupon.is_active ? "bg-zinc-100 text-zinc-500 border-zinc-200" :
                  isCouponExpired(selectedCoupon.expires_at) ? "bg-rose-50 text-rose-700 border-rose-200" : 
                  "bg-[#09090b] text-white border-[#09090b]"
                )}>
                  {selectedCoupon.code}
                </span>
                <QuickCopy text={selectedCoupon.code} iconSize={14} className="bg-[#f4f4f5] p-2 hover:bg-[#e5e5e5]" />
              </div>
              <span className="text-[14px] font-mono font-bold text-[#09090b]">
                {selectedCoupon.discount_type === "PERCENTAGE" ? `${selectedCoupon.amount}% OFF` : `RM ${selectedCoupon.amount.toFixed(2)} OFF`}
              </span>
            </div>

            <div className="space-y-4">
              <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Performance</h4>
              <div className="grid grid-cols-3 gap-4 text-[12px] text-center">
                <div className="bg-[#fafafa] border border-[#e5e5e5] p-3 rounded-sm">
                  <span className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block mb-1">Redeemed</span>
                  <span className="text-xl font-bold text-emerald-600 font-mono">{selectedCoupon.used_count}</span>
                </div>
                <div className="bg-[#fafafa] border border-[#e5e5e5] p-3 rounded-sm">
                  <span className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block mb-1">In Carts</span>
                  <span className="text-xl font-bold text-amber-600 font-mono">{selectedCoupon.reserved_count}</span>
                </div>
                <div className="bg-[#fafafa] border border-[#e5e5e5] p-3 rounded-sm">
                  <span className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block mb-1">Limit</span>
                  <span className="text-xl font-bold text-[#09090b] font-mono">{selectedCoupon.max_uses > 0 ? selectedCoupon.max_uses : "∞"}</span>
                </div>
              </div>
            </div>

            <div className="space-y-4">
              <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Applicable Scope</h4>
              {selectedCoupon.applicable_product_ids && selectedCoupon.applicable_product_ids.length > 0 ? (
                <ul className="space-y-2">
                  {selectedCoupon.applicable_product_ids.map(id => {
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
                  <span className={cn("font-mono font-bold", isCouponExpired(selectedCoupon.expires_at) ? "text-rose-600" : "text-[#09090b]")}>
                    {selectedCoupon.expires_at ? new Date(selectedCoupon.expires_at).toLocaleString('en-GB') : "Never"}
                  </span>
                </div>
                <div>
                  <span className="text-[#a1a1aa] block mb-1">Min. Purchase Price</span>
                  <span className="font-mono text-[#09090b]">RM {selectedCoupon.minimum_original_price.toFixed(2)}</span>
                </div>
              </div>
            </div>

            <div className="space-y-4 pt-4">
              <div className="grid grid-cols-2 gap-2">
                <button 
                  onClick={startEditingInSlider} 
                  disabled={isActionLoading} 
                  className="h-8 border border-[#e5e5e5] bg-white text-[10px] font-bold uppercase tracking-widest text-[#09090b] hover:bg-[#f4f4f5] transition-colors disabled:opacity-50 flex items-center justify-center gap-1.5"
                >
                  <Edit2 size={12} /> Edit Config
                </button>
                
                {selectedCoupon.is_active ? (
                  <button 
                    onClick={() => { if(window.confirm("Archive this promotion? Future checkouts will reject it immediately.")) softDeleteMutation.mutate(selectedCoupon.id); }} 
                    disabled={isActionLoading} 
                    className="h-8 border border-rose-200 bg-rose-50 text-[10px] font-bold uppercase tracking-widest text-rose-700 hover:bg-rose-100 transition-colors disabled:opacity-50 flex items-center justify-center gap-1.5"
                  >
                    {isActionLoading ? <Loader2 size={12} className="animate-spin" /> : <Archive size={12} />} Archive Promo
                  </button>
                ) : (
                  <button 
                    onClick={() => { if(window.confirm("Restore this promotion? It will become valid immediately.")) editMutation.mutate({ id: selectedCoupon.id, code: selectedCoupon.code, discount_type: selectedCoupon.discount_type, amount: selectedCoupon.amount, max_uses: selectedCoupon.max_uses, minimum_original_price: selectedCoupon.minimum_original_price, expires_at: selectedCoupon.expires_at ? new Date(selectedCoupon.expires_at).toISOString() : null, applicable_product_ids: selectedCoupon.applicable_product_ids || [], is_active: true }); }} 
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

        {selectedCoupon && isEditingInSlider && (
          <div className="absolute inset-0 bg-white z-10 flex flex-col animate-in slide-in-from-right duration-200">
            <div className="flex items-center justify-between p-5 border-b border-[#e5e5e5] bg-[#fafafa] shrink-0">
              <div>
                <h3 className="text-[15px] font-bold text-[#09090b]">Edit Configuration</h3>
                <p className="text-[11px] font-mono text-[#71717a] mt-0.5">{selectedCoupon.code}</p>
              </div>
            </div>
            
            <div className="flex-1 overflow-y-auto">
              <form onSubmit={(e) => { 
                e.preventDefault(); 
                editMutation.mutate({ 
                  id: selectedCoupon.id,
                  code: code.trim().toUpperCase(),
                  discount_type: discountType,
                  amount: Number(amount),
                  max_uses: Number(maxUses), 
                  minimum_original_price: Number(minPrice), 
                  expires_at: expiresAt ? new Date(expiresAt).toISOString() : null, 
                  applicable_product_ids: applicableProducts.length > 0 ? applicableProducts : undefined,
                  is_active: selectedCoupon.is_active
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

      {/* Global Create Modal */}
      {isCreateModalOpen && (
        <div className="fixed inset-0 z-[60] flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/20 backdrop-blur-sm transition-opacity" onClick={() => !createMutation.isPending && setIsCreateModalOpen(false)} />
          <div className="relative bg-white border border-[#e5e5e5] shadow-2xl w-full max-w-lg flex flex-col animate-in fade-in zoom-in-95 duration-200 max-h-[90vh]">
            <div className="flex items-center justify-between p-4 border-b border-[#e5e5e5] bg-[#fafafa]/50 shrink-0">
              <h3 className="text-[13px] font-bold uppercase tracking-widest text-[#09090b]">Create Promo Code</h3>
              <button onClick={() => setIsCreateModalOpen(false)} disabled={createMutation.isPending} className="text-[#a1a1aa] hover:bg-[#e5e5e5] hover:text-[#09090b] transition-colors p-1 disabled:opacity-50"><X size={16} /></button>
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
                  <button type="button" onClick={() => setIsCreateModalOpen(false)} disabled={createMutation.isPending} className="h-8 px-4 rounded-sm border border-[#e5e5e5] bg-white text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:bg-[#f4f4f5] hover:text-[#09090b] transition-colors disabled:opacity-50">Cancel</button>
                  <button type="submit" disabled={createMutation.isPending} className="h-8 px-6 rounded-sm bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest flex items-center justify-center gap-2 hover:bg-[#27272a] transition-colors disabled:opacity-50">
                    {createMutation.isPending && <Loader2 size={13} className="animate-spin" />} Create
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
