import { useState } from "react";
import { Send } from "lucide-react";
import type { CustomFormProps } from "./types";

export default function CreateProductForm({ prefillData, onSubmit, onCancel }: CustomFormProps) {
  const [name, setName] = useState(prefillData?.name || "");
  const [slug, setSlug] = useState(prefillData?.slug || "");
  const [pricingModel, setPricingModel] = useState(prefillData?.pricingModel || "FIXED");
  const [price, setPrice] = useState(prefillData?.price ?? 0);
  const [minimumPrice, setMinimumPrice] = useState(prefillData?.minimumPrice ?? 0);
  const [interval, setInterval] = useState(prefillData?.interval || "one_time");

  const [requiresAddress, setRequiresAddress] = useState(prefillData?.requiresAddress ?? false);
  const [requiresTaxId, setRequiresTaxId] = useState(prefillData?.requiresTaxId ?? false);
  const [requiresPhone, setRequiresPhone] = useState(prefillData?.requiresPhone ?? false);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSubmit({
      name: name.trim(),
      slug: slug.trim(),
      price: Number(price),
      pricingModel,
      minimumPrice: Number(minimumPrice),
      currency: "MYR",
      interval,
      requiresAddress,
      requiresTaxId,
      requiresPhone,
      fulfillmentTargets: []
    });
  };

  return (
    <form onSubmit={handleSubmit} className="flex flex-col font-sans">
      <div className="p-5 space-y-8 max-h-[65vh] overflow-y-auto bg-[#fafafa]/30">
        
        <div className="space-y-4">
          <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1.5">1. Basic Details</h4>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Product Name *</label>
              <input required value={name} onChange={e => setName(e.target.value)} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b]" />
            </div>
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Slug Identifier *</label>
              <input required value={slug} onChange={e => setSlug(e.target.value)} placeholder="e.g. basic-tier" className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b] font-mono" />
            </div>
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Pricing Model</label>
              <select value={pricingModel} onChange={e => setPricingModel(e.target.value)} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b]">
                <option value="FIXED">Fixed Price</option>
                <option value="PWYW">Pay What You Want (PWYW)</option>
              </select>
            </div>
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Billing Interval</label>
              <select value={interval} onChange={e => setInterval(e.target.value)} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b]">
                <option value="one_time">One-Time Payment</option>
                <option value="mo">Monthly</option>
                <option value="yr">Yearly</option>
              </select>
            </div>
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">{pricingModel === "PWYW" ? "Recommended Price (MYR) *" : "Price (MYR) *"}</label>
              <input type="number" step="0.01" required value={price} onChange={e => setPrice(Number(e.target.value))} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b]" />
            </div>
            {pricingModel === "PWYW" && (
              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Minimum Price (MYR) *</label>
                <input type="number" step="0.01" required value={minimumPrice} onChange={e => setMinimumPrice(Number(e.target.value))} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b]" />
              </div>
            )}
          </div>
        </div>

        <div className="space-y-4">
          <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1.5">2. Checkout UX Constraints</h4>
          <div className="flex flex-col gap-3">
            <label className="flex items-center gap-2 cursor-pointer w-fit">
              <input type="checkbox" checked={requiresAddress} onChange={e => setRequiresAddress(e.target.checked)} className="rounded-sm border-[#e5e5e5] text-[#09090b] focus:ring-[#09090b]" />
              <span className="text-[12px] font-medium text-[#09090b]">Require Full Billing Address</span>
            </label>
            <label className="flex items-start gap-2 cursor-pointer w-fit">
              <input type="checkbox" checked={requiresTaxId} onChange={e => setRequiresTaxId(e.target.checked)} className="rounded-sm border-[#e5e5e5] text-[#09090b] focus:ring-[#09090b] mt-0.5" />
              <span>
                <span className="text-[12px] font-medium text-[#09090b] block">Require Company Name &amp; Tax ID (LHDN B2B)</span>
                <span className="text-[11px] text-[#71717a] block mt-0.5">Collects buyer company + TIN. Checkout validates the TIN against MyInvois when LHDN is connected.</span>
              </span>
            </label>
            <label className="flex items-center gap-2 cursor-pointer w-fit">
              <input type="checkbox" checked={requiresPhone} onChange={e => setRequiresPhone(e.target.checked)} className="rounded-sm border-[#e5e5e5] text-[#09090b] focus:ring-[#09090b]" />
              <span className="text-[12px] font-medium text-[#09090b]">Require WhatsApp Number</span>
            </label>
          </div>
        </div>

      </div>

      <div className="px-5 py-3 border-t border-[#e5e5e5] bg-[#fafafa] flex items-center justify-end gap-2.5 shrink-0 mt-auto">
        <button type="button" onClick={onCancel} className="h-8 px-4 rounded-sm border border-[#e5e5e5] bg-white text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:bg-[#f4f4f5] hover:text-[#09090b] transition-colors">
          Cancel
        </button>
        <button type="submit" className="h-8 px-6 rounded-sm bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest flex items-center justify-center gap-2 hover:bg-[#27272a] transition-colors">
          <Send size={13} /> Save Product
        </button>
      </div>
    </form>
  );
}
