import { useState } from "react";
import { useOutletContext } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { Plus, Loader2, FileText, ExternalLink } from "lucide-react";
import { client, type components } from "../../../lib/api-client";
import PageLayout from "../../core/components/PageLayout";
import CreateVaultAssetModal from "../components/CreateVaultAssetModal";
import AssetDetailPanel from "../components/AssetDetailPanel";

type VaultAssetDto = components["schemas"]["Vault.VaultAssetDto"];

export default function DigitalProductsPage() {
  const { activeWorkspaceId } = useOutletContext<{ activeWorkspaceId: string | null }>();
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [selectedAsset, setSelectedAsset] = useState<VaultAssetDto | null>(null);

  const { data: assets, isLoading } = useQuery({
    queryKey: ["vault-assets"],
    queryFn: async () => {
      const { data, error } = await client.GET("/admin/vault/assets");
      if (error) throw new Error(error.detail);
      return data;
    },
    enabled: !!activeWorkspaceId
  });

  return (
    <PageLayout 
      title="Digital Products" 
      description="Upload and sell PDFs, templates, and zip files."
      breadcrumbs={[{ label: "Vault", href: "/vault/products" }, { label: "Digital Products" }]}
      actionButton={
        <button 
          onClick={() => setIsCreateModalOpen(true)}
          className="h-9 px-4 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest flex items-center gap-2 hover:bg-[#27272a] transition-colors"
        >
          <Plus size={14} /> Create Digital Asset
        </button>
      }
    >
      <div className="bg-white border border-[#e5e5e5] rounded-none overflow-hidden">
        <div className="w-full overflow-x-auto min-h-[320px]">
          <table className="w-full text-left text-[13px] min-w-[750px]">
            <thead className="bg-[#fafafa] border-b border-[#e5e5e5] select-none">
              <tr>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[35%]">Asset Name</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[45%]">R2 Storage Link</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[20%]">Linked Products</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#f4f4f5]">
              {isLoading ? (
                <tr>
                  <td colSpan={3} className="py-12 text-center text-[#a1a1aa]">
                    <Loader2 className="animate-spin mx-auto" size={20} />
                  </td>
                </tr>
              ) : assets?.length === 0 ? (
                <tr>
                  <td colSpan={3} className="py-12 text-center text-[13px] text-[#71717a] leading-relaxed">
                    No digital products found.<br /> 
                    Click "Create Digital Asset" to securely upload a file to R2.
                  </td>
                </tr>
              ) : (
                assets?.map((asset) => (
                  <tr key={asset.id} onClick={() => setSelectedAsset(asset)} className="hover:bg-[#fafafa] transition-colors cursor-pointer">
                    <td className="px-5 py-4">
                      <div className="flex items-center gap-2 mb-1">
                        <FileText size={14} className="text-indigo-600" />
                        <span className="font-bold text-[#09090b] text-[13px]">{asset.name}</span>
                      </div>
                    </td>
                    <td className="px-5 py-4 space-y-1.5">
                      <a href={asset.cloudflare_r2_url} target="_blank" rel="noopener noreferrer" onClick={e => e.stopPropagation()} className="flex items-center gap-1.5 text-[11px] font-mono text-indigo-600 hover:underline truncate max-w-[300px]">
                        <ExternalLink size={12} className="shrink-0" /> {asset.cloudflare_r2_url}
                      </a>
                    </td>
                    <td className="px-5 py-4 font-mono text-[11px] text-[#71717a]">
                      {asset.product_ids?.length || 0} product(s)
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      <CreateVaultAssetModal 
        isOpen={isCreateModalOpen}
        onClose={() => setIsCreateModalOpen(false)}
      />

      <AssetDetailPanel
        asset={selectedAsset}
        onClose={() => setSelectedAsset(null)}
        onUpdate={setSelectedAsset}
      />
    </PageLayout>
  );
}
