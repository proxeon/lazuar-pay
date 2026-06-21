import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2, Plus, X, Tag } from "lucide-react";
import { toast } from "sonner";
import { client } from "../../../lib/api-client";
import PageLayout from "../../core/components/PageLayout";

export default function CouponsPage() {
  const queryClient = useQueryClient();
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);

  // Form State
  const [code, setCode] = useState("");
  const [discountType, setDiscountType] = useState("PERCENTAGE");
  const [amount, setAmount] = useState(0);
  const [maxUses, setMaxUses] = useState(0);
  const [minPrice, setMinPrice] = useState(0);
  const [expiresAt, setExpiresAt] = useState("");

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
          expires_at: expiresAt ? new Date(expiresAt).toISOString() : undefined
        }
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("Coupon created successfully");
      queryClient.invalidateQueries({ queryKey: ["community-coupons"] });
      setIsCreateModalOpen(false);
      setCode("");
      setAmount(0);
    },
    onError: (err: any) => toast.error("Failed to create coupon", { description: err.message })
  });

  return (
    <PageLayout 
      title="Promotions" 
      description="Create and track discount codes."
      breadcrumbs={[{ label: "Community", href: "/community/dashboard" }, { label: "Promotions" }]}
      actionButton={
        <button 
          onClick={() => setIsCreateModalOpen(true)}
          className="h-9 px-4 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest flex items-center gap-2 hover:bg-[#27272a] transition-colors"
        >
          <Plus size={14} /> Create Coupon
        </button>
      }
    >
      <div className="bg-white border border-[#e5e5e5] rounded-none overflow-hidden">
        <div className="overflow-x-auto min-h-[400px]">
          <table className="w-full text-left text-[13px]">
            <thead className="bg-[#fafafa] border-b border-[#e5e5e5]">
              <tr>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Promo Code</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Discount</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Usage Limit</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Redeemed</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Expiration</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#f4f4f5]">
              {isLoading ? (
                <tr><td colSpan={5} className="py-12 text-center text-[#a1a1aa]"><Loader2 size={20} className="animate-spin mx-auto" /></td></tr>
              ) : coupons?.length === 0 ? (
                <tr><td colSpan={5} className="py-12 text-center text-[12px] text-[#71717a]">No promotional codes found.</td></tr>
              ) : (
                coupons?.map((coupon) => (
                  <tr key={coupon.id} className="hover:bg-[#fafafa]/50 transition-colors">
                    <td className="px-5 py-3.5">
                      <div className="flex items-center gap-2">
                        <Tag size={13} className="text-[#a1a1aa]" />
                        <span className="font-mono font-bold text-[#09090b] bg-zinc-100 px-2 py-0.5 border border-zinc-200">{coupon.code}</span>
                      </div>
                    </td>
                    <td className="px-5 py-3.5 font-mono text-[#52525b]">
                      {coupon.discount_type === "PERCENTAGE" ? `${coupon.amount}%` : `RM ${coupon.amount.toFixed(2)}`}
                    </td>
                    <td className="px-5 py-3.5 text-[#52525b]">
                      {coupon.max_uses > 0 ? coupon.max_uses : "Unlimited"}
                    </td>
                    <td className="px-5 py-3.5 text-[#52525b]">
                      <span className="font-bold text-[#09090b]">{coupon.used_count}</span> <span className="text-[10px]">({coupon.reserved_count} pending)</span>
                    </td>
                    <td className="px-5 py-3.5 text-[#52525b] text-[11px] font-mono">
                      {coupon.expires_at ? new Date(coupon.expires_at).toLocaleDateString('en-GB') : "Never"}
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {isCreateModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/20 backdrop-blur-sm transition-opacity" onClick={() => setIsCreateModalOpen(false)} />
          <div className="relative bg-white border border-[#e5e5e5] shadow-xl w-full max-w-md flex flex-col animate-in fade-in zoom-in-95 duration-200">
            <div className="flex items-center justify-between p-4 border-b border-[#e5e5e5] bg-[#fafafa]/50 shrink-0">
              <h3 className="text-[13px] font-bold uppercase tracking-widest text-[#09090b]">Create Promo Code</h3>
              <button onClick={() => setIsCreateModalOpen(false)} className="text-[#a1a1aa] hover:bg-[#e5e5e5] hover:text-[#09090b] transition-colors p-1"><X size={16} /></button>
            </div>
            <form onSubmit={(e) => { e.preventDefault(); createMutation.mutate(); }} className="flex flex-col">
              <div className="p-5 space-y-4">
                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Coupon Code *</label>
                  <input required value={code} onChange={e => setCode(e.target.value.toUpperCase())} placeholder="e.g. SUMMER20" className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 font-mono text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b]" />
                </div>
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Type</label>
                    <select value={discountType} onChange={e => setDiscountType(e.target.value)} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b]">
                      <option value="PERCENTAGE">Percentage (%)</option>
                      <option value="FIXED">Fixed Amount (RM)</option>
                    </select>
                  </div>
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Amount *</label>
                    <input type="number" step="0.01" required value={amount} onChange={e => setAmount(Number(e.target.value))} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b]" />
                  </div>
                </div>
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Max Uses (0 = ∞)</label>
                    <input type="number" value={maxUses} onChange={e => setMaxUses(Number(e.target.value))} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b]" />
                  </div>
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Min. Plan Price</label>
                    <input type="number" step="0.01" value={minPrice} onChange={e => setMinPrice(Number(e.target.value))} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b]" />
                  </div>
                </div>
                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Expires At (Optional)</label>
                  <input type="datetime-local" value={expiresAt} onChange={e => setExpiresAt(e.target.value)} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b]" />
                </div>
              </div>
              <div className="px-5 py-3 border-t border-[#f4f4f5] bg-[#fafafa]/50 flex items-center justify-end gap-2.5">
                <button type="button" onClick={() => setIsCreateModalOpen(false)} className="h-8 px-4 rounded-sm border border-[#e5e5e5] bg-white text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:bg-[#f4f4f5] hover:text-[#09090b] transition-colors">
                  Cancel
                </button>
                <button type="submit" disabled={createMutation.isPending} className="h-8 px-6 rounded-sm bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest flex items-center justify-center gap-2 hover:bg-[#27272a] transition-colors disabled:opacity-50">
                  {createMutation.isPending && <Loader2 size={13} className="animate-spin" />} Create
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </PageLayout>
  );
}
