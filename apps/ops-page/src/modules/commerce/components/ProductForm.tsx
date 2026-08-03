import { useState, useEffect } from "react";
import { Loader2 } from "lucide-react";
import { useQuery } from "@tanstack/react-query";
import { client, type components } from "../../../lib/api-client";
import { filterHiddenFulfillmentTargets, cn } from "../../../lib/utils";

type ProductDto = components["schemas"]["Commerce.ProductDto"];
type PaymentConfigDto = components["schemas"]["Commerce.PaymentConfigDto"];

interface ProductFormProps {
  initialData?: ProductDto | null;
  onSubmit: (data: any) => void;
  onCancel: () => void;
  isPending: boolean;
  submitLabel: React.ReactNode;
  hasValidEmailConfig: boolean;
}

export default function ProductForm({
  initialData,
  onSubmit,
  onCancel,
  isPending,
  submitLabel,
  hasValidEmailConfig
}: ProductFormProps) {
  const { data: configuredGateways } = useQuery<PaymentConfigDto[]>({
    queryKey: ["configured-gateways"],
    queryFn: async () => {
      const { data, error } = await client.GET("/admin/commerce/payment-config");
      if (error) throw new Error(error.detail);
      return data.filter(c => c.api_key || c.secret_key); // Only return gateways with saved credentials
    }
  });

  const [name, setName] = useState(initialData?.name || "");
  const [slug, setSlug] = useState(initialData?.slug || "");
  const [pricingModel, setPricingModel] = useState(initialData?.pricing_model || "FIXED");
  const [price, setPrice] = useState(initialData?.price ?? 0);
  const [minimumPrice, setMinimumPrice] = useState(initialData?.minimum_price ?? 0);
  const [interval, setInterval] = useState(initialData?.interval || "one_time");
  const [gatewayName, setGatewayName] = useState(initialData?.gateway_name || "");

  const [reqAddress, setReqAddress] = useState(initialData?.checkout_configuration?.requires_address ?? false);
  const [reqPhone, setReqPhone] = useState(initialData?.checkout_configuration?.requires_phone ?? false);

  const [isActive, setIsActive] = useState(initialData?.is_active ?? hasValidEmailConfig);

  const [webhooksText, setWebhooksText] = useState(() => 
    filterHiddenFulfillmentTargets(initialData?.fulfillment_targets).join("\n")
  );

  useEffect(() => {
    if (!gatewayName && configuredGateways && configuredGateways.length > 0) {
      setGatewayName(configuredGateways[0].gateway_type);
    }
  }, [configuredGateways, gatewayName]);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();

    const targets = filterHiddenFulfillmentTargets(
      webhooksText
        .split("\n")
        .map(url => url.trim())
        .filter(url => url.length > 0)
    );

    onSubmit({
      name: name.trim(),
      slug: slug.trim(),
      price: Number(price),
      pricing_model: pricingModel,
      minimum_price: Number(minimumPrice),
      currency: "MYR",
      interval,
      gateway_name: gatewayName,
      is_active: isActive,
      requires_address: reqAddress,
      requires_tax_id: false,
      requires_phone: reqPhone,
      fulfillment_targets: targets, 
    });
  };

  return (
    <form onSubmit={handleSubmit} className="flex flex-col h-full min-h-0">
      <div className="p-6 space-y-8 flex-1 overflow-y-auto bg-[#fafafa]/30">
        
        <div className="space-y-4">
          <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">1. Checkout Configuration</label>
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
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Pricing Model</label>
              <select value={pricingModel} onChange={e => setPricingModel(e.target.value)} disabled={isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50">
                <option value="FIXED">Fixed Price</option>
                <option value="PWYW">Pay What You Want (PWYW)</option>
              </select>
            </div>
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Billing Interval</label>
              <select value={interval} onChange={e => setInterval(e.target.value)} disabled={isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50">
                <option value="one_time">One-Time Payment</option>
                <option value="mo">Monthly</option>
                <option value="yr">Yearly</option>
              </select>
            </div>
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">{pricingModel === "PWYW" ? "Recommended Price (MYR) *" : "Price (MYR) *"}</label>
              <input type="number" step="0.01" required value={price} onChange={e => setPrice(Number(e.target.value))} disabled={isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50" />
            </div>
            {pricingModel === "PWYW" && (
              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Minimum Price (MYR) *</label>
                <input type="number" step="0.01" required value={minimumPrice} onChange={e => setMinimumPrice(Number(e.target.value))} disabled={isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50" />
              </div>
            )}
            <div className="space-y-1.5 sm:col-span-2">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Payment Gateway *</label>
              <select required value={gatewayName} onChange={e => setGatewayName(e.target.value)} disabled={isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50">
                <option value="" disabled>Select a configured gateway...</option>
                {configuredGateways?.map(g => (
                  <option key={g.gateway_type} value={g.gateway_type}>{g.gateway_type}</option>
                ))}
              </select>
              {configuredGateways?.length === 0 && <p className="text-[10px] text-rose-600">No gateways configured in Workspace Settings.</p>}
              {gatewayName === "BILLPLZ" && (
                <p className="text-[10px] text-amber-700 bg-amber-50 border border-amber-200 rounded-sm px-2 py-1.5 mt-1.5 leading-relaxed">
                  Billplz is online checkout only — it cannot vault cards or run silent auto-charge / dunning retries. Use Stripe or CHIP Collect for subscription auto-renewal and AUTO_CHARGE dunning steps.
                </p>
              )}
            </div>
          </div>
        </div>

        <div className="space-y-4">
          <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">2. Product Status</label>
          <div className="flex flex-col gap-3">
            <label className={cn("flex items-center gap-2 w-fit", hasValidEmailConfig ? "cursor-pointer" : "cursor-not-allowed opacity-60")}>
              <input 
                type="checkbox" 
                checked={isActive} 
                onChange={e => setIsActive(e.target.checked)} 
                disabled={isPending || !hasValidEmailConfig} 
                className="rounded-sm border-[#e5e5e5] text-[#09090b] focus:ring-[#09090b] disabled:opacity-50" 
              />
              <span className="text-[12px] font-medium text-[#09090b]">Active (Visible at Checkout)</span>
            </label>
            {!hasValidEmailConfig && (
              <p className="text-[10px] text-rose-600">
                You must configure a valid Resend API key before activating checkout links.
              </p>
            )}
          </div>
        </div>

        <div className="space-y-4">
          <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">3. Checkout UX Constraints</label>
          <div className="flex flex-col gap-3">
            <label className="flex items-center gap-2 cursor-pointer w-fit">
              <input type="checkbox" checked={reqAddress} onChange={e => setReqAddress(e.target.checked)} disabled={isPending} className="rounded-sm border-[#e5e5e5] text-[#09090b] focus:ring-[#09090b]" />
              <span className="text-[12px] font-medium text-[#09090b]">Require Full Billing Address</span>
            </label>
            <label className="flex items-center gap-2 cursor-pointer w-fit">
              <input type="checkbox" checked={reqPhone} onChange={e => setReqPhone(e.target.checked)} disabled={isPending} className="rounded-sm border-[#e5e5e5] text-[#09090b] focus:ring-[#09090b]" />
              <span className="text-[12px] font-medium text-[#09090b]">Require WhatsApp Number</span>
            </label>
          </div>
        </div>

        <div className="space-y-4">
          <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">4. Post-Purchase Webhooks (Optional)</label>
          <div className="space-y-1.5">
            <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">External SaaS Integrations</label>
            <textarea 
              value={webhooksText} 
              onChange={e => setWebhooksText(e.target.value)} 
              disabled={isPending} 
              rows={3}
              placeholder="https://hooks.zapier.com/..." 
              className="flex w-full rounded-sm border border-[#e5e5e5] bg-white p-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50 font-mono resize-y" 
            />
            <p className="text-[10px] text-[#71717a]">Enter one URL per line. We will send an HTTP POST request to these URLs when this specific product is purchased.</p>
          </div>
        </div>

      </div>

      <div className="px-5 py-4 border-t border-[#e5e5e5] bg-[#fafafa]/50 flex justify-end gap-2 shrink-0">
        <button type="button" onClick={onCancel} disabled={isPending} className="px-4 h-8 text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:bg-[#e5e5e5] hover:text-[#09090b] border border-[#e5e5e5] bg-white transition-colors disabled:opacity-50 rounded-sm">Cancel</button>
        <button type="submit" disabled={isPending || !gatewayName} className="px-6 h-8 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest hover:bg-[#27272a] disabled:opacity-50 flex items-center gap-1.5 rounded-sm">
          {isPending && <Loader2 size={13} className="animate-spin" />} {submitLabel}
        </button>
      </div>
    </form>
  );
}
