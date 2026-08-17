import { Link } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { Loader2 } from "lucide-react";
import { client, type components } from "../lib/api-client";

type PublicPricingDto = components["schemas"]["One.PublicPricingDto"];

const FALLBACK_PRICING: PublicPricingDto = {
  gmv_take_percent: 0,
  starter_credits: 50,
  packages: [
    { amount_myr: 50, credits: 500 },
    { amount_myr: 100, credits: 1100 },
    { amount_myr: 200, credits: 2500 },
  ],
  sst_rate: 0,
  sst_note:
    "SST 0% — Supplier not SST-registered. Confirm with your accountant. We do not add SST at checkout today.",
  checkout_is_free: false,
  lhdn_credits_live: false,
  whatsapp_credits_live: false,
  lhdn_submit_credits: 3,
  whatsapp_send_credits: 0,
  hub_plan: {
    code: "hub_starter",
    name: "Hub Starter",
    amount_myr: 0,
    interval: "mo",
    currency: "MYR",
  },
};

function intervalLabel(interval: string) {
  if (interval === "yr") return "year";
  return "month";
}

export default function PricingPage() {
  const { data, isLoading } = useQuery({
    queryKey: ["public-pricing"],
    queryFn: async () => {
      const { data: body, error } = await client.GET("/one/public/pricing");
      if (error || !body) throw new Error(error?.detail || "Pricing unavailable");
      return body as PublicPricingDto;
    },
    retry: 1,
  });

  const pricing = data ?? FALLBACK_PRICING;
  const plan = pricing.hub_plan;
  const planPriced = (plan.amount_myr ?? 0) > 0;
  const checkoutFree = pricing.checkout_is_free === true;

  return (
    <div className="min-h-screen w-full bg-[#f5f5f5] font-sans text-[#1a1a1a]">
      <header className="h-14 border-b border-[#e5e5e5] bg-white">
        <div className="mx-auto flex h-full max-w-4xl items-center justify-between px-6">
          <span className="text-[13px] font-bold uppercase tracking-widest text-[#09090b]">Lazuar Hub</span>
          <div className="flex items-center gap-4">
            <Link to="/login" className="text-[12px] font-semibold text-[#71717a] hover:text-[#09090b]">
              Sign in
            </Link>
            <Link
              to="/signup"
              className="h-8 px-4 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest flex items-center hover:bg-[#27272a]"
            >
              Create workspace
            </Link>
          </div>
        </div>
      </header>

      <main className="mx-auto max-w-4xl px-6 py-14 space-y-10">
        <div className="space-y-3">
          <p className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Pricing</p>
          <h1 className="text-3xl font-semibold tracking-tight text-[#09090b]">RM 0 on your sales.</h1>
          <p className="text-[15px] text-[#52525b] max-w-2xl leading-relaxed">
            You pay Billplz / Stripe / CHIP their rate. We never take a percent of guest GMV. Checkout
            software is a Hub software fee — not a cut of what your customers pay you.
          </p>
        </div>

        {isLoading && !data && (
          <div className="flex items-center gap-2 text-[12px] text-[#a1a1aa] uppercase tracking-widest font-bold">
            <Loader2 size={14} className="animate-spin" /> Loading current plan
          </div>
        )}

        <section className="bg-white border border-[#e5e5e5] p-8 space-y-4">
          <h2 className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Hub plan</h2>
          <p className="text-2xl font-semibold text-[#09090b]">{plan.name}</p>
          {checkoutFree ? (
            <p className="text-[15px] text-[#52525b] leading-relaxed">
              Checkout software is <span className="font-semibold text-[#09090b]">free today</span>.{" "}
              {plan.name} has no listed monthly price until we publish one. Optional Hub subscription
              later is a flat software fee — never a GMV tax.
            </p>
          ) : !planPriced ? (
            <p className="text-[15px] text-[#52525b] leading-relaxed">
              Hub subscription is <span className="font-semibold text-[#09090b]">not on sale</span> in
              this environment. A RM 0 plan is unpaid, not free — checkout will not mint a Hub
              subscription until a price is configured.
            </p>
          ) : (
            <p className="text-[15px] text-[#52525b] leading-relaxed">
              <span className="font-semibold text-[#09090b]">
                RM {plan.amount_myr} / {intervalLabel(plan.interval)}
              </span>{" "}
              · {plan.currency}. This is a software fee for Hub, not a percent of your sales.
            </p>
          )}
          <p className="text-[13px] text-[#71717a] leading-relaxed">
            Money from your checkouts settles to your Billplz / Stripe / CHIP account. Lazuar is not
            Merchant of Record, not an acquirer, and does not KYC your buyers.
          </p>
        </section>

        <section className="bg-white border border-[#e5e5e5] p-8 space-y-5">
          <h2 className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Utility credits</h2>
          <p className="text-[14px] text-[#52525b] leading-relaxed">
            New workspaces get <span className="font-semibold text-[#09090b]">{pricing.starter_credits} starter credits</span>.
            Credits meter live-key LHDN e-invoice submits
            {pricing.lhdn_submit_credits > 0 ? ` (${pricing.lhdn_submit_credits} credits each)` : ""}{" "}
            when that product is on. They are not a tax on checkout GMV.
          </p>
          {!pricing.lhdn_credits_live && (
            <p className="text-[13px] text-[#71717a] leading-relaxed">
              LHDN merchant UI is not live in Hub Ops yet. Do not buy credits expecting e-invoice at
              checkout today.
            </p>
          )}
          {!pricing.whatsapp_credits_live && (
            <p className="text-[13px] text-[#71717a] leading-relaxed">
              WhatsApp recovery is not connected and is not billed.
            </p>
          )}
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-2">
            {pricing.packages.map((pkg) => (
              <div key={`${pkg.amount_myr}-${pkg.credits}`} className="border border-[#e5e5e5] h-20 flex flex-col items-center justify-center">
                <span className="text-[15px] font-mono font-bold text-[#09090b]">RM {pkg.amount_myr}</span>
                <span className="text-[11px] font-mono text-[#71717a]">{pkg.credits} credits</span>
              </div>
            ))}
          </div>
        </section>

        <section className="space-y-3">
          <h2 className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">What we are not</h2>
          <ul className="text-[13px] text-[#52525b] space-y-1.5 list-disc pl-5">
            <li>Not Merchant of Record for your sales</li>
            <li>Not a licensed acquirer — settlement stays on your gateway</li>
            <li>Not a KYC bureau — your processor handles merchant KYC</li>
            <li>Not a GMV take-rate. There is no platform cut of guest checkout.</li>
          </ul>
        </section>

        <p className="text-[12px] text-[#71717a] leading-relaxed border-t border-[#e5e5e5] pt-6">
          {pricing.sst_note}
        </p>

        <div className="flex flex-col sm:flex-row gap-3">
          <Link
            to="/signup"
            className="h-11 px-6 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest flex items-center justify-center hover:bg-[#27272a]"
          >
            Create workspace
          </Link>
          <Link
            to="/login"
            className="h-11 px-6 border border-[#e5e5e5] bg-white text-[#09090b] text-[11px] font-bold uppercase tracking-widest flex items-center justify-center hover:bg-[#f4f4f5]"
          >
            Sign in
          </Link>
        </div>
      </main>
    </div>
  );
}
