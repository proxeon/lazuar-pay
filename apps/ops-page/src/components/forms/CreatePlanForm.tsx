// apps/ops-page/src/components/forms/CreatePlanForm.tsx
import { useState } from "react";
import { Send } from "lucide-react";
import type { CustomFormProps } from "./types";

export default function CreatePlanForm({ prefillData, onSubmit, onCancel }: CustomFormProps) {
  const [name, setName] = useState(prefillData?.name || "");
  const [slug, setSlug] = useState(prefillData?.slug || "");
  const [price, setPrice] = useState(prefillData?.price ?? 0);
  const [interval, setInterval] = useState(prefillData?.interval || "mo");
  const [audience, setAudience] = useState(prefillData?.audience || "General");

  const [telegramLink, setTelegramLink] = useState(prefillData?.telegram_invite_link || "");
  const [meetingLink, setMeetingLink] = useState(prefillData?.weekly_meeting_link || "");
  const [gracePeriod, setGracePeriod] = useState(prefillData?.grace_period_days ?? 3);

  const [maxCapacity, setMaxCapacity] = useState(prefillData?.max_capacity?.toString() || "");
  const [displayOrder, setDisplayOrder] = useState(prefillData?.display_order ?? 1);

  const [adminNotes, setAdminNotes] = useState(prefillData?.admin_notes || "");

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSubmit({
      name: name.trim(),
      slug: slug.trim(),
      price: Number(price),
      interval,
      audience: audience.trim(),
      grace_period_days: Number(gracePeriod),
      display_order: Number(displayOrder),
      max_capacity: maxCapacity.trim() !== "" ? Number(maxCapacity) : null,
      telegram_invite_link: telegramLink.trim() || null,
      weekly_meeting_link: meetingLink.trim() || null,
      admin_notes: adminNotes.trim() || null
    });
  };

  return (
    <form onSubmit={handleSubmit} className="flex flex-col font-sans">
      <div className="p-5 space-y-8">
        
        <div className="space-y-4">
          <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1.5">1. Basic Details</h4>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Plan Name *</label>
              <input required value={name} onChange={e => setName(e.target.value)} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b]" />
            </div>
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Slug Identifier *</label>
              <input required value={slug} onChange={e => setSlug(e.target.value)} placeholder="e.g. basic-tier" className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b] font-mono" />
            </div>
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Price (MYR) *</label>
              <input type="number" step="0.01" required value={price} onChange={e => setPrice(e.target.value)} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b]" />
            </div>
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Billing Interval</label>
              <select value={interval} onChange={e => setInterval(e.target.value)} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b]">
                <option value="mo">Monthly</option>
                <option value="yr">Yearly</option>
              </select>
            </div>
            <div className="space-y-1.5 sm:col-span-2">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Target Audience *</label>
              <input required value={audience} onChange={e => setAudience(e.target.value)} placeholder="e.g. Beginners, Founders" className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b]" />
            </div>
          </div>
        </div>

        <div className="space-y-4">
          <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1.5">2. Fulfillment & Access</h4>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Telegram/Group Link</label>
              <input type="url" value={telegramLink} onChange={e => setTelegramLink(e.target.value)} placeholder="https://t.me/..." className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b]" />
            </div>
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Weekly Meeting Link</label>
              <input type="url" value={meetingLink} onChange={e => setMeetingLink(e.target.value)} placeholder="https://zoom.us/..." className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b]" />
            </div>
            <div className="space-y-1.5 sm:col-span-2">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Grace Period (Days) *</label>
              <input type="number" required min="0" value={gracePeriod} onChange={e => setGracePeriod(Number(e.target.value))} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b]" />
              <p className="text-[10px] text-[#a1a1aa] mt-1">Days a user retains access while their payment is past due.</p>
            </div>
          </div>
        </div>

        <div className="space-y-4">
          <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1.5">3. Capacity Limits</h4>
          <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Max Capacity (Spots)</label>
              <input type="number" min="1" value={maxCapacity} onChange={e => setMaxCapacity(e.target.value)} placeholder="Leave empty for unlimited" className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b]" />
            </div>
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Display Order *</label>
              <input type="number" required value={displayOrder} onChange={e => setDisplayOrder(Number(e.target.value))} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b]" />
            </div>
          </div>
        </div>

        <div className="space-y-4">
          <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1.5">4. Internal Operations</h4>
          <div className="space-y-1.5">
            <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Admin Notes</label>
            <textarea value={adminNotes} onChange={e => setAdminNotes(e.target.value)} rows={3} placeholder="Private notes about this plan version..." className="w-full rounded-sm border border-[#e5e5e5] bg-white p-3 text-[13px] focus:outline-none focus:border-[#09090b] resize-y" />
          </div>
        </div>

      </div>

      <div className="px-5 py-3 border-t border-[#e5e5e5] bg-[#fafafa] flex items-center justify-end gap-2.5 shrink-0">
        <button type="button" onClick={onCancel} className="h-8 px-4 rounded-sm border border-[#e5e5e5] bg-white text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:bg-[#f4f4f5] hover:text-[#09090b] transition-colors">
          Cancel
        </button>
        <button type="submit" className="h-8 px-6 rounded-sm bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest flex items-center justify-center gap-2 hover:bg-[#27272a] transition-colors">
          <Send size={13} /> Save Plan
        </button>
      </div>
    </form>
  );
}
