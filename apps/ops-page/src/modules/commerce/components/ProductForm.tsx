import { useState, useRef } from "react";
import { Loader2, UploadCloud, FileText, Send } from "lucide-react";
import { toast } from "sonner";
import { client, type components } from "../../../lib/api-client";
import { cn } from "../../../lib/utils";

type ProductDto = components["schemas"]["Commerce.ProductDto"];

interface ProductFormProps {
  initialData?: ProductDto | null;
  onSubmit: (data: any) => void;
  onCancel: () => void;
  isPending: boolean;
  submitLabel: React.ReactNode;
}

export default function ProductForm({
  initialData,
  onSubmit,
  onCancel,
  isPending,
  submitLabel
}: ProductFormProps) {
  const [name, setName] = useState(initialData?.name || "");
  const [slug, setSlug] = useState(initialData?.slug || "");
  const [price, setPrice] = useState(initialData?.price ?? 0);
  const [interval, setInterval] = useState(initialData?.interval || "one_time");

  const [reqAddress, setReqAddress] = useState(initialData?.checkout_configuration?.requires_address ?? false);
  const [reqTaxId, setReqTaxId] = useState(initialData?.checkout_configuration?.requires_tax_id ?? false);
  const [reqPhone, setReqPhone] = useState(initialData?.checkout_configuration?.requires_phone ?? false);

  const initialTargets = initialData?.fulfillment_targets || [];
  const [enableVault, setEnableVault] = useState(initialTargets.includes("internal:vault"));
  const [enableCommunity, setEnableCommunity] = useState(initialTargets.includes("internal:community"));
  const [enableWebhook, setEnableWebhook] = useState(initialTargets.some(t => t.startsWith("http")));

  const fileInputRef = useRef<HTMLInputElement>(null);
  const [isUploading, setIsUploading] = useState(false);
  const [uploadedUrl, setUploadedUrl] = useState("");

  const [telegramLink, setTelegramLink] = useState("");
  const [zoomLink, setZoomLink] = useState("");
  
  const [webhookUrl, setWebhookUrl] = useState(initialTargets.find(t => t.startsWith("http")) || "");

  const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    setIsUploading(true);

    try {
      const { data, error } = await client.POST("/admin/community/vault/presigned-url", {
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

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();

    if (enableVault && !uploadedUrl && !initialData) {
      toast.error("You must upload a file for digital product fulfillment.");
      return;
    }

    if (enableWebhook && !webhookUrl.trim().startsWith("http")) {
      toast.error("You must provide a valid HTTP/HTTPS URL for the webhook target.");
      return;
    }

    const targets: string[] = [];
    if (enableVault) targets.push("internal:vault");
    if (enableCommunity) targets.push("internal:community");
    if (enableWebhook && webhookUrl.trim()) targets.push(webhookUrl.trim());

    onSubmit({
      name: name.trim(),
      slug: slug.trim(),
      price: Number(price),
      currency: "MYR",
      interval,
      requires_address: reqAddress,
      requires_tax_id: reqTaxId,
      requires_phone: reqPhone,
      fulfillment_targets: targets,
      
      _vault_url: enableVault ? uploadedUrl : undefined,
      _community_telegram: enableCommunity ? telegramLink.trim() : undefined,
      _community_zoom: enableCommunity ? zoomLink.trim() : undefined,
    });
  };

  return (
    <form onSubmit={handleSubmit} className="flex flex-col h-full min-h-0">
      <div className="p-6 space-y-8 flex-1 overflow-y-auto bg-[#fafafa]/30">
        
        <div className="space-y-4">
          <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">1. Product Definition</label>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Name *</label>
              <input required value={name} onChange={e => setName(e.target.value)} disabled={isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50" />
            </div>
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Slug Identifier *</label>
              <input required value={slug} onChange={e => setSlug(e.target.value)} disabled={isPending} placeholder="e.g. basic-tier" className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 font-mono text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50" />
            </div>
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Price (MYR) *</label>
              <input type="number" step="0.01" required value={price} onChange={e => setPrice(Number(e.target.value))} disabled={isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50" />
            </div>
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Billing Interval</label>
              <select value={interval} onChange={e => setInterval(e.target.value)} disabled={isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50">
                <option value="one_time">One-Time Payment</option>
                <option value="mo">Monthly</option>
                <option value="yr">Yearly</option>
              </select>
            </div>
          </div>
        </div>

        <div className="space-y-4">
          <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">2. Checkout UX Toggles</label>
          <div className="flex flex-col gap-3">
            <label className="flex items-center gap-2 cursor-pointer w-fit">
              <input type="checkbox" checked={reqAddress} onChange={e => setReqAddress(e.target.checked)} disabled={isPending} className="rounded-sm border-[#e5e5e5] text-[#09090b] focus:ring-[#09090b]" />
              <span className="text-[12px] font-medium text-[#09090b]">Require Full Billing Address</span>
            </label>
            <label className="flex items-center gap-2 cursor-pointer w-fit">
              <input type="checkbox" checked={reqTaxId} onChange={e => setReqTaxId(e.target.checked)} disabled={isPending} className="rounded-sm border-[#e5e5e5] text-[#09090b] focus:ring-[#09090b]" />
              <span className="text-[12px] font-medium text-[#09090b]">Require Company Name & Tax ID (LHDN B2B)</span>
            </label>
            <label className="flex items-center gap-2 cursor-pointer w-fit">
              <input type="checkbox" checked={reqPhone} onChange={e => setReqPhone(e.target.checked)} disabled={isPending} className="rounded-sm border-[#e5e5e5] text-[#09090b] focus:ring-[#09090b]" />
              <span className="text-[12px] font-medium text-[#09090b]">Require WhatsApp Number</span>
            </label>
          </div>
        </div>

        <div className="space-y-4">
          <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">3. Post-Purchase Fulfillment</label>
          
          <div className="space-y-4">
            <div className={cn("border border-[#e5e5e5] bg-white rounded-sm overflow-hidden transition-all", enableVault && "border-[#09090b]")}>
              <div className="p-3 bg-[#fafafa] flex items-center justify-between cursor-pointer" onClick={() => setEnableVault(!enableVault)}>
                <span className="text-[12px] font-bold text-[#09090b]">📦 Deliver a Digital File (Vault)</span>
                <input type="checkbox" checked={enableVault} readOnly className="rounded-sm border-[#e5e5e5] text-[#09090b] focus:ring-[#09090b]" />
              </div>
              {enableVault && (
                <div className="p-4 border-t border-[#e5e5e5]">
                  <div className="flex flex-col gap-3 p-6 border border-dashed border-[#a1a1aa] bg-[#fafafa] rounded-sm text-center items-center justify-center relative hover:bg-[#f4f4f5] transition-colors group cursor-pointer">
                    <input type="file" ref={fileInputRef} onChange={handleFileUpload} className="absolute inset-0 w-full h-full opacity-0 cursor-pointer disabled:cursor-not-allowed" disabled={isUploading || isPending} required={!uploadedUrl && !initialData} />
                    {isUploading ? (
                      <><Loader2 className="animate-spin text-[#a1a1aa] mb-2" size={28} /><span className="text-[11px] font-medium text-[#71717a]">Uploading...</span></>
                    ) : uploadedUrl ? (
                      <><FileText className="text-emerald-600 mb-2" size={28} /><span className="text-[11px] font-medium text-emerald-700">File uploaded securely.</span></>
                    ) : (
                      <><UploadCloud className="text-[#a1a1aa] mb-2" size={28} /><span className="text-[11px] font-medium text-[#71717a]">Click to upload PDF/ZIP</span></>
                    )}
                  </div>
                </div>
              )}
            </div>

            <div className={cn("border border-[#e5e5e5] bg-white rounded-sm overflow-hidden transition-all", enableCommunity && "border-[#09090b]")}>
              <div className="p-3 bg-[#fafafa] flex items-center justify-between cursor-pointer" onClick={() => setEnableCommunity(!enableCommunity)}>
                <span className="text-[12px] font-bold text-[#09090b]">👥 Grant Private Access (Community)</span>
                <input type="checkbox" checked={enableCommunity} readOnly className="rounded-sm border-[#e5e5e5] text-[#09090b] focus:ring-[#09090b]" />
              </div>
              {enableCommunity && (
                <div className="p-4 border-t border-[#e5e5e5] grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Telegram Link</label>
                    <input type="url" value={telegramLink} onChange={e => setTelegramLink(e.target.value)} disabled={isPending} className="w-full h-9 border border-[#e5e5e5] px-3 text-[13px] focus:outline-none focus:border-[#09090b]" placeholder="https://t.me/..." />
                  </div>
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Zoom Link</label>
                    <input type="url" value={zoomLink} onChange={e => setZoomLink(e.target.value)} disabled={isPending} className="w-full h-9 border border-[#e5e5e5] px-3 text-[13px] focus:outline-none focus:border-[#09090b]" placeholder="https://zoom.us/..." />
                  </div>
                </div>
              )}
            </div>

            <div className={cn("border border-[#e5e5e5] bg-white rounded-sm overflow-hidden transition-all", enableWebhook && "border-[#09090b]")}>
              <div className="p-3 bg-[#fafafa] flex items-center justify-between cursor-pointer" onClick={() => setEnableWebhook(!enableWebhook)}>
                <span className="text-[12px] font-bold text-[#09090b]">⚡ Send Outbound Webhook (Developer SaaS)</span>
                <input type="checkbox" checked={enableWebhook} readOnly className="rounded-sm border-[#e5e5e5] text-[#09090b] focus:ring-[#09090b]" />
              </div>
              {enableWebhook && (
                <div className="p-4 border-t border-[#e5e5e5] space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Target URL</label>
                  <input type="url" required value={webhookUrl} onChange={e => setWebhookUrl(e.target.value)} disabled={isPending} className="w-full h-9 border border-[#e5e5e5] px-3 font-mono text-[13px] focus:outline-none focus:border-[#09090b]" placeholder="https://your-saas.com/api/lazuar" />
                </div>
              </div>
            )}
          </div>
        </div>

      </div>

      <div className="px-5 py-4 border-t border-[#e5e5e5] bg-[#fafafa]/50 flex justify-end gap-2 shrink-0">
        <button type="button" onClick={onCancel} disabled={isPending} className="px-4 h-8 text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:bg-[#e5e5e5] hover:text-[#09090b] border border-[#e5e5e5] bg-white transition-colors disabled:opacity-50 rounded-sm">Cancel</button>
        <button type="submit" disabled={isPending} className="px-6 h-8 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest hover:bg-[#27272a] disabled:opacity-50 flex items-center gap-1.5 rounded-sm">
          {isPending && <Loader2 size={13} className="animate-spin" />} {submitLabel}
        </button>
      </div>
    </form>
  );
}
