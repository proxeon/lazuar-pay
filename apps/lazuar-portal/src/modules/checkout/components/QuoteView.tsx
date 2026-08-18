"use client";

import { useState, useEffect } from "react";
import { Loader2, Download, Building2, CheckCircle2 } from "lucide-react";
import { components } from "@repo/api-types-ts";
import { submitCheckout, validateTin, TinValidationUnavailableError, getCheckoutStatus } from "../lib/api";
import { customQuoteBreakdown } from "../lib/grossBreakdown";
import { cn } from "../../../../lib/utils";
import Link from "next/link";
import type { PublicWorkspaceBranding } from "../../core/lib/branding";

type CustomCheckoutDto = components["schemas"]["Commerce.CustomCheckoutDto"];
type TenantBillingProfileDto = components["schemas"]["Billing.TenantBillingProfileDto"];

interface QuoteViewProps {
  tenantSlug: string;
  checkout: CustomCheckoutDto;
  branding?: PublicWorkspaceBranding | null;
  profile?: TenantBillingProfileDto | null;
  isCancelled?: boolean;
}

export function QuoteView({ tenantSlug, checkout, branding, profile, isCancelled }: QuoteViewProps) {
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [globalError, setGlobalError] = useState<string | null>(null);
  const [companyName, setCompanyName] = useState("");
  const [taxId, setTaxId] = useState("");
  const [idType, setIdType] = useState("BRN");
  const [idValue, setIdValue] = useState("");
  const [tinHint, setTinHint] = useState<string | null>(null);
  const [buyerEmail, setBuyerEmail] = useState(checkout.client_email || "");

  const isCompleted = checkout.status === "COMPLETED";
  const [portalHref, setPortalHref] = useState(`/${tenantSlug}/portal`);

  useEffect(() => {
    if (!isCompleted) return;
    let cancelled = false;
    getCheckoutStatus(tenantSlug, checkout.id)
      .then((response) => {
        if (cancelled) return;
        const minted = response.token;
        if (minted) {
          setPortalHref(`/${tenantSlug}/portal?token=${encodeURIComponent(minted)}`);
        }
      })
      .catch(() => {
        /* leave tokenless portal (magic-link form) */
      });
    return () => {
      cancelled = true;
    };
  }, [isCompleted, tenantSlug, checkout.id]);
  const isExpired = checkout.status === "EXPIRED" || new Date(checkout.expires_at).getTime() < Date.now();
  const sellerName = checkout.is_b2b_required
    ? (profile?.legal_name || branding?.name || "Merchant")
    : (branding?.name || "Merchant");
  const logoUrl = checkout.is_b2b_required ? (profile?.logo_url || branding?.logo_url) : branding?.logo_url;
  const quoteNumber = checkout.document_number || "PENDING";
  const lineNet = checkout.line_items.reduce((sum, item) => sum + item.quantity * item.unit_price, 0);
  const quoteSst = customQuoteBreakdown(lineNet, !!profile?.sst_registration_number);

  const handleProceedToPayment = async () => {
    if (checkout.is_b2b_required && !companyName.trim()) {
      setGlobalError("Company name is required for this payment request.");
      return;
    }
    if (checkout.is_b2b_required && !taxId.trim()) {
      setGlobalError("Company tax ID (TIN) is required for this payment request.");
      return;
    }
    if (checkout.is_b2b_required && !idValue.trim()) {
      setGlobalError("Buyer ID type and ID value (BRN / NRIC / PASSPORT / ARMY) are required.");
      return;
    }
    const email = (checkout.client_email || buyerEmail).trim();
    if (!email || email.toLowerCase() === "customer@example.com") {
      setGlobalError("A real buyer email is required.");
      return;
    }

    setIsSubmitting(true);
    setGlobalError(null);
    setTinHint(null);

    try {
      if (checkout.is_b2b_required) {
        try {
          const tin = await validateTin(tenantSlug, taxId.trim(), idType, idValue.trim());
          if (!tin.is_valid) {
            setGlobalError("This TIN / ID pair is not valid in MyInvois.");
            setIsSubmitting(false);
            return;
          }
          setTinHint(tin.taxpayer_name ? `Matched: ${tin.taxpayer_name}` : "TIN is valid.");
        } catch (err: unknown) {
          if (!(err instanceof TinValidationUnavailableError)) {
            throw err;
          }
          setTinHint("MyInvois is not connected. TIN will be stored on the commercial invoice.");
        }
      }

      const payload = {
        tenant_slug: tenantSlug,
        product_slug: "custom",
        session_id: checkout.id,
        name: checkout.client_name || "Customer",
        email,
        company_name: checkout.is_b2b_required ? companyName.trim() : undefined,
        tax_id: checkout.is_b2b_required ? taxId.trim() : undefined,
        id_type: checkout.is_b2b_required ? idType : undefined,
        id_value: checkout.is_b2b_required ? idValue.trim() : undefined,
        is_guest_checkout: true
      };

      const result = await submitCheckout(payload);

      if (result.is_zero_amount_bypass) {
        window.location.reload();
      } else {
        window.location.href = result.url;
      }
    } catch (err: unknown) {
      setGlobalError(err instanceof Error ? err.message : "An error occurred initiating checkout.");
      setIsSubmitting(false);
    }
  };

  const downloadDraftUrl = checkout.draft_pdf_url;

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
              <p className="font-bold uppercase tracking-widest text-[11px] mb-1">Payment request settled</p>
              <p className="text-sm font-medium">This payment request has been successfully completed.</p>
            </div>
          </div>
          <Link href={portalHref} className="h-10 px-6 bg-emerald-700 hover:bg-emerald-800 text-white text-xs font-bold uppercase tracking-widest transition-colors shrink-0 flex items-center justify-center whitespace-nowrap">
            Open buyer portal
          </Link>
        </div>
      )}

      <div className="bg-card border border-border/60 shadow-sm rounded-none overflow-hidden">
        <div className="p-8 sm:p-12 border-b border-border/40 flex flex-col md:flex-row md:items-start justify-between gap-8 bg-secondary/10">
          
          <div className="space-y-4 max-w-sm">
            {logoUrl ? (
              <img src={logoUrl} alt="Company Logo" className="max-h-16 object-contain" />
            ) : (
              <div className="h-16 w-16 bg-secondary flex items-center justify-center border border-border/60">
                <Building2 size={24} className="text-muted-foreground" />
              </div>
            )}
            <div>
              <h2 className="text-lg font-bold text-foreground">{sellerName}</h2>
              {checkout.is_b2b_required && profile?.tin && <p className="text-xs text-muted-foreground font-mono mt-1">TIN: {profile.tin}</p>}
              {checkout.is_b2b_required && profile?.registration_number && <p className="text-xs text-muted-foreground font-mono mt-0.5">SSM: {profile.registration_number}</p>}
            </div>
          </div>

          <div className="md:text-right space-y-1.5">
            <h1 className="text-3xl font-light tracking-tight text-foreground uppercase">Proforma Invoice</h1>
            <p className="text-sm font-mono text-muted-foreground">No: {quoteNumber}</p>
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
          {checkout.client_email ? (
            <p className="text-sm text-muted-foreground">{checkout.client_email}</p>
          ) : (
            <input
              type="email"
              required
              value={buyerEmail}
              onChange={(e) => setBuyerEmail(e.target.value)}
              placeholder="buyer@example.com"
              className="mt-2 h-10 w-full max-w-sm border border-border bg-background px-3 text-sm"
            />
          )}
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
          <div className="w-full md:w-auto space-y-3">
            {checkout.is_b2b_required && !isCompleted && (
              <div className="space-y-2 w-full md:min-w-[280px]">
                <p className="text-[11px] font-medium text-amber-700 dark:text-amber-500">
                  Enter company tax details to issue a tax invoice after payment.
                </p>
                <input
                  value={companyName}
                  onChange={(e) => setCompanyName(e.target.value)}
                  placeholder="Company name"
                  className="w-full h-9 border border-border bg-background px-3 text-sm"
                />
                <input
                  value={taxId}
                  onChange={(e) => setTaxId(e.target.value)}
                  placeholder="Tax ID (TIN) *"
                  className="w-full h-9 border border-border bg-background px-3 text-sm"
                />
                <select
                  value={idType}
                  onChange={(e) => setIdType(e.target.value)}
                  className="w-full h-9 border border-border bg-background px-3 text-sm"
                >
                  <option value="BRN">BRN</option>
                  <option value="NRIC">NRIC</option>
                  <option value="PASSPORT">PASSPORT</option>
                  <option value="ARMY">ARMY</option>
                </select>
                <input
                  value={idValue}
                  onChange={(e) => setIdValue(e.target.value)}
                  placeholder="SSM / NRIC / passport no. *"
                  className="w-full h-9 border border-border bg-background px-3 text-sm"
                />
                {tinHint && <p className="text-[11px] text-emerald-700">{tinHint}</p>}
              </div>
            )}
          </div>
          
          <div className="w-full md:w-auto min-w-[240px]">
            <div className="flex justify-between items-center py-2 border-b border-border/40">
              <span className="text-sm font-semibold text-muted-foreground">Subtotal</span>
              <span className="text-base font-mono text-foreground font-medium">MYR {lineNet.toFixed(2)}</span>
            </div>
            {quoteSst.lineTax > 0 && (
              <div className="flex justify-between items-center py-2 border-b border-border/40">
                <span className="text-sm font-semibold text-muted-foreground">SST (8%)</span>
                <span className="text-base font-mono text-foreground font-medium">MYR {quoteSst.lineTax.toFixed(2)}</span>
              </div>
            )}
            <div className="flex justify-between items-center py-4">
              <span className="text-base font-bold uppercase tracking-widest text-foreground">Total Due</span>
              <span className="text-2xl font-mono font-bold text-foreground">MYR {quoteSst.gross.toFixed(2)}</span>
            </div>
          </div>
        </div>

      </div>

      <div className="flex flex-col-reverse sm:flex-row items-center justify-between gap-4 pt-4">
        {downloadDraftUrl && (
          <a
            href={downloadDraftUrl}
            target="_blank"
            rel="noopener noreferrer"
            className="w-full sm:w-auto h-12 px-6 border border-border/60 bg-card hover:bg-secondary text-foreground text-xs font-bold uppercase tracking-widest transition-colors flex items-center justify-center gap-2"
          >
            <Download size={14} /> Download PDF Quote
          </a>
        )}
        
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
