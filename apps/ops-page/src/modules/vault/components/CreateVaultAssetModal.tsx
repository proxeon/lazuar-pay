import { useState, useRef } from "react";
import { useMutation, useQueryClient, useQuery } from "@tanstack/react-query";
import { X, Loader2, UploadCloud, FileText } from "lucide-react";
import { toast } from "sonner";
import { client } from "../../../lib/api-client";

interface CreateVaultAssetModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export default function CreateVaultAssetModal({ isOpen, onClose }: CreateVaultAssetModalProps) {
  const queryClient = useQueryClient();
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [name, setName] = useState("");
  const [selectedProductIds, setSelectedProductIds] = useState<string[]>([]);
  const [isUploading, setIsUploading] = useState(false);
  const [uploadedUrl, setUploadedUrl] = useState("");

  const { data: products } = useQuery({
    queryKey: ["commerce-products"],
    queryFn: async () => {
      const { data } = await client.GET("/admin/commerce/products");
      return data || [];
    },
    enabled: isOpen
  });

  const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setIsUploading(true);

    try {
      const { data, error } = await client.POST("/admin/vault/presigned-url", {
        body: {
          file_name: file.name,
          content_type: file.type || "application/octet-stream"
        }
      });

      if (error || !data) throw new Error(error?.detail || "Failed to generate secure upload link.");

      const uploadResponse = await fetch(data.upload_url, {
        method: "PUT",
        body: file,
        headers: {
          "Content-Type": file.type || "application/octet-stream"
        }
      });

      if (!uploadResponse.ok) {
        throw new Error("Failed to upload file to storage bucket. Check CORS configuration.");
      }
      
      setUploadedUrl(data.final_url);
      toast.success("File securely uploaded to Vault.");
    } catch (err: any) {
      toast.error(err.message);
    } finally {
      setIsUploading(false);
    }
  };

  const createMutation = useMutation({
    mutationFn: async () => {
      if (!uploadedUrl) throw new Error("A digital file is required.");
      if (selectedProductIds.length === 0) throw new Error("Select at least one Commerce Product to unlock this file.");

      const { error } = await client.POST("/admin/vault/assets", {
        body: {
          name: name.trim(),
          cloudflare_r2_url: uploadedUrl,
          product_ids: selectedProductIds
        }
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("Digital Asset created successfully.");
      queryClient.invalidateQueries({ queryKey: ["vault-assets"] });
      onClose();
    },
    onError: (err: any) => toast.error(err.message)
  });

  const handleProductToggle = (id: string) => {
    setSelectedProductIds(prev => 
      prev.includes(id) ? prev.filter(p => p !== id) : [...prev, id]
    );
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/20 backdrop-blur-sm transition-opacity" onClick={() => !createMutation.isPending && onClose()} />
      <div className="relative bg-white border border-[#e5e5e5] shadow-2xl w-full max-w-lg flex flex-col animate-in fade-in zoom-in-95 duration-200 max-h-[90vh]">
        <div className="flex items-center justify-between p-4 border-b border-[#e5e5e5] bg-[#fafafa] shrink-0">
          <h3 className="text-[13px] font-bold uppercase tracking-widest text-[#09090b]">Create Digital Asset</h3>
          <button onClick={() => !createMutation.isPending && onClose()} disabled={createMutation.isPending} className="text-[#a1a1aa] hover:bg-[#e5e5e5] hover:text-[#09090b] transition-colors p-1 disabled:opacity-50">
            <X size={16} />
          </button>
        </div>
        
        <form onSubmit={(e) => { e.preventDefault(); createMutation.mutate(); }} className="flex flex-col flex-1 min-h-0">
          <div className="p-6 space-y-6 flex-1 overflow-y-auto bg-[#fafafa]/30">
            
            <div className="space-y-4">
              <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">Asset Details</label>
              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Asset Name *</label>
                <input required value={name} onChange={e => setName(e.target.value)} disabled={createMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
              </div>
            </div>

            <div className="space-y-4">
              <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">Secure File Upload *</label>
              <div className="flex flex-col gap-3 p-6 border border-dashed border-[#a1a1aa] bg-[#fafafa] rounded-sm text-center items-center justify-center relative hover:bg-[#f4f4f5] transition-colors group cursor-pointer">
                <input 
                  type="file" 
                  ref={fileInputRef}
                  onChange={handleFileUpload}
                  className="absolute inset-0 w-full h-full opacity-0 cursor-pointer disabled:cursor-not-allowed"
                  disabled={isUploading || createMutation.isPending}
                />
                {isUploading ? (
                  <>
                    <Loader2 className="animate-spin text-[#a1a1aa] mb-2" size={28} />
                    <span className="text-[11px] font-medium text-[#71717a]">Uploading directly to Cloudflare R2...</span>
                  </>
                ) : uploadedUrl ? (
                  <>
                    <FileText className="text-emerald-600 mb-2" size={28} />
                    <span className="text-[11px] font-medium text-emerald-700">File uploaded securely. Click to replace.</span>
                  </>
                ) : (
                  <>
                    <UploadCloud className="text-[#a1a1aa] group-hover:text-[#09090b] transition-colors mb-2" size={28} />
                    <span className="text-[11px] font-medium text-[#71717a] group-hover:text-[#09090b]">Click or drag PDF/ZIP file here</span>
                  </>
                )}
              </div>
            </div>

            <div className="space-y-4">
              <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">Unlocked By Commerce Products *</label>
              <div className="border border-[#e5e5e5] rounded-sm bg-white overflow-hidden">
                <div className="max-h-[160px] overflow-y-auto p-2 space-y-1">
                  {products?.map((product: any) => (
                    <label key={product.id} className="flex items-center gap-2 p-1.5 hover:bg-[#fafafa] cursor-pointer rounded-sm">
                      <input 
                        type="checkbox" 
                        checked={selectedProductIds.includes(product.id)}
                        onChange={() => handleProductToggle(product.id)}
                        disabled={createMutation.isPending}
                        className="rounded-sm border-[#e5e5e5] text-[#09090b] focus:ring-[#09090b]"
                      />
                      <span className="text-[12px] text-[#09090b] font-medium">{product.name}</span>
                    </label>
                  ))}
                  {products?.length === 0 && <span className="text-[11px] text-[#a1a1aa] p-2">No Commerce Products found.</span>}
                </div>
              </div>
            </div>

          </div>

          <div className="px-5 py-4 border-t border-[#e5e5e5] bg-[#fafafa]/50 flex justify-end gap-2 shrink-0">
            <button type="button" onClick={onClose} disabled={createMutation.isPending} className="px-4 h-8 text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:bg-[#e5e5e5] hover:text-[#09090b] border border-[#e5e5e5] bg-white transition-colors disabled:opacity-50 rounded-sm">Cancel</button>
            <button type="submit" disabled={createMutation.isPending || !uploadedUrl || selectedProductIds.length === 0} className="px-6 h-8 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest hover:bg-[#27272a] disabled:opacity-50 flex items-center gap-1.5 rounded-sm">
              {createMutation.isPending && <Loader2 size={13} className="animate-spin" />} Create Asset
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
