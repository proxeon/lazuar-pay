import { useState, useEffect } from "react";
import { Send } from "lucide-react";
import { useQuery } from "@tanstack/react-query";
import { client, type components } from "../../../lib/api-client";
import { gatewaySupportsOffSession } from "../../../lib/utils";
import type { CustomFormProps } from "./types";

type PaymentConfigDto = components["schemas"]["Commerce.PaymentConfigDto"];

export default function CreateProductForm({ prefillData, onSubmit, onCancel }: CustomFormProps) {
  const { data: configuredGateways } = useQuery<PaymentConfigDto[]>({
    queryKey: ["configured-gateways"],
    queryFn: async () => {
      const { data, error } = await client.GET("/admin/commerce/payment-config");
      if (error) throw new Error(error.detail);
      return data.filter(c => c.is_active && (c.has_api_key || c.has_secret_key)); 
    }
  });

  const [name, setName] = useState(prefillData?.name || "");
  const [slug, setSlug] = useState(prefillData?.slug || "");
  const [pricingModel, setPricingModel] = useState(prefillData?.pricingModel || "FIXED");
  const [price, setPrice] = useState(prefillData?.price ?? 0);
  const [minimumPrice, setMinimumPrice] = useState(prefillData?.minimumPrice ?? 0);
  const [interval, setInterval] = useState(prefillData?.interval || "one_time");
  const [gatewayName, setGatewayName] = useState(prefillData?.gatewayName || "");

  const [requiresAddress, setRequiresAddress] = useState(prefillData?.requiresAddress ?? false);
  const [requiresTaxId, setRequiresTaxId] = useState(prefillData?.requiresTaxId ?? false);
  const [requiresPhone, setRequiresPhone] = useState(prefillData?.requiresPhone ?? false);

  useEffect(() => {
    if (!gatewayName && configuredGateways && configuredGateways.length > 0) {
      setGatewayName(configuredGateways[0].gateway_type);
    }
  }, [configuredGateways, gatewayName]);

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
      gatewayName,
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
            <div className="space-y-1.5 sm:col-span-2">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Payment Gateway *</label>
              <select required value={gatewayName} onChange={e => setGatewayName(e.target.value)} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b]">
                <option value="" disabled>Select a configured gateway...</option>
                {configuredGateways?.map(g => (
                  <option key={g.gateway_type} value={g.gateway_type}>{g.gateway_type}</option>
                ))}
              </select>
              {configuredGateways?.length === 0 && <p className="text-[10px] text-rose-600">No gateways configured in Workspace Settings.</p>}
              {gatewayName && interval !== "one_time" && !gatewaySupportsOffSession(gatewayName) && (
                <p className="text-[10px] text-amber-800 bg-amber-50 border border-amber-200 rounded-sm px-2 py-1.5 mt-1.5 leading-relaxed">
                  <strong>Collection mode: pay link each cycle.</strong> We email a hosted Billplz/CHIP/Stripe page every period. No card is stored. AUTO_CHARGE will not run.
                </p>
              )}
              {gatewayName && interval !== "one_time" && gatewaySupportsOffSession(gatewayName) && (
                <p className="text-[10px] text-emerald-800 bg-emerald-50 border border-emerald-200 rounded-sm px-2 py-1.5 mt-1.5 leading-relaxed">
                  <strong>Auto-debit:</strong> card is saved for renewals.
                </p>
              )}
              {gatewayName && interval === "one_time" && !gatewaySupportsOffSession(gatewayName) && (
                <p className="text-[10px] text-amber-700 bg-amber-50 border border-amber-200 rounded-sm px-2 py-1.5 mt-1.5 leading-relaxed">
                  Hosted checkout only — this gateway cannot vault a card.
                </p>
              )}
            </div>
          </div>
        </div>

        <div className="space-y-4">
          <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1.5">2. Checkout UX Constraints</h4>
          <div className="flex flex-col gap-3">
            <label className="flex items-center gap-2 cursor-pointer w-fit">
              <input type="checkbox" checked={requiresAddress} onChange={e => setRequiresAddress(e.target.checked)} className="rounded-sm border-[#e5e5e5] text-[#09090b] focus:ring-[#09090b]" />
              <span className="text-[12px] font-medium text-[#09090b]">Require Full Billing Address</span>
            </label>
            <label className="flex items-center gap-2 cursor-pointer w-fit">
              <input type="checkbox" checked={requiresTaxId} onChange={e => setRequiresTaxId(e.target.checked)} className="rounded-sm border-[#e5e5e5] text-[#09090b] focus:ring-[#09090b]" />
              <span className="text-[12px] font-medium text-[#09090b]">Require Company Name &amp; Tax ID (LHDN B2B)</span>
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
        <button type="submit" disabled={!gatewayName} className="h-8 px-6 rounded-sm bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest flex items-center justify-center gap-2 hover:bg-[#27272a] transition-colors disabled:opacity-50">
          <Send size={13} /> Save Product
        </button>
      </div>
    </form>
  );
}
