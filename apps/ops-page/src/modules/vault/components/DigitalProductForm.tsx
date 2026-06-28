import { useState, useRef, useEffect } from "react";
import { useQuery } from "@tanstack/react-query";
import { Loader2, UploadCloud, FileText } from "lucide-react";
import { toast } from "sonner";
import { client } from "../../../lib/api-client";

interface DigitalProductFormProps {
  initialData?: any | null;
  onSubmit: (data: any) => void;
  onCancel: () => void;
  isPending: boolean;
  submitLabel: React.ReactNode;
}

export default function DigitalProductForm({
  initialData,
  onSubmit,
  onCancel,
  isPending,
  submitLabel
}: DigitalProductFormProps) {
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [isUploading, setIsUploading] = useState(false);
  const [uploadedUrl, setUploadedUrl] = useState("");

  const [name, setName] = useState("");
  const [selectedProductIds, setSelectedProductIds] = useState<string[]>([]);

  const { data: products, isLoading: isProductsLoading } = useQuery({
    queryKey: ["commerce-products"],
    queryFn: async () => {
      const { data } = await client.GET("/admin/commerce/products");
      return data || [];
    }
  });

  useEffect(() => {
    if (initialData) {
      setName(initialData.name);
      setUploadedUrl(initialData.cloudflare_r2_url || "");
      setSelectedProductIds(initialData.product_ids || []);
    }
  }, [initialData]);

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

  const handleProductToggle = (id: string) => {
    setSelectedProductIds(prev => 
      prev.includes(id) ? prev.filter(p => p !== id) : [...prev, id]
    );
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!uploadedUrl) {
      toast.error("You must upload a file for digital products.");
      return;
    }

    onSubmit({
      name: name.trim(),
      cloudflare_r2_url: uploadedUrl,
      product_ids: selectedProductIds
    });
  };

  return (
    <form onSubmit={handleSubmit} className="flex flex-col h-full min-h-0">
      <div className="p-6 space-y-6 flex-1 overflow-y-auto">
        
        <div className="space-y-4">
          <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">Asset Details</label>
          <div className="space-y-1.5">
            <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Name *</label>
            <input required value={name} onChange={e => setName(e.target.value)} disabled={isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
          </div>
        </div>

        <div className="space-y-4">
          <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">File Upload *</label>
          <div className="flex flex-col gap-3 p-6 border border-dashed border-[#a1a1aa] bg-[#fafafa] rounded-sm text-center items-center justify-center relative hover:bg-[#f4f4f5] transition-colors group cursor-pointer">
            <input 
              type="file" 
              ref={fileInputRef}
              onChange={handleFileUpload}
              className="absolute inset-0 w-full h-full opacity-0 cursor-pointer disabled:cursor-not-allowed"
              disabled={isUploading || isPending}
              required={!uploadedUrl}
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
          {uploadedUrl && (
            <p className="text-[10px] font-mono text-emerald-600 truncate bg-emerald-50 p-2 border border-emerald-200">Attached: {uploadedUrl.split('/').pop()}</p>
          )}
        </div>

        <div className="space-y-4">
          <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">Unlocked By Commerce Products</label>
          <div className="border border-[#e5e5e5] rounded-sm bg-white overflow-hidden">
            <div className="max-h-[160px] overflow-y-auto p-2 space-y-1">
              {products?.map((product: any) => (
                <label key={product.id} className="flex items-center gap-2 p-1.5 hover:bg-[#fafafa] cursor-pointer rounded-sm">
                  <input 
                    type="checkbox" 
                    checked={selectedProductIds.includes(product.id)}
                    onChange={() => handleProductToggle(product.id)}
                    disabled={isPending}
                    className="rounded-sm border-[#e5e5e5] text-[#09090b] focus:ring-[#09090b]"
                  />
                  <span className="text-[12px] text-[#09090b] font-medium">{product.name}</span>
                </label>
              ))}
              {isProductsLoading && <div className="flex justify-center p-4"><Loader2 className="animate-spin text-[#a1a1aa]" size={16} /></div>}
            </div>
          </div>
        </div>

      </div>

      <div className="px-5 py-4 border-t border-[#e5e5e5] bg-[#fafafa]/50 flex justify-end gap-2 shrink-0">
        <button type="button" onClick={onCancel} disabled={isPending} className="px-4 h-8 text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:bg-[#e5e5e5] hover:text-[#09090b] border border-[#e5e5e5] bg-white transition-colors disabled:opacity-50 rounded-sm">Cancel</button>
        <button type="submit" disabled={isPending || (!uploadedUrl && !initialData)} className="px-6 h-8 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest hover:bg-[#27272a] disabled:opacity-50 flex items-center gap-1.5 rounded-sm">
          {isPending && <Loader2 size={13} className="animate-spin" />} {submitLabel}
        </button>
      </div>
    </form>
  );
}
