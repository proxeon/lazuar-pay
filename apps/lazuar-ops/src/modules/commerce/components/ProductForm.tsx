import { useState, useEffect } from "react";
import { Loader2 } from "lucide-react";
import { useQuery } from "@tanstack/react-query";
import { client, type components } from "../../../lib/api-client";
import { filterHiddenFulfillmentTargets, cn, gatewaySupportsOffSession } from "../../../lib/utils";

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
      return data.filter(c => c.is_active && (c.has_api_key || c.has_secret_key)); // Active gateways with saved credentials
    }
  });

  const [name, setName] = useState(initialData?.name || "");
  const [slug, setSlug] = useState(initialData?.slug || "");
  const [pricingModel, setPricingModel] = useState(initialData?.pricing_model || "FIXED");
  const [price, setPrice] = useState(initialData?.price ?? 0);
  const [minimumPrice, setMinimumPrice] = useState(initialData?.minimum_price ?? 0);
  const [yearlyPrice, setYearlyPrice] = useState<number>(
    (initialData?.prices ?? []).find((p) => p.interval === "yr" && !p.is_default)?.amount
      ?? (initialData?.interval === "yr" ? 0 : 0)
  );
  const [trialDays, setTrialDays] = useState(initialData?.trial_days ?? 0);
  const [interval, setInterval] = useState(initialData?.interval || "one_time");
  const [gatewayName, setGatewayName] = useState(initialData?.gateway_name || "");

  const [reqAddress, setReqAddress] = useState(initialData?.checkout_configuration?.requires_address ?? false);
  const [reqTaxId, setReqTaxId] = useState(initialData?.checkout_configuration?.requires_tax_id ?? false);
  const [reqPhone, setReqPhone] = useState(initialData?.checkout_configuration?.requires_phone ?? false);
  const [sstType, setSstType] = useState(initialData?.sst_tax_type || "06");
  const [sstRate, setSstRate] = useState(initialData?.sst_rate_percent ?? 0);

  const { data: billingProfile } = useQuery({
    queryKey: ["billing-profile"],
    queryFn: async () => {
      const { data, error, response } = await client.GET("/admin/billing/profile");
      if (response.status === 404) return null;
      if (error) throw new Error(error.detail);
      return data;
    }
  });
  const hasSst = !!billingProfile?.sst_registration_number;

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
      requires_tax_id: reqTaxId,
      requires_phone: reqPhone,
      sst_tax_type: hasSst ? sstType : "06",
      sst_rate_percent: hasSst && sstType === "02" ? Number(sstRate) : 0,
      trial_days: interval === "one_time" ? 0 : Number(trialDays) || 0,
      yearly_price: interval === "mo" && Number(yearlyPrice) > 0 ? Number(yearlyPrice) : undefined,
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
            {interval === "mo" && (
              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Yearly price (MYR, optional)</label>
                <input type="number" step="0.01" min={0} value={yearlyPrice} onChange={e => setYearlyPrice(Number(e.target.value))} disabled={isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50" />
                <p className="text-[10px] text-[#71717a]">Buyers can pick monthly or yearly on the same checkout link.</p>
              </div>
            )}
            {interval !== "one_time" && (
              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Trial days (0 = off)</label>
                <input type="number" min={0} max={90} value={trialDays} onChange={e => setTrialDays(Number(e.target.value))} disabled={isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50" />
              </div>
            )}
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
              {gatewayName && interval !== "one_time" && !gatewaySupportsOffSession(gatewayName, initialData?.supports_off_session) && (
                <p className="text-[10px] text-amber-800 bg-amber-50 border border-amber-200 rounded-sm px-2 py-1.5 mt-1.5 leading-relaxed">
                  <strong>Collection mode: pay link each cycle.</strong> We email a hosted Billplz/CHIP/Stripe page every period. No card is stored. AUTO_CHARGE will not run.
                </p>
              )}
              {gatewayName && interval !== "one_time" && gatewaySupportsOffSession(gatewayName, initialData?.supports_off_session) && (
                <p className="text-[10px] text-emerald-800 bg-emerald-50 border border-emerald-200 rounded-sm px-2 py-1.5 mt-1.5 leading-relaxed">
                  <strong>Auto-debit:</strong> card is saved for renewals.
                </p>
              )}
              {gatewayName && interval === "one_time" && !gatewaySupportsOffSession(gatewayName, initialData?.supports_off_session) && (
                <p className="text-[10px] text-amber-700 bg-amber-50 border border-amber-200 rounded-sm px-2 py-1.5 mt-1.5 leading-relaxed">
                  Hosted checkout only — this gateway cannot vault a card.
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
            <label className="flex items-start gap-2 cursor-pointer w-fit">
              <input type="checkbox" checked={reqTaxId} onChange={e => setReqTaxId(e.target.checked)} disabled={isPending} className="rounded-sm border-[#e5e5e5] text-[#09090b] focus:ring-[#09090b] mt-0.5" />
              <span>
                <span className="text-[12px] font-medium text-[#09090b] block">Require Company Name &amp; Tax ID (LHDN B2B)</span>
                <span className="text-[11px] text-[#71717a] block mt-0.5">Collects buyer company + TIN. Checkout validates the TIN against MyInvois when LHDN is connected.</span>
              </span>
            </label>
            <label className="flex items-center gap-2 cursor-pointer w-fit">
              <input type="checkbox" checked={reqPhone} onChange={e => setReqPhone(e.target.checked)} disabled={isPending} className="rounded-sm border-[#e5e5e5] text-[#09090b] focus:ring-[#09090b]" />
              <span className="text-[12px] font-medium text-[#09090b]">Require WhatsApp Number</span>
            </label>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3 pt-2">
              <label className="space-y-1.5">
                <span className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">SST on this product</span>
                <select
                  value={hasSst ? sstType : "06"}
                  onChange={e => setSstType(e.target.value)}
                  disabled={isPending || !hasSst}
                  className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50"
                >
                  <option value="06">06 — Not applicable</option>
                  <option value="02">02 — Service Tax</option>
                </select>
              </label>
              <label className="space-y-1.5">
                <span className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">SST rate %</span>
                <input
                  type="number"
                  step="0.01"
                  min={0}
                  value={sstRate}
                  onChange={e => setSstRate(Number(e.target.value))}
                  disabled={isPending || !hasSst || sstType !== "02"}
                  className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50"
                />
              </label>
            </div>
            {!hasSst && (
              <p className="text-[11px] text-[#71717a]">Add an SST registration number on Legal &amp; Billing before marking service tax.</p>
            )}
          </div>
        </div>

        <div className="space-y-4">
          <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">4. Fulfillment targets (optional)</label>
          <div className="space-y-2">
            <div className="rounded-sm border border-[#e5e5e5] bg-[#fafafa] px-3 py-2.5 space-y-1">
              <p className="text-[12px] text-[#09090b] font-medium leading-relaxed">
                Signed outbound events (HMAC, multi-endpoint, delivery logs) are configured under{" "}
                <span className="font-semibold">Developer → Outbound Webhooks</span> — not on this product form.
              </p>
              <p className="text-[11px] text-[#71717a] leading-relaxed">
                Workspace webhooks receive commerce lifecycle events for the whole workspace. Product URLs below no longer gate those deliveries.
              </p>
            </div>
            <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Legacy / internal targets</label>
            <textarea
              value={webhooksText}
              onChange={e => setWebhooksText(e.target.value)}
              disabled={isPending}
              rows={3}
              placeholder={"internal:your-app\n# optional legacy product-scoped URL"}
              className="flex w-full rounded-sm border border-[#e5e5e5] bg-white p-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50 font-mono resize-y"
            />
            <p className="text-[10px] text-[#71717a] leading-relaxed">
              One target per line. Prefer <code className="bg-[#f4f4f5] px-1">internal:…</code> for in-platform fulfillment.
              HTTP lines here are legacy product-scoped hooks only — use Developer settings for signed integrator webhooks.
            </p>
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
