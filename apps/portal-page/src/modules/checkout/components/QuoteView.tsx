"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { Loader2, Download, Building2, CheckCircle2 } from "lucide-react";
import { components } from "@repo/api-types-ts";
import { submitCheckout } from "../lib/api";
import { cn } from "../../../lib/utils";

type CustomCheckoutDto = components["schemas"]["Commerce.CustomCheckoutDto"];
type TenantBillingProfileDto = components["schemas"]["Billing.TenantBillingProfileDto"];

interface QuoteViewProps {
  tenantSlug: string;
  checkout: CustomCheckoutDto;
  profile?: TenantBillingProfileDto | null;
  isCancelled?: boolean;
}

export function QuoteView({ tenantSlug, checkout, profile, isCancelled }: QuoteViewProps) {
  const router = useRouter();
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [globalError, setGlobalError] = useState<string | null>(null);

  const isCompleted = checkout.status === "COMPLETED";
  const isExpired = checkout.status === "EXPIRED" || new Date(checkout.expires_at).getTime() < Date.now();

  const handleProceedToPayment = async () => {
    setIsSubmitting(true);
    setGlobalError(null);

    try {
      const payload = {
        tenant_slug: tenantSlug,
        product_slug: "custom", 
        session_id: checkout.id,
        name: checkout.client_name || "Customer",
        email: checkout.client_email || "customer@example.com",
        is_guest_checkout: true
      };

      const result = await submitCheckout(payload as any);
      
      if (result.is_zero_amount_bypass) {
        window.location.reload();
      } else {
        window.location.href = result.url;
      }
    } catch (err: any) {
      setGlobalError(err.message || "An error occurred initiating checkout.");
      setIsSubmitting(false);
    }
  };

  const API_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:8080/api/v1";
  const downloadDraftUrl = `${API_URL}/public/billing/${tenantSlug}/documents/draft/${checkout.id}`;

  return (
    <div className="w-full max-w-4xl mx-auto space-y-6">
      
      {isCancelled && !globalError && (
        <div className="p-4 bg-amber-50 border border-amber-200 text-amber-800 text-sm font-medium">
          Payment was cancelled or failed. Please try again.
        </div>
      )}

      {globalError && (
        <div className="p-4 bg-red-50 border border-red-200 text-red-600 text-sm font-medium">
          {globalError}
        </div>
      )}

      {isCompleted && (
        <div className="p-6 bg-emerald-50 dark:bg-emerald-950/20 border border-emerald-200 dark:border-emerald-900 flex flex-col sm:flex-row items-center justify-between gap-4">
          <div className="flex items-center gap-3 text-emerald-800 dark:text-emerald-500">
            <CheckCircle2 size={24} className="shrink-0" />
            <div>
              <p className="font-bold uppercase tracking-widest text-[11px] mb-1">Invoice Settled</p>
              <p className="text-sm font-medium">This payment request has been successfully completed.</p>
            </div>
          </div>
          <button onClick={() => window.alert("Official receipt will be emailed shortly or available in the client portal.")} className="h-10 px-6 bg-emerald-700 hover:bg-emerald-800 text-white text-xs font-bold uppercase tracking-widest transition-colors shrink-0 whitespace-nowrap">
            View Official Receipt
          </button>
        </div>
      )}

      <div className="bg-card border border-border/60 shadow-sm rounded-none overflow-hidden">
        <div className="p-8 sm:p-12 border-b border-border/40 flex flex-col md:flex-row md:items-start justify-between gap-8 bg-secondary/10">
          
          <div className="space-y-4 max-w-sm">
            {profile?.logo_url ? (
              <img src={profile.logo_url} alt="Company Logo" className="max-h-16 object-contain" />
            ) : (
              <div className="h-16 w-16 bg-secondary flex items-center justify-center border border-border/60">
                <Building2 size={24} className="text-muted-foreground" />
              </div>
            )}
            <div>
              <h2 className="text-lg font-bold text-foreground">{profile?.legal_name || "Lazuar Merchant"}</h2>
              {profile?.tin && <p className="text-xs text-muted-foreground font-mono mt-1">TIN: {profile.tin}</p>}
              {profile?.registration_number && <p className="text-xs text-muted-foreground font-mono mt-0.5">SSM: {profile.registration_number}</p>}
            </div>
          </div>

          <div className="md:text-right space-y-1.5">
            <h1 className="text-3xl font-light tracking-tight text-foreground uppercase">Proforma Invoice</h1>
            <p className="text-sm font-mono text-muted-foreground">REF: {checkout.id.substring(0,8).toUpperCase()}</p>
            <p className="text-sm font-mono text-muted-foreground">Date: {new Date(checkout.created_at).toLocaleDateString('en-GB')}</p>
            {!isCompleted && (
              <p className={cn("text-sm font-mono font-semibold mt-2", isExpired ? "text-rose-600" : "text-amber-600")}>
                Valid Until: {new Date(checkout.expires_at).toLocaleDateString('en-GB')}
              </p>
            )}
          </div>
        </div>

        <div className="p-8 sm:p-12 border-b border-border/40">
          <h3 className="text-[10px] font-bold uppercase tracking-widest text-muted-foreground mb-3">Billed To</h3>
          <p className="text-base font-semibold text-foreground">{checkout.client_name || "Client"}</p>
          <p className="text-sm text-muted-foreground">{checkout.client_email}</p>
        </div>

        <div className="w-full overflow-x-auto">
          <table className="w-full text-left text-sm">
            <thead className="bg-secondary/30 border-b border-border/60">
              <tr>
                <th className="px-8 py-4 font-bold uppercase tracking-widest text-muted-foreground text-[10px]">Description</th>
                <th className="px-8 py-4 font-bold uppercase tracking-widest text-muted-foreground text-[10px] text-right">Qty</th>
                <th className="px-8 py-4 font-bold uppercase tracking-widest text-muted-foreground text-[10px] text-right">Unit Price</th>
                <th className="px-8 py-4 font-bold uppercase tracking-widest text-muted-foreground text-[10px] text-right">Total</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-border/40">
              {checkout.line_items.map((item, idx) => (
                <tr key={idx} className="bg-card">
                  <td className="px-8 py-5 font-medium text-foreground">{item.description}</td>
                  <td className="px-8 py-5 text-muted-foreground text-right">{item.quantity}</td>
                  <td className="px-8 py-5 font-mono text-muted-foreground text-right">{item.unit_price.toFixed(2)}</td>
                  <td className="px-8 py-5 font-mono font-bold text-foreground text-right">{(item.quantity * item.unit_price).toFixed(2)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div className="p-8 sm:p-12 bg-secondary/10 flex flex-col md:flex-row items-center justify-between gap-6 border-t border-border/60">
          <div className="w-full md:w-auto">
            {checkout.is_b2b_required && !isCompleted && (
              <p className="text-[11px] font-medium text-amber-700 dark:text-amber-500 bg-amber-50 dark:bg-amber-950/20 border border-amber-200 dark:border-amber-900 p-3 w-full md:max-w-xs">
                Company Tax Details (TIN) will be collected during the secure checkout step to generate your official LHDN e-Invoice.
              </p>
            )}
          </div>
          
          <div className="w-full md:w-auto min-w-[240px]">
            <div className="flex justify-between items-center py-2 border-b border-border/40">
              <span className="text-sm font-semibold text-muted-foreground">Subtotal</span>
              <span className="text-base font-mono text-foreground font-medium">MYR {checkout.total_amount.toFixed(2)}</span>
            </div>
            <div className="flex justify-between items-center py-4">
              <span className="text-base font-bold uppercase tracking-widest text-foreground">Total Due</span>
              <span className="text-2xl font-mono font-bold text-foreground">MYR {checkout.total_amount.toFixed(2)}</span>
            </div>
          </div>
        </div>

      </div>

      <div className="flex flex-col-reverse sm:flex-row items-center justify-between gap-4 pt-4">
        <a 
          href={downloadDraftUrl} 
          target="_blank" 
          rel="noopener noreferrer"
          className="w-full sm:w-auto h-12 px-6 border border-border/60 bg-card hover:bg-secondary text-foreground text-xs font-bold uppercase tracking-widest transition-colors flex items-center justify-center gap-2"
        >
          <Download size={14} /> Download PDF Quote
        </a>
        
        {!isCompleted && (
          <button 
            onClick={handleProceedToPayment}
            disabled={isSubmitting || isExpired}
            className="w-full sm:w-auto min-w-[200px] h-12 px-8 bg-foreground text-background text-xs font-bold uppercase tracking-widest hover:bg-foreground/90 disabled:opacity-50 transition-colors flex items-center justify-center gap-2"
          >
            {isSubmitting && <Loader2 size={14} className="animate-spin" />} 
            {isExpired ? "Quote Expired" : "Proceed to Payment"}
          </button>
        )}
      </div>

    </div>
  );
}
