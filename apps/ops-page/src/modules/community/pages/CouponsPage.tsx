import { useState, useEffect } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2, Plus, X, Tag, MoreHorizontal, Link as LinkIcon, Edit2, Trash2, AlertTriangle } from "lucide-react";
import { toast } from "sonner";
import { client } from "../../../lib/api-client";
import PageLayout from "../../core/components/PageLayout";
import { cn } from "../../../lib/utils";

export default function CouponsPage() {
  const queryClient = useQueryClient();
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [editingCoupon, setEditingCoupon] = useState<any | null>(null);
  const [couponToDelete, setCouponToDelete] = useState<any | null>(null);
  const [openMenuId, setOpenMenuId] = useState<string | null>(null);

  const [code, setCode] = useState("");
  const [discountType, setDiscountType] = useState("PERCENTAGE");
  const [amount, setAmount] = useState(0);
  const [maxUses, setMaxUses] = useState(0);
  const [minPrice, setMinPrice] = useState(0);
  const [expiresAt, setExpiresAt] = useState("");
  const [applicablePlans, setApplicablePlans] = useState<string[]>([]);

  useEffect(() => {
    const closeMenu = (event: MouseEvent) => {
      const target = event.target as HTMLElement;
      if (target.closest("[data-menu-trigger]")) {
        return;
      }
      setOpenMenuId(null);
    };
    
    document.addEventListener("click", closeMenu);
    return () => document.removeEventListener("click", closeMenu);
  }, []);

  const { data: plans } = useQuery({
    queryKey: ["community-plans-lookup"],
    queryFn: async () => {
      const { data } = await client.GET("/admin/community/plans");
      return data || [];
    }
  });

  const { data: coupons, isLoading } = useQuery({
    queryKey: ["community-coupons"],
    queryFn: async () => {
      const { data, error } = await client.GET("/admin/community/coupons");
      if (error) throw new Error(error.detail);
      return data;
    }
  });

  const createMutation = useMutation({
    mutationFn: async () => {
      const { error } = await client.POST("/admin/community/coupons", {
        body: {
          code: code.trim().toUpperCase(),
          discount_type: discountType,
          amount: Number(amount),
          max_uses: Number(maxUses),
          minimum_original_price: Number(minPrice),
          expires_at: expiresAt ? new Date(expiresAt).toISOString() : undefined,
          applicable_plan_ids: applicablePlans.length > 0 ? applicablePlans : undefined
        }
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("Coupon created successfully");
      queryClient.invalidateQueries({ queryKey: ["community-coupons"] });
      setIsCreateModalOpen(false);
      resetFormStates();
    },
    onError: (err: any) => toast.error("Failed to create coupon", { description: err.message })
  });

  const editMutation = useMutation({
    mutationFn: async () => {
      if (!editingCoupon) return;
      const { error } = await client.PUT("/admin/community/coupons/{id}", {
        params: { path: { id: editingCoupon.id } },
        body: {
          max_uses: Number(maxUses),
          minimum_original_price: Number(minPrice),
          expires_at: expiresAt ? new Date(expiresAt).toISOString() : undefined,
          applicable_plan_ids: applicablePlans.length > 0 ? applicablePlans : undefined
        }
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("Promo limits saved successfully");
      queryClient.invalidateQueries({ queryKey: ["community-coupons"] });
      setEditingCoupon(null);
      resetFormStates();
    },
    onError: (err: any) => toast.error("Failed to update coupon", { description: err.message })
  });

  const softDeleteMutation = useMutation({
    mutationFn: async (id: string) => {
      const { error } = await client.DELETE("/admin/community/coupons/{id}", {
        params: { path: { id } }
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("Promo code soft-deleted and deactivated");
      queryClient.invalidateQueries({ queryKey: ["community-coupons"] });
      setCouponToDelete(null);
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
    setApplicablePlans([]);
  };

  const openCreateModal = () => {
    resetFormStates();
    setIsCreateModalOpen(true);
  };

  const openEditModal = (coupon: any) => {
    setCode(coupon.code);
    setDiscountType(coupon.discount_type);
    setAmount(coupon.amount);
    setMaxUses(coupon.max_uses);
    setMinPrice(coupon.minimum_original_price);
    setExpiresAt(coupon.expires_at ? coupon.expires_at.slice(0, 16) : "");
    setApplicablePlans(coupon.applicable_plan_ids || []);
    setEditingCoupon(coupon);
  };

  const copyPromo = (promoCode: string) => {
    navigator.clipboard.writeText(promoCode);
    toast.success(`Promo code "${promoCode}" copied to clipboard`);
  };

  const handlePlanToggle = (planId: string) => {
    setApplicablePlans(prev => 
      prev.includes(planId) 
        ? prev.filter(id => id !== planId)
        : [...prev, planId]
    );
  };

  return (
    <PageLayout 
      title="Promotions" 
      description="Create and track discount codes."
      breadcrumbs={[{ label: "Community", href: "/community/dashboard" }, { label: "Promotions" }]}
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
            <thead className="bg-[#fafafa] border-b border-[#e5e5e5]">
              <tr>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] whitespace-nowrap">Promo Code</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] whitespace-nowrap">Discount</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] whitespace-nowrap">Applies To</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] whitespace-nowrap">Usage Limit</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] whitespace-nowrap">Redeemed</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] whitespace-nowrap">Expiration</th>
                <th className="px-5 py-3 w-[5%]"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#f4f4f5]">
              {isLoading ? (
                <tr><td colSpan={7} className="py-12 text-center text-[#a1a1aa]"><Loader2 size={20} className="animate-spin mx-auto" /></td></tr>
              ) : coupons?.length === 0 ? (
                <tr><td colSpan={7} className="py-12 text-center text-[12px] text-[#71717a]">No promotional codes found.</td></tr>
              ) : (
                coupons?.map((coupon) => {
                  const isExpired = coupon.expires_at ? new Date(coupon.expires_at).getTime() < Date.now() : false;
                  return (
                    <tr key={coupon.id} className={cn("hover:bg-[#fafafa]/50 transition-colors", isExpired && "opacity-60 bg-[#fafafa]/30")}>
                      <td className="px-5 py-3.5 whitespace-nowrap">
                        <div className="flex items-center gap-2">
                          <Tag size={13} className="text-[#a1a1aa]" />
                          <span className={cn("font-mono font-bold px-2 py-0.5 border text-[11px]", isExpired ? "bg-zinc-100 text-zinc-500 border-zinc-200" : "bg-zinc-100 text-[#09090b] border-zinc-200")}>
                            {coupon.code}
                          </span>
                        </div>
                      </td>
                      <td className="px-5 py-3.5 font-mono text-[#52525b] whitespace-nowrap font-medium text-[12px]">
                        {coupon.discount_type === "PERCENTAGE" ? `${coupon.amount}%` : `RM ${coupon.amount.toFixed(2)}`}
                      </td>
                      <td className="px-5 py-3.5 text-[#52525b] whitespace-nowrap">
                        {coupon.applicable_plan_ids && coupon.applicable_plan_ids.length > 0 ? (
                          <span className="text-[9px] font-bold uppercase tracking-widest bg-blue-50 text-blue-700 border border-blue-200 px-1.5 py-0.5 rounded-sm">
                            Specific ({coupon.applicable_plan_ids.length})
                          </span>
                        ) : (
                          <span className="text-[9px] font-bold uppercase tracking-widest bg-emerald-50 text-emerald-700 border border-emerald-200 px-1.5 py-0.5 rounded-sm">
                            Global
                          </span>
                        )}
                      </td>
                      <td className="px-5 py-3.5 text-[#52525b] whitespace-nowrap">
                        {coupon.max_uses > 0 ? coupon.max_uses : "Unlimited"}
                      </td>
                      <td className="px-5 py-3.5 text-[#52525b] whitespace-nowrap">
                        <span className="font-bold text-[#09090b]">{coupon.used_count}</span> <span className="text-[10px] text-[#71717a]">({coupon.reserved_count} pending)</span>
                      </td>
                      <td className="px-5 py-3.5 text-[#52525b] text-[11px] font-mono whitespace-nowrap">
                        {coupon.expires_at ? <span className={cn(isExpired ? "text-rose-600 font-semibold" : "text-[#52525b]")}>{new Date(coupon.expires_at).toLocaleDateString('en-GB')} {isExpired && "(Expired)"}</span> : "Never"}
                      </td>
                      <td className="px-5 py-3.5 text-right relative">
                        <button data-menu-trigger={coupon.id} onClick={() => setOpenMenuId(openMenuId === coupon.id ? null : coupon.id)} className="p-1.5 text-[#a1a1aa] hover:text-[#09090b] hover:bg-[#f4f4f5] transition-colors rounded-sm focus:outline-none"><MoreHorizontal size={15} /></button>
                        {openMenuId === coupon.id && (
                          <div className="absolute right-5 top-full mt-1 w-44 bg-white border border-[#e5e5e5] rounded-none py-1 z-50 text-left animate-in fade-in slide-in-from-top-1 duration-150">
                            <button onClick={() => openEditModal(coupon)} className="w-full text-left px-3 py-1.5 text-[11px] font-medium text-[#09090b] hover:bg-[#f4f4f5] transition-colors flex items-center gap-2"><Edit2 size={12} className="text-[#71717a]" /> Edit Details</button>
                            <button onClick={() => { setOpenMenuId(null); copyPromo(coupon.code); }} className="w-full text-left px-3 py-1.5 text-[11px] font-medium text-[#09090b] hover:bg-[#f4f4f5] transition-colors flex items-center gap-2"><LinkIcon size={12} className="text-[#71717a]" /> Copy Code</button>
                            <div className="h-px w-full bg-[#f4f4f5] my-1" />
                            <button onClick={() => setCouponToDelete(coupon)} className="w-full text-left px-3 py-1.5 text-[11px] font-semibold text-rose-600 hover:bg-rose-50 transition-colors flex items-center gap-2"><Trash2 size={12} className="text-rose-500" /> Soft Delete</button>
                          </div>
                        )}
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </div>

      {isCreateModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/20 backdrop-blur-sm transition-opacity" onClick={() => !createMutation.isPending && setIsCreateModalOpen(false)} />
          <div className="relative bg-white border border-[#e5e5e5] shadow-xl w-full max-w-md flex flex-col animate-in fade-in zoom-in-95 duration-200 max-h-[90vh]">
            <div className="flex items-center justify-between p-4 border-b border-[#e5e5e5] bg-[#fafafa]/50 shrink-0">
              <h3 className="text-[13px] font-bold uppercase tracking-widest text-[#09090b]">Create Promo Code</h3>
              <button onClick={() => setIsCreateModalOpen(false)} disabled={createMutation.isPending} className="text-[#a1a1aa] hover:bg-[#e5e5e5] hover:text-[#09090b] transition-colors p-1 disabled:opacity-50"><X size={16} /></button>
            </div>
            <div className="overflow-y-auto flex-1">
              <form onSubmit={(e) => { e.preventDefault(); createMutation.mutate(); }}>
                <div className="p-5 space-y-4">
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
                  
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Target Scope (Applies To)</label>
                    <div className="border border-[#e5e5e5] rounded-sm bg-white overflow-hidden">
                      <div className="max-h-[120px] overflow-y-auto p-2 space-y-1">
                        {plans?.map((plan: any) => (
                          <label key={plan.id} className="flex items-center gap-2 p-1.5 hover:bg-[#fafafa] cursor-pointer rounded-sm">
                            <input 
                              type="checkbox" 
                              checked={applicablePlans.includes(plan.id)}
                              onChange={() => handlePlanToggle(plan.id)}
                              disabled={createMutation.isPending}
                              className="rounded-sm border-[#e5e5e5] text-[#09090b] focus:ring-[#09090b]"
                            />
                            <span className="text-[12px] text-[#09090b] font-medium">{plan.name}</span>
                          </label>
                        ))}
                      </div>
                      <div className="bg-[#fafafa] border-t border-[#e5e5e5] px-3 py-2 text-[10px] text-[#71717a]">
                        {applicablePlans.length === 0 ? "Applies to ALL plans (Global)" : `Applies to ${applicablePlans.length} specific plan(s)`}
                      </div>
                    </div>
                  </div>

                  <div className="grid grid-cols-2 gap-4">
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Max Uses (0 = ∞)</label>
                      <input type="number" value={maxUses} onChange={e => setMaxUses(Number(e.target.value))} disabled={createMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
                    </div>
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Min. Plan Price</label>
                      <input type="number" step="0.01" value={minPrice} onChange={e => setMinPrice(Number(e.target.value))} disabled={createMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
                    </div>
                  </div>
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Expires At (Optional)</label>
                    <input type="datetime-local" value={expiresAt} onChange={e => setExpiresAt(e.target.value)} disabled={createMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
                  </div>
                </div>
                <div className="px-5 py-3 border-t border-[#f4f4f5] bg-[#fafafa]/50 flex items-center justify-end gap-2.5">
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

      {editingCoupon && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/20 backdrop-blur-sm transition-opacity" onClick={() => !editMutation.isPending && setEditingCoupon(null)} />
          <div className="relative bg-white border border-[#e5e5e5] shadow-xl w-full max-w-md flex flex-col animate-in fade-in zoom-in-95 duration-200 max-h-[90vh]">
            <div className="flex items-center justify-between p-4 border-b border-[#e5e5e5] bg-[#fafafa]/50 shrink-0">
              <h3 className="text-[13px] font-bold uppercase tracking-widest text-[#09090b]">Edit Promo Limits</h3>
              <button onClick={() => setEditingCoupon(null)} disabled={editMutation.isPending} className="text-[#a1a1aa] hover:bg-[#e5e5e5] hover:text-[#09090b] transition-colors p-1 disabled:opacity-50"><X size={16} /></button>
            </div>
            <div className="overflow-y-auto flex-1">
              <form onSubmit={(e) => { e.preventDefault(); editMutation.mutate(); }}>
                <div className="p-5 space-y-4">
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Coupon Code</label>
                    <input disabled value={code} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-[#fafafa] px-3 py-1 font-mono text-[13px] focus:outline-none text-[#71717a]" />
                  </div>
                  <div className="grid grid-cols-2 gap-4">
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Type</label>
                      <select disabled value={discountType} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-[#fafafa] px-3 text-[13px] focus:outline-none text-[#71717a]">
                        <option value="PERCENTAGE">Percentage (%)</option>
                        <option value="FIXED">Fixed Amount (RM)</option>
                      </select>
                    </div>
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Amount</label>
                      <input disabled type="number" value={amount} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-[#fafafa] px-3 py-1 text-[13px] focus:outline-none text-[#71717a]" />
                    </div>
                  </div>

                  <div className="space-y-1.5">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Target Scope (Applies To)</label>
                    <div className="border border-[#e5e5e5] rounded-sm bg-white overflow-hidden">
                      <div className="max-h-[120px] overflow-y-auto p-2 space-y-1">
                        {plans?.map((plan: any) => (
                          <label key={plan.id} className="flex items-center gap-2 p-1.5 hover:bg-[#fafafa] cursor-pointer rounded-sm">
                            <input 
                              type="checkbox" 
                              checked={applicablePlans.includes(plan.id)}
                              onChange={() => handlePlanToggle(plan.id)}
                              disabled={editMutation.isPending}
                              className="rounded-sm border-[#e5e5e5] text-[#09090b] focus:ring-[#09090b]"
                            />
                            <span className="text-[12px] text-[#09090b] font-medium">{plan.name}</span>
                          </label>
                        ))}
                      </div>
                      <div className="bg-[#fafafa] border-t border-[#e5e5e5] px-3 py-2 text-[10px] text-[#71717a]">
                        {applicablePlans.length === 0 ? "Applies to ALL plans (Global)" : `Applies to ${applicablePlans.length} specific plan(s)`}
                      </div>
                    </div>
                  </div>

                  <div className="grid grid-cols-2 gap-4">
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Max Uses (0 = ∞)</label>
                      <input type="number" value={maxUses} onChange={e => setMaxUses(Number(e.target.value))} disabled={editMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
                    </div>
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Min. Plan Price</label>
                      <input type="number" step="0.01" value={minPrice} onChange={e => setMinPrice(Number(e.target.value))} disabled={editMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
                    </div>
                  </div>
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Expires At (Optional)</label>
                    <input type="datetime-local" value={expiresAt} onChange={e => setExpiresAt(e.target.value)} disabled={editMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
                  </div>
                </div>
                <div className="px-5 py-3 border-t border-[#f4f4f5] bg-[#fafafa]/50 flex items-center justify-end gap-2.5">
                  <button type="button" onClick={() => setEditingCoupon(null)} disabled={editMutation.isPending} className="h-8 px-4 rounded-sm border border-[#e5e5e5] bg-white text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:bg-[#f4f4f5] hover:text-[#09090b] transition-colors disabled:opacity-50">Cancel</button>
                  <button type="submit" disabled={editMutation.isPending} className="h-8 px-6 rounded-sm bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest flex items-center justify-center gap-2 hover:bg-[#27272a] transition-colors disabled:opacity-50">
                    {editMutation.isPending && <Loader2 size={13} className="animate-spin" />} Save Changes
                  </button>
                </div>
              </form>
            </div>
          </div>
        </div>
      )}

      {couponToDelete && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/20 backdrop-blur-sm transition-opacity" onClick={() => !softDeleteMutation.isPending && setCouponToDelete(null)} />
          <div className="relative bg-white border border-rose-200 w-full max-w-md flex flex-col animate-in fade-in zoom-in-95 duration-200">
            <div className="p-6 flex flex-col items-center text-center">
              <div className="h-10 w-10 rounded-full bg-rose-50 flex items-center justify-center mb-4">
                <AlertTriangle className="text-rose-600" size={20} />
              </div>
              <h3 className="text-[14px] font-bold uppercase tracking-widest text-[#09090b] mb-2">Soft Delete Coupon?</h3>
              <p className="text-[13px] text-[#71717a] leading-relaxed max-w-xs">
                You are archiving promotion <strong>{couponToDelete.code}</strong>. Active orders using this promotion code are unaffected, but no new customers will be allowed to apply this code during checkout.
              </p>
            </div>
            <div className="px-5 py-3.5 border-t border-[#f4f4f5] bg-[#fafafa]/50 flex items-center justify-end gap-2">
              <button onClick={() => setCouponToDelete(null)} disabled={softDeleteMutation.isPending} className="h-8 px-4 border border-[#e5e5e5] bg-white text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:bg-[#f4f4f5] hover:text-[#09090b] transition-colors disabled:opacity-50">Cancel</button>
              <button onClick={() => softDeleteMutation.mutate(couponToDelete.id)} disabled={softDeleteMutation.isPending} className="h-8 px-5 bg-rose-600 text-white text-[11px] font-bold uppercase tracking-widest hover:bg-rose-700 transition-colors flex items-center justify-center gap-1.5 disabled:opacity-50">
                {softDeleteMutation.isPending ? <Loader2 size={12} className="animate-spin" /> : <Trash2 size={12} />} Confirm Archive
              </button>
            </div>
          </div>
        </div>
      )}
    </PageLayout>
  );
}
