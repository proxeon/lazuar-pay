import { useState } from "react";
import { useOutletContext } from "react-router-dom";
import { Plus } from "lucide-react";
import PageLayout from "../../core/components/PageLayout";
import CreateVaultAssetModal from "../components/CreateVaultAssetModal";

export default function DigitalProductsPage() {
  const { activeWorkspaceId } = useOutletContext<{ activeWorkspaceId: string | null }>();
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);

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
        <div className="w-full overflow-x-auto min-h-[320px] flex items-center justify-center">
            <p className="text-[13px] text-[#71717a] leading-relaxed text-center p-12">
              No digital products found.<br/>
              Click "Create Digital Asset" to securely upload a file to R2.
            </p>
        </div>
      </div>

      <CreateVaultAssetModal 
        isOpen={isCreateModalOpen}
        onClose={() => setIsCreateModalOpen(false)}
      />

    </PageLayout>
  );
}
