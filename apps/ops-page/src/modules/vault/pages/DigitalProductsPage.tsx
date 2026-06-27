import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Loader2, Plus, FileText } from "lucide-react";
import { client, type EntitlementDto } from "../../../lib/api-client";
import { useOutletContext } from "react-router-dom";
import PageLayout from "../../core/components/PageLayout";
import { cn } from "../../../lib/utils";
import CreateProductModal from "../components/CreateProductModal";
import ProductDetailPanel from "../components/ProductDetailPanel";

export default function DigitalProductsPage() {
  const { activeWorkspaceId } = useOutletContext<{ activeWorkspaceId: string | null }>();
  
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [selectedProduct, setSelectedProduct] = useState<any | null>(null);

  const { data: products, isLoading } = useQuery({
    queryKey: ["commerce-products"],
    queryFn: async () => {
      try {
        const { data, error } = await client.GET("/admin/commerce/products");
        if (error) throw new Error(error.detail);
        return data || [];
      } catch {
        return [];
      }
    }
  });

  const { data: entitlements } = useQuery({
    queryKey: ["entitlements"],
    queryFn: async () => {
      const { data } = await client.GET("/one/me/entitlements");
      return data as EntitlementDto[];
    }
  });

  const digitalProducts = products?.filter(p => p.fulfillment_targets?.includes("internal:vault")) || [];
  const activeWorkspaceSlug = entitlements?.find(e => e.workspace_id === activeWorkspaceId)?.workspace_slug;

  return (
    <PageLayout 
      title="Digital Products" 
      description="Upload and sell PDFs, templates, and zip files."
      breadcrumbs={[{ label: "Vault", href: "/vault/products" }, { label: "Digital Products" }]}
    >
      <div className="bg-white border border-[#e5e5e5] rounded-none overflow-hidden">
        <div className="w-full overflow-x-auto min-h-[320px]">
          <table className="w-full text-left text-[13px] min-w-[700px]">
            <thead className="bg-[#fafafa] border-b border-[#e5e5e5] select-none">
              <tr>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Product Details</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Price (MYR)</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Status</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#f4f4f5]">
              {isLoading ? (
                <tr><td colSpan={3} className="py-12 text-center text-[#a1a1aa]"><Loader2 size={20} className="animate-spin mx-auto" /></td></tr>
              ) : digitalProducts.length === 0 ? (
                <tr>
                  <td colSpan={3} className="py-12 text-center text-[13px] text-[#71717a] leading-relaxed">
                    No digital products found.<br/>
                    Go to <strong>Commerce → Products</strong> and enable the Digital File toggle to attach files.
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

      <ProductDetailPanel 
        product={selectedProduct} 
        activeWorkspaceSlug={activeWorkspaceSlug}
        onClose={() => setSelectedProduct(null)} 
        onUpdate={setSelectedProduct}
      />

    </PageLayout>
  );
}
