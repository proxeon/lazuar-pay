import { useState } from "react";
import { useOutletContext } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { Loader2, Plus, Tag, Infinity as InfinityIcon } from "lucide-react";
import { client, type components } from "../../../lib/api-client";
import PageLayout from "../../core/components/PageLayout";
import QuickCopy from "../../core/components/QuickCopy";
import { cn } from "../../../lib/utils";
import CreateCouponModal from "../components/CreateCouponModal";
import CouponDetailPanel from "../components/CouponDetailPanel";

type CouponDto = components["schemas"]["Commerce.CouponDto"];

export default function CouponsPage() {
  const { activeWorkspaceId } = useOutletContext<{ activeWorkspaceId: string | null }>();
  
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [selectedCoupon, setSelectedCoupon] = useState<CouponDto | null>(null);

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

  const isCouponExpired = (dateString?: string | null) => {
    if (!dateString) return false;
    return new Date(dateString).getTime() < Date.now();
  };

  return (
    <PageLayout 
      title="Promotions" 
      description="Create and track discount codes and limits."
      breadcrumbs={[{ label: "Commerce", href: "/commerce/dashboard" }, { label: "Promotions" }]}
      actionButton={
        <button 
          onClick={() => setIsCreateModalOpen(true)}
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
                        {coupon.max_uses > 0 ? coupon.max_uses : <InfinityIcon size={14} className="text-[#a1a1aa]" />}
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

      <CreateCouponModal 
        isOpen={isCreateModalOpen} 
        onClose={() => setIsCreateModalOpen(false)} 
        products={products}
      />

      <CouponDetailPanel 
        coupon={selectedCoupon} 
        products={products} 
        onClose={() => setSelectedCoupon(null)} 
        onUpdate={setSelectedCoupon}
      />

    </PageLayout>
  );
}
