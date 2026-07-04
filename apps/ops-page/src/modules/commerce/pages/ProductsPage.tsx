import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Loader2, Plus, AlertTriangle, CreditCard } from "lucide-react";
import { Link } from "react-router-dom";
import { client, type EntitlementDto, type components } from "../../../lib/api-client";
import { useOutletContext } from "react-router-dom";
import PageLayout from "../../core/components/PageLayout";
import { cn, filterHiddenFulfillmentTargets } from "../../../lib/utils";
import CreateProductModal from "../components/CreateProductModal";
import ProductDetailPanel from "../components/ProductDetailPanel";
import QuickCopy from "../../core/components/QuickCopy";

type ProductDto = components["schemas"]["Commerce.ProductDto"];

export default function ProductsPage() {
  const { activeWorkspaceId } = useOutletContext<{ activeWorkspaceId: string | null }>();
  
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [selectedProduct, setSelectedProduct] = useState<ProductDto | null>(null);

  const { data: products, isLoading } = useQuery({
    queryKey: ["commerce-products"],
    queryFn: async () => {
      const { data, error } = await client.GET("/admin/commerce/products");
      if (error) throw new Error(error.detail);
      return data;
    },
    enabled: !!activeWorkspaceId
  });

  const { data: paymentConfig, error: paymentConfigError } = useQuery({
    queryKey: ["payment-config-status"],
    queryFn: async () => {
      const { data, error, response } = await client.GET("/admin/commerce/payment-config");
      if (response.status === 404) return null;
      if (error) throw new Error(error.detail);
      return data;
    },
    enabled: !!activeWorkspaceId
  });

  const { data: entitlements } = useQuery({
    queryKey: ["entitlements"],
    queryFn: async () => {
      const { data } = await client.GET("/one/me/entitlements");
      return data as EntitlementDto[];
    }
  });

  const activeWorkspaceSlug = entitlements?.find(e => e.workspace_id === activeWorkspaceId)?.workspace_slug;
  const showGatewayWarning = paymentConfigError || !paymentConfig || !paymentConfig.is_active;

  const renderFulfillmentBadges = (targets: string[] | undefined) => {
    const visible = filterHiddenFulfillmentTargets(targets);
    if (visible.length === 0) return null;
    return (
      <div className="flex flex-wrap gap-1 mt-1.5">
        {visible.map((target, idx) => {
          const isWebhook = target.startsWith("http");
          const label = isWebhook ? "Webhook" : "Fulfillment";
          const classes = isWebhook
            ? "bg-emerald-50 text-emerald-700 border-emerald-200 dark:bg-emerald-950/20 dark:text-emerald-400 dark:border-emerald-900/50"
            : "bg-zinc-50 text-zinc-600 border-zinc-200";

          return (
            <span key={idx} className={cn("text-[9px] font-bold uppercase tracking-widest px-1.5 py-0.5 border rounded-sm", classes)}>
              {label}
            </span>
          );
        })}
      </div>
    );
  };

  return (
    <PageLayout 
      title="Checkout Links" 
      description="Manage all your checkouts and pricing rules across the ecosystem."
      breadcrumbs={[{ label: "Commerce", href: "/commerce/dashboard" }, { label: "Checkout Links" }]}
      actionButton={
        <button 
          onClick={() => setIsCreateModalOpen(true)}
          className="h-9 px-4 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest flex items-center gap-2 hover:bg-[#27272a] transition-colors"
        >
          <Plus size={14} /> Create Link
        </button>
      }
    >
      <div className="flex flex-col gap-6">
        
        {showGatewayWarning && (
          <div className="flex items-center justify-between p-4 bg-rose-50 border border-rose-200">
            <div className="flex items-center gap-3">
              <AlertTriangle size={18} className="text-rose-600" />
              <div>
                <p className="text-[13px] font-bold text-rose-800">Action Required: Payment Gateway Not Configured</p>
                <p className="text-[12px] text-rose-700 mt-0.5">Your checkout links cannot accept payments. Customers will be unable to purchase your products.</p>
              </div>
            </div>
            <Link to="/workspace/payment-gateways" className="h-8 px-4 bg-rose-600 hover:bg-rose-700 text-white text-[11px] font-bold uppercase tracking-widest flex items-center gap-2 transition-colors">
              <CreditCard size={14} /> Configure Now
            </Link>
          </div>
        )}

        <div className="bg-white border border-[#e5e5e5] rounded-none overflow-hidden">
          <div className="w-full overflow-x-auto min-h-[320px]">
            <table className="w-full text-left text-[13px] min-w-[800px]">
              <thead className="bg-[#fafafa] border-b border-[#e5e5e5] select-none">
                <tr>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[35%]">Link Details</th>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[20%]">Pricing Model</th>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[15%]">Price (MYR)</th>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[15%]">Billing Interval</th>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[15%]">Status</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#f4f4f5]">
                {isLoading ? (
                  <tr><td colSpan={5} className="py-12 text-center text-[#a1a1aa]"><Loader2 size={20} className="animate-spin mx-auto" /></td></tr>
                ) : products?.length === 0 ? (
                  <tr>
                    <td colSpan={5} className="py-12 text-center text-[13px] text-[#71717a]">
                      No checkout links found. Click "Create Link" to build one.
                    </td>
                  </tr>
                ) : (
                  products?.map((product: ProductDto) => (
                    <tr 
                      key={product.id} 
                      onClick={() => setSelectedProduct(product)}
                      className={cn(
                        "hover:bg-[#fafafa] transition-colors cursor-pointer group",
                        !product.is_active && "opacity-60 bg-[#fafafa]/30"
                      )}
                    >
                      <td className="px-5 py-4">
                        <div className="flex items-center gap-2 mb-1">
                          <span className="font-bold text-[#09090b] text-[13px] group-hover:text-blue-600 transition-colors">{product.name}</span>
                        </div>
                        <div className="flex items-center gap-1.5">
                          <p className="text-[11px] font-mono text-[#71717a]">{product.slug}</p>
                          <QuickCopy text={product.slug} iconSize={10} className="p-0.5 opacity-0 group-hover:opacity-100 transition-opacity" />
                        </div>
                        {renderFulfillmentBadges(product.fulfillment_targets)}
                      </td>
                      <td className="px-5 py-4">
                        <span className="text-[11px] font-medium text-[#52525b]">
                          {product.pricing_model === "PWYW" ? "Pay What You Want" : "Fixed Price"}
                        </span>
                      </td>
                      <td className="px-5 py-4">
                        <span className="font-mono font-bold text-[#09090b]">
                          RM {product.price.toFixed(2)}
                        </span>
                      </td>
                      <td className="px-5 py-4 font-mono text-[11px] text-[#52525b] uppercase font-bold">
                        {product.interval}
                      </td>
                      <td className="px-5 py-4">
                        <span className={cn(
                          "text-[9px] px-1.5 py-0.5 border font-bold uppercase tracking-widest whitespace-nowrap inline-block",
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
      </div>

      <CreateProductModal 
        isOpen={isCreateModalOpen} 
        onClose={() => setIsCreateModalOpen(false)} 
      />

      <ProductDetailPanel 
        product={selectedProduct} 
        activeWorkspaceSlug={activeWorkspaceSlug}
        onClose={() => setSelectedProduct(null)} 
        onUpdate={setSelectedProduct}
      />
    </PageLayout>
  );
}
