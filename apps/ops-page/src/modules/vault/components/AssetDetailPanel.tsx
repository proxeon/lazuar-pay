import { useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2, Trash2, Edit2, Link as LinkIcon, FileText } from "lucide-react";
import { toast } from "sonner";
import { client, type components } from "../../../lib/api-client";
import { cn } from "../../../lib/utils";
import SidePanel from "../../core/components/SidePanel";
import QuickCopy from "../../core/components/QuickCopy";
import DigitalProductForm from "./DigitalProductForm";

type VaultAssetDto = components["schemas"]["Vault.VaultAssetDto"];

interface AssetDetailPanelProps {
  asset: VaultAssetDto | null;
  onClose: () => void;
  onUpdate: (asset: VaultAssetDto | null) => void;
}

export default function AssetDetailPanel({ asset, onClose, onUpdate }: AssetDetailPanelProps) {
  const queryClient = useQueryClient();
  const [isEditing, setIsEditing] = useState(false);
  const [isActionLoading, setIsActionLoading] = useState(false);

  const editMutation = useMutation({
    mutationFn: async (payload: any) => {
      const { id, ...body } = payload;
      const { error } = await client.PUT("/admin/vault/assets/{id}", {
        params: { path: { id } },
        body
      });
      if (error) throw new Error(error.detail);
      return payload;
    },
    onMutate: () => setIsActionLoading(true),
    onSettled: () => setIsActionLoading(false),
    onSuccess: (variables) => {
      toast.success("Asset saved successfully");
      queryClient.invalidateQueries({ queryKey: ["vault-assets"] });
      onUpdate({ ...asset!, ...variables });
      setIsEditing(false);
    },
    onError: (err: any) => toast.error("Failed to update asset", { description: err.message })
  });

  const deleteMutation = useMutation({
    mutationFn: async (id: string) => {
      const { error } = await client.DELETE("/admin/vault/assets/{id}", {
        params: { path: { id } }
      });
      if (error) throw new Error(error.detail);
    },
    onMutate: () => setIsActionLoading(true),
    onSettled: () => setIsActionLoading(false),
    onSuccess: () => {
      toast.success("Asset deleted successfully");
      queryClient.invalidateQueries({ queryKey: ["vault-assets"] });
      onUpdate(null);
    },
    onError: (err: any) => toast.error("Failed to delete asset", { description: err.message })
  });

  const handleClose = () => {
    setIsEditing(false);
    onClose();
  };

  return (
    <SidePanel
      isOpen={!!asset}
      onClose={handleClose}
      title="Asset Console"
      disableOutsideClick={isActionLoading || isEditing}
    >
      {asset && !isEditing && (
        <div className="space-y-8 animate-in fade-in duration-200">
          <div className="flex items-start justify-between border-b border-[#f4f4f5] pb-4">
            <div>
              <h3 className="text-xl font-bold text-[#09090b] tracking-tight">{asset.name}</h3>
              <div className="flex items-center gap-2 mt-1 text-[11px] font-mono text-[#71717a]">
                ID: {asset.id.substring(0,8)}
              </div>
            </div>
            <div className="h-10 w-10 bg-indigo-50 border border-indigo-100 flex items-center justify-center rounded-none shrink-0">
               <FileText size={20} className="text-indigo-600" />
            </div>
          </div>

          <div className="space-y-4">
            <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Fulfillment</h4>
            <div>
              <span className="text-[11px] text-[#a1a1aa] block mb-0.5">Secure Download Link (R2)</span>
              <div className="flex items-center gap-2">
                <a href={asset.cloudflare_r2_url} target="_blank" rel="noopener noreferrer" className="text-[12px] font-mono text-indigo-600 hover:opacity-80 underline underline-offset-2 truncate max-w-[280px]">
                  {asset.cloudflare_r2_url}
                </a>
                <QuickCopy text={asset.cloudflare_r2_url} iconSize={12} className="hover:bg-[#fafafa]" />
              </div>
            </div>
          </div>

          <div className="space-y-4">
            <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Linked Products</h4>
            <div className="bg-[#fafafa] border border-[#e5e5e5] p-3 text-[12px] text-[#52525b]">
               {asset.product_ids?.length || 0} product(s) linked.
            </div>
          </div>

          <div className="space-y-4 pt-4">
            <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Operations</h4>
            <div className="grid grid-cols-2 gap-2">
              <button 
                onClick={() => setIsEditing(true)} 
                disabled={isActionLoading} 
                className="h-8 border border-[#e5e5e5] bg-white text-[10px] font-bold uppercase tracking-widest text-[#09090b] hover:bg-[#f4f4f5] transition-colors disabled:opacity-50 flex items-center justify-center gap-1.5"
              >
                <Edit2 size={12} /> Edit Details
              </button>
              
              <button 
                onClick={() => { if(window.confirm("Permanently delete this asset?")) deleteMutation.mutate(asset.id); }} 
                disabled={isActionLoading} 
                className="h-8 border border-rose-200 bg-rose-50 text-[10px] font-bold uppercase tracking-widest text-rose-700 hover:bg-rose-100 transition-colors disabled:opacity-50 flex items-center justify-center gap-1.5"
              >
                {isActionLoading ? <Loader2 size={12} className="animate-spin" /> : <Trash2 size={12} />} Delete Asset
              </button>
            </div>
          </div>
        </div>
      )}

      {asset && isEditing && (
        <div className="absolute inset-0 bg-white z-10 flex flex-col animate-in slide-in-from-right duration-200">
          <div className="flex items-center justify-between p-5 border-b border-[#e5e5e5] bg-[#fafafa] shrink-0">
            <div>
              <h3 className="text-[15px] font-bold text-[#09090b]">Edit Asset</h3>
              <p className="text-[11px] font-mono text-[#71717a] mt-0.5">{asset.name}</p>
            </div>
          </div>
          
          <div className="flex-1 flex flex-col overflow-hidden bg-white min-h-0">
            <DigitalProductForm 
              initialData={{
                name: asset.name,
                cloudflare_r2_url: asset.cloudflare_r2_url,
                product_ids: asset.product_ids
              }}
              onSubmit={(data: any) => editMutation.mutate({ id: asset.id, ...data })} 
              onCancel={() => setIsEditing(false)} 
              isPending={editMutation.isPending}
              submitLabel="Save Changes"
            />
          </div>
        </div>
      )}
    </SidePanel>
  );
}
