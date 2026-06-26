// apps/ops-page/src/modules/vault/components/DigitalProductForm.tsx
import { useState, useRef, useEffect } from "react";
import { Loader2, UploadCloud, FileText } from "lucide-react";
import { toast } from "sonner";
import { useOutletContext } from "react-router-dom";
import { API_URL, type components } from "../../../lib/api-client";

type CommunityPlanDto = components["schemas"]["Community.CommunityPlanDto"];

interface DigitalProductFormProps {
  initialData?: CommunityPlanDto | null;
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
  const { activeWorkspaceId } = useOutletContext<{ activeWorkspaceId: string | null }>();
  
  const fileInputRef = useRef<HTMLInputElement>(null);
  const [isUploading, setIsUploading] = useState(false);
  const [uploadedUrl, setUploadedUrl] = useState("");

  const [name, setName] = useState("");
  const [slug, setSlug] = useState("");
  const [price, setPrice] = useState(0);
  const [audience, setAudience] = useState("General");
  const [displayOrder, setDisplayOrder] = useState(1);
  const [adminNotes, setAdminNotes] = useState("");

  useEffect(() => {
    if (initialData) {
      setName(initialData.name);
      setSlug(initialData.slug);
      setPrice(initialData.price);
      setAudience(initialData.audience);
      setDisplayOrder(initialData.display_order);
      setAdminNotes(initialData.admin_notes || "");
      setUploadedUrl(initialData.fulfillment_file_url || "");
    }
  }, [initialData]);

  const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setIsUploading(true);
    const formData = new FormData();
    formData.append("file", file);

    try {
      const response = await fetch(`${API_URL}/admin/community/vault/upload`, {
        method: "POST",
        body: formData,
        credentials: "include", // Required to send the auth cookie
        headers: {
          "X-Tenant-Id": activeWorkspaceId || ""
        }
      });

      if (!response.ok) throw new Error("File upload failed.");
      
      const result = await response.json();
      setUploadedUrl(result.url);
      toast.success("File uploaded successfully to Cloudflare R2.");
    } catch (err: any) {
      toast.error(err.message);
    } finally {
      setIsUploading(false);
    }
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!uploadedUrl) {
      toast.error("You must upload a file for digital products.");
      return;
    }

    onSubmit({
      name: name.trim(),
      slug: slug.trim(),
      price: Number(price),
      interval: "one_time",
      pricing_model: "FLAT_RATE",
      audience: audience.trim(),
      grace_period_days: 0,
      display_order: Number(displayOrder),
      admin_notes: adminNotes.trim() || undefined,
      product_type: "VAULT",
      fulfillment_file_url: uploadedUrl
    });
  };

  return (
    <form onSubmit={handleSubmit} className="flex flex-col h-full">
      <div className="p-6 space-y-6 flex-1 overflow-y-auto">
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
                <span className="text-[11px] font-medium text-[#71717a]">Uploading to secure storage...</span>
              </>
            ) : uploadedUrl ? (
              <>
                <FileText className="text-emerald-600 mb-2" size={28} />
                <span className="text-[11px] font-medium text-emerald-700">File uploaded successfully. Click to replace.</span>
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
          <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">Product Details</label>
          <div className="space-y-1.5">
            <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Name *</label>
            <input required value={name} onChange={e => setName(e.target.value)} disabled={isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
          </div>
          <div className="space-y-1.5">
            <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Slug Identifier *</label>
            <input required value={slug} onChange={e => setSlug(e.target.value)} disabled={isPending} placeholder="e.g. startup-guide-pdf" className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 font-mono text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
          </div>
          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Price (MYR) *</label>
              <input type="number" step="0.01" required value={price} onChange={e => setPrice(Number(e.target.value))} disabled={isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
            </div>
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Audience *</label>
              <input required value={audience} onChange={e => setAudience(e.target.value)} disabled={isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
            </div>
          </div>
        </div>

        <div className="space-y-4">
          <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">Internal Operations</label>
          <div className="space-y-1.5">
            <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Display Order</label>
            <input type="number" required value={displayOrder} onChange={e => setDisplayOrder(Number(e.target.value))} disabled={isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
          </div>
          <div className="space-y-1.5">
            <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Admin Notes</label>
            <textarea value={adminNotes} onChange={e => setAdminNotes(e.target.value)} disabled={isPending} rows={3} className="w-full rounded-sm border border-[#e5e5e5] bg-white p-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] resize-y disabled:opacity-50" />
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
