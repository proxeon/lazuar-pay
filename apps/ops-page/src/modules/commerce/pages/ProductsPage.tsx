import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2, Plus, Archive, RotateCcw, Link as LinkIcon } from "lucide-react";
import { toast } from "sonner";
import { client, type EntitlementDto, type components } from "../../../lib/api-client";
import { useOutletContext } from "react-router-dom";
import PageLayout from "../../core/components/PageLayout";
import { cn } from "../../../lib/utils";
import CreateProductModal from "../components/CreateProductModal";
import QuickCopy from "../../core/components/QuickCopy";

type ProductDto = components["schemas"]["Commerce.ProductDto"];

export default function ProductsPage() {
  const { activeWorkspaceId } = useOutletContext<{ activeWorkspaceId: string | null }>();
  const queryClient = useQueryClient();
  
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);

  const { data: products, isLoading } = useQuery({
    queryKey: ["commerce-products"],
    queryFn: async () => {
      const { data, error } = await client.GET("/admin/commerce/products");
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

  const softDeleteMutation = useMutation({
    mutationFn: async (id: string) => {
      const { error } = await client.DELETE("/admin/commerce/products/{id}", {
        params: { path: { id } }
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("Product archived successfully");
      queryClient.invalidateQueries({ queryKey: ["commerce-products"] });
    },
    onError: (err: any) => toast.error("Failed to archive product", { description: err.message })
  });

  const activeWorkspaceSlug = entitlements?.find(e => e.workspace_id === activeWorkspaceId)?.workspace_slug;

  const generateCheckoutUrl = (productSlug: string) => {
    if (!activeWorkspaceSlug) return "";
    const baseUrl = import.meta.env.VITE_PORTAL_URL || "http://localhost:3004";
    return `${baseUrl}/${activeWorkspaceSlug}/checkout/${productSlug}`;
  };

  return (
    <PageLayout 
      title="Universal Products" 
      description="Manage all your checkouts, pricing rules, and fulfillment hooks across the ecosystem."
      breadcrumbs={[{ label: "Commerce", href: "/commerce/dashboard" }, { label: "Products" }]}
      actionButton={
        <button 
          onClick={() => setIsCreateModalOpen(true)}
          className="h-9 px-4 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest flex items-center gap-2 hover:bg-[#27272a] transition-colors"
        >
          <Plus size={14} /> New Product
        </button>
      }
    >
      <div className="bg-white border border-[#e5e5e5] rounded-none overflow-hidden">
        <div className="w-full overflow-x-auto min-h-[320px]">
          <table className="w-full text-left text-[13px] min-w-[800px]">
            <thead className="bg-[#fafafa] border-b border-[#e5e5e5] select-none">
              <tr>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[30%]">Product Details</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[20%]">Price</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[30%]">Fulfillment Hooks</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[20%]">Status / Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#f4f4f5]">
              {isLoading ? (
                <tr><td colSpan={4} className="py-12 text-center text-[#a1a1aa]"><Loader2 size={20} className="animate-spin mx-auto" /></td></tr>
              ) : products?.length === 0 ? (
                <tr>
                  <td colSpan={4} className="py-12 text-center text-[13px] text-[#71717a]">
                    No products found. Click "New Product" to build a checkout link.
                  </td>
                </tr>
              ) : (
                products?.map((product: ProductDto) => (
                  <tr 
                    key={product.id} 
                    className={cn(
                      "hover:bg-[#fafafa] transition-colors group",
                      !product.is_active && "opacity-60 bg-[#fafafa]/30"
                    )}
                  >
                    <td className="px-5 py-4">
                      <div className="flex items-center gap-2 mb-1">
                        <span className="font-bold text-[#09090b] text-[13px]">{product.name}</span>
                      </div>
                      <p className="text-[11px] font-mono text-[#71717a]">{product.slug}</p>
                    </td>
                    <td className="px-5 py-4">
                      <div className="font-mono text-[#09090b]">
                        <span className="font-bold">RM {product.price.toFixed(2)}</span>
                        <span className="text-[11px] text-[#71717a] ml-1 uppercase">{product.interval}</span>
                      </div>
                    </td>
                    <td className="px-5 py-4">
                      <div className="flex flex-wrap gap-1.5">
                        {product.fulfillment_targets.length === 0 ? (
                          <span className="text-[10px] bg-zinc-100 text-zinc-500 border border-zinc-200 px-1.5 py-0.5 font-bold uppercase tracking-widest">
                            Raw Link (No Fulfillment)
                          </span>
                        ) : (
                          product.fulfillment_targets.map((t, idx) => (
                            <span key={idx} className={cn(
                              "text-[9px] px-1.5 py-0.5 border font-bold uppercase tracking-widest whitespace-nowrap",
                              t === "internal:vault" ? "bg-indigo-50 text-indigo-700 border-indigo-200" :
                              t === "internal:community" ? "bg-blue-50 text-blue-700 border-blue-200" :
                              "bg-purple-50 text-purple-700 border-purple-200"
                            )}>
                              {t.replace("internal:", "")}
                            </span>
                          ))
                        )}
                      </div>
                    </td>
                    <td className="px-5 py-4 flex flex-col gap-2 items-start">
                      <span className={cn(
                        "text-[9px] px-1.5 py-0.5 border font-bold uppercase tracking-widest whitespace-nowrap inline-block",
                        product.is_active ? "bg-emerald-50 text-emerald-700 border-emerald-200" : "bg-zinc-100 text-zinc-500 border-zinc-200"
                      )}>
                        {product.is_active ? "Active" : "Archived"}
                      </span>
                      <div className="flex items-center gap-2 mt-1">
                        <QuickCopy text={generateCheckoutUrl(product.slug)} iconSize={12} className="border border-[#e5e5e5] bg-white h-6 w-6 rounded-sm" />
                        {product.is_active && (
                          <button onClick={() => { if(window.confirm("Archive product?")) softDeleteMutation.mutate(product.id); }} className="h-6 px-2 text-[10px] font-bold uppercase tracking-widest border border-rose-200 bg-rose-50 text-rose-700 hover:bg-rose-100 transition-colors rounded-sm">Archive</button>
                        )}
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      <CreateProductModal 
        isOpen={isCreateModalOpen} 
        onClose={() => setIsCreateModalOpen(false)} 
      />

    </PageLayout>
  );
}
