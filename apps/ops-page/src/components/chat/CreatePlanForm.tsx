// apps/ops-page/src/components/chat/CreatePlanForm.tsx
import { useState } from "react";
import { Send } from "lucide-react";
import { CustomFormProps } from "./FormRegistry";

export default function CreatePlanForm({ prefillData, onSubmit, onCancel }: CustomFormProps) {
  const [name, setName] = useState(prefillData?.name || "");
  const [slug, setSlug] = useState(prefillData?.slug || "");
  const [price, setPrice] = useState(prefillData?.price || 0);
  const [interval, setInterval] = useState(prefillData?.interval || "mo");

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSubmit({
      name,
      slug,
      price: Number(price),
      interval,
      audience: prefillData?.audience || "General",
      short_description: prefillData?.short_description || "Custom Plan",
      long_description: prefillData?.long_description || "Created via UI",
      grace_period_days: prefillData?.grace_period_days || 3,
      display_order: prefillData?.display_order || 1,
      features: prefillData?.features || [],
      faq: prefillData?.faq || [],
      methodology: prefillData?.methodology || "Standard"
    });
  };

  return (
    <form onSubmit={handleSubmit} className="flex flex-col font-sans">
      <div className="p-4 bg-blue-50 border-b border-blue-100 text-[12px] text-blue-800">
        <strong>Custom Registry Active:</strong> This UI is intercepting the AutoForm to provide a tailored plan creation layout.
      </div>
      
      <div className="p-4 space-y-4">
        <div className="grid grid-cols-2 gap-4">
          <div className="space-y-1.5">
            <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Plan Name *</label>
            <input required value={name} onChange={e => setName(e.target.value)} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b]" />
          </div>
          <div className="space-y-1.5">
            <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Slug Identifier *</label>
            <input required value={slug} onChange={e => setSlug(e.target.value)} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b]" />
          </div>
        </div>

        <div className="grid grid-cols-2 gap-4">
          <div className="space-y-1.5">
            <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Price (MYR) *</label>
            <input type="number" step="0.01" required value={price} onChange={e => setPrice(e.target.value)} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b]" />
          </div>
          <div className="space-y-1.5">
            <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Billing Interval</label>
            <select value={interval} onChange={e => setInterval(e.target.value)} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b]">
              <option value="mo">Monthly</option>
              <option value="yr">Yearly</option>
            </select>
          </div>
        </div>
      </div>

      <div className="px-4 py-3 border-t border-[#f4f4f5] bg-[#fafafa]/50 flex items-center justify-end gap-2.5 mt-2">
        <button type="button" onClick={onCancel} className="h-8 px-4 rounded-sm border border-[#e5e5e5] bg-white text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:bg-[#f4f4f5] hover:text-[#09090b] transition-colors">
          Cancel
        </button>
        <button type="submit" className="h-8 px-6 rounded-sm bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest flex items-center justify-center gap-2 hover:bg-[#27272a] transition-colors">
          <Send size={13} /> Create Plan
        </button>
      </div>
    </form>
  );
}
