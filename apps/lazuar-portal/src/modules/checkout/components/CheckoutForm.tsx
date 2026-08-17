"use client";

import { useState } from "react";
import Link from "next/link";
import { CheckoutAuthContext } from "../types";
import { interpolateNodes, useCheckoutT } from "../i18n/CheckoutI18n";
import { IdentityBanner } from "./IdentityBanner";
import { submitCheckout, validateTin, PublicCheckoutRequestDto, ProductDto } from "../lib/api";

interface CheckoutFormProps {
  tenantSlug: string;
  product: ProductDto;
  authContext: CheckoutAuthContext;
  isCouponApplied: boolean;
  couponCode: string;
  quantity: number;
  interval?: string;
  priceId?: string;
  workspaceName?: string;
  onSetGuestMode: (isGuest: boolean) => void;
  onError: (errorMsg: string) => void;
}

export function CheckoutForm({
  tenantSlug,
  product,
  authContext,
  isCouponApplied,
  couponCode,
  quantity,
  interval,
  priceId,
  workspaceName,
  onSetGuestMode,
  onError
}: CheckoutFormProps) {
  const { t } = useCheckoutT();
  const config = product.checkout_configuration;

  const [name, setName] = useState(authContext.userName || "");
  const [email, setEmail] = useState(authContext.userEmail || "");
  const [phone, setPhone] = useState("");
  const [companyName, setCompanyName] = useState("");
  const [taxId, setTaxId] = useState("");
  const [idType, setIdType] = useState("BRN");
  const [idValue, setIdValue] = useState("");
  const [tinHint, setTinHint] = useState<string | null>(null);

  const [addressLine1, setAddressLine1] = useState("");
  const [city, setCity] = useState("");
  const [postalCode, setPostalCode] = useState("");
  const [stateCode, setStateCode] = useState("");
  const [countryCode, setCountryCode] = useState("MYS");
  
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleEnableGuestMode = () => {
    onSetGuestMode(true);
    setName("");
    setEmail("");
  };

  const handleDisableGuestMode = () => {
    onSetGuestMode(false);
    setName(authContext.userName || "");
    setEmail(authContext.userEmail || "");
  };

  const handleSubmit = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setIsSubmitting(true);

    const payload: PublicCheckoutRequestDto = {
      tenant_slug: tenantSlug,
      product_slug: product.slug,
      name: name,
      email: email,
      phone: config.requires_phone ? phone : undefined,
      company_name: config.requires_tax_id ? companyName : undefined,
      tax_id: config.requires_tax_id ? taxId : undefined,
      id_type: config.requires_tax_id ? idType : undefined,
      id_value: config.requires_tax_id ? idValue : undefined,
      address_line1: config.requires_address ? addressLine1 : undefined,
      city: config.requires_address ? city : undefined,
      postal_code: config.requires_address ? postalCode : undefined,
      state_code: config.requires_address ? stateCode : undefined,
      country_code: config.requires_address ? countryCode : undefined,
      quantity: quantity,
      interval,
      price_id: priceId,
      is_guest_checkout: authContext.isGuestMode,
      coupon_code: isCouponApplied ? couponCode : undefined
    };

    try {
      if (config.requires_tax_id) {
        try {
          const tin = await validateTin(tenantSlug, taxId, idType, idValue);
          if (!tin.is_valid) {
            onError("This TIN / ID pair is not valid in MyInvois.");
            setIsSubmitting(false);
            return;
          }
          setTinHint(tin.taxpayer_name ? `Matched: ${tin.taxpayer_name}` : "TIN is valid.");
        } catch (err: any) {
          onError(err.message || "Could not validate TIN.");
          setIsSubmitting(false);
          return;
        }
      }

      const result = await submitCheckout(payload);

      // Zero-amount already settled server-side; navigate to the initiate URL (includes sub_id).
      // Do not guess /success — missing handle would show Invalid Session after a real COMPLETED.
      if (result.is_zero_amount_bypass && !result.url) {
        onError("Checkout completed but the confirmation link was missing. Please check your email.");
        setIsSubmitting(false);
        return;
      }

      window.location.assign(result.url);
    } catch (err: any) {
      onError(err.message || "Checkout submission failed.");
      setIsSubmitting(false);
    }
  };

  const isProfileLocked = !!(authContext.userName && !authContext.isGuestMode);

  return (
    <form onSubmit={handleSubmit} className="space-y-6">
      <IdentityBanner
        userName={authContext.userName}
        isAdminOfTenant={authContext.isAdminOfTenant}
        isGuestMode={authContext.isGuestMode}
        onEnableGuestMode={handleEnableGuestMode}
        onDisableGuestMode={handleDisableGuestMode}
      />

      <div className="space-y-4 border-b border-border/60 pb-6">
        <h3 className="text-xs font-bold uppercase tracking-widest text-muted-foreground">{t("form.accountDetails")}</h3>
        
        <div className="space-y-2">
          <label htmlFor="name" className="text-sm font-semibold text-foreground">{t("form.fullName")}</label>
          <input
            id="name" 
            type="text" 
            name="name"
            autoComplete="name"
            autoCapitalize="words"
            required
            value={name} 
            onChange={e => setName(e.target.value)}
            disabled={isProfileLocked}
            className="flex h-12 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-base shadow-sm transition-colors disabled:opacity-60 disabled:cursor-not-allowed focus:outline-none focus:ring-1 focus:ring-foreground"
          />
        </div>

        <div className="space-y-2">
          <label htmlFor="email" className="text-sm font-semibold text-foreground">{t("form.email")}</label>
          <input
            id="email" 
            type="email"
            name="email"
            autoComplete="email"
            required
            value={email} 
            onChange={e => setEmail(e.target.value)}
            disabled={isProfileLocked}
            className="flex h-12 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-base shadow-sm transition-colors disabled:opacity-60 disabled:cursor-not-allowed focus:outline-none focus:ring-1 focus:ring-foreground"
          />
        </div>

        {config.requires_phone && (
          <div className="space-y-2">
            <label htmlFor="phone" className="text-sm font-semibold text-foreground">{t("form.phone")}</label>
            <input
              id="phone" 
              type="tel"
              name="tel"
              autoComplete="tel"
              required
              value={phone} 
              onChange={e => setPhone(e.target.value)}
              className="flex h-12 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-base shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-foreground"
              placeholder={t("form.phonePlaceholder")}
            />
            <p className="text-[11px] text-muted-foreground">{t("form.phoneHint")}</p>
          </div>
        )}
      </div>

      {(config.requires_tax_id || config.requires_address) && (
        <div className="space-y-4 border-b border-border/60 pb-6">
          <h3 className="text-xs font-bold uppercase tracking-widest text-muted-foreground">{t("form.billingDetails")}</h3>

          {config.requires_tax_id && (
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <div className="space-y-2">
                <label htmlFor="company-name" className="text-sm font-semibold text-foreground">{t("form.companyName")}</label>
                <input
                  id="company-name"
                  name="organization"
                  autoComplete="organization"
                  required
                  type="text"
                  value={companyName}
                  onChange={e => setCompanyName(e.target.value)}
                  className="flex h-12 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-base shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-foreground"
                />
              </div>
              <div className="space-y-2">
                <label htmlFor="tax-id" className="text-sm font-semibold text-foreground">{t("form.taxId")}</label>
                <input
                  id="tax-id"
                  name="tax-id"
                  required
                  type="text"
                  value={taxId}
                  onChange={e => setTaxId(e.target.value)}
                  className="flex h-12 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-base shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-foreground"
                  placeholder={t("form.taxIdPlaceholder")}
                />
                <p className="text-[11px] text-muted-foreground">{t("form.taxIdHint")}</p>
              </div>
              <div className="space-y-2">
                <label htmlFor="id-type" className="text-sm font-semibold text-foreground">ID type</label>
                <select
                  id="id-type"
                  required
                  value={idType}
                  onChange={e => setIdType(e.target.value)}
                  className="flex h-12 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-base shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-foreground"
                >
                  <option value="BRN">BRN</option>
                  <option value="NRIC">NRIC</option>
                  <option value="PASSPORT">PASSPORT</option>
                  <option value="ARMY">ARMY</option>
                </select>
              </div>
              <div className="space-y-2">
                <label htmlFor="id-value" className="text-sm font-semibold text-foreground">ID value</label>
                <input
                  id="id-value"
                  required
                  type="text"
                  value={idValue}
                  onChange={e => setIdValue(e.target.value)}
                  className="flex h-12 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-base shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-foreground"
                  placeholder="SSM / NRIC / passport no."
                />
                {tinHint && <p className="text-[11px] text-emerald-700">{tinHint}</p>}
              </div>
            </div>
          )}

          {config.requires_address && (
            <div className="space-y-4 pt-2">
              <div className="space-y-2">
                <label htmlFor="address-line1" className="text-sm font-semibold text-foreground">{t("form.billingAddress")}</label>
                <input
                  id="address-line1"
                  name="address-line1"
                  autoComplete="address-line1"
                  required
                  type="text"
                  value={addressLine1}
                  onChange={e => setAddressLine1(e.target.value)}
                  className="flex h-12 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-base shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-foreground"
                  placeholder={t("form.street")}
                />
              </div>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <label htmlFor="city" className="text-sm font-semibold text-foreground">{t("form.city")}</label>
                  <input
                    id="city"
                    name="address-level2"
                    autoComplete="address-level2"
                    required
                    type="text"
                    value={city}
                    onChange={e => setCity(e.target.value)}
                    className="flex h-12 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-base shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-foreground"
                    placeholder={t("form.city")}
                  />
                </div>
                <div className="space-y-2">
                  <label htmlFor="postal-code" className="text-sm font-semibold text-foreground">{t("form.postal")}</label>
                  <input
                    id="postal-code"
                    name="postal-code"
                    autoComplete="postal-code"
                    inputMode="numeric"
                    required
                    type="text"
                    value={postalCode}
                    onChange={e => setPostalCode(e.target.value)}
                    className="flex h-12 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-base shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-foreground"
                    placeholder={t("form.postal")}
                  />
                </div>
              </div>
              <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                <div className="space-y-2">
                  <label htmlFor="state" className="text-sm font-semibold text-foreground">{t("form.state")}</label>
                  <input
                    id="state"
                    name="address-level1"
                    autoComplete="address-level1"
                    required
                    type="text"
                    value={stateCode}
                    onChange={e => setStateCode(e.target.value)}
                    className="flex h-12 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-base shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-foreground"
                    placeholder={t("form.state")}
                  />
                </div>
                <div className="space-y-2">
                  <label htmlFor="country" className="text-sm font-semibold text-foreground">{t("form.country")}</label>
                  <input
                    id="country"
                    name="country"
                    autoComplete="country"
                    required
                    type="text"
                    value={countryCode}
                    onChange={e => setCountryCode(e.target.value)}
                    className="flex h-12 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-base shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-foreground"
                    placeholder={t("form.country")}
                  />
                </div>
              </div>
            </div>
          )}
        </div>
      )}

      <div className="pt-4">
        <p className="text-[11px] text-muted-foreground leading-relaxed mb-4">
          {interpolateNodes(t("form.consent"), {
            terms: (
              <Link href="/legal/terms" target="_blank" className="underline hover:text-foreground transition-colors">
                {t("form.consentTerms")}
              </Link>
            ),
            privacy: (
              <Link href="/legal/privacy" target="_blank" className="underline hover:text-foreground transition-colors">
                {t("form.consentPrivacy")}
              </Link>
            ),
            seller: workspaceName || tenantSlug,
          })}
        </p>
        <button 
          type="submit" 
          disabled={isSubmitting} 
          className="w-full h-14 text-sm font-bold tracking-wide uppercase text-background hover:opacity-90 rounded-none disabled:opacity-50 transition-colors flex items-center justify-center bg-foreground"
          style={{ backgroundColor: "var(--brand, var(--foreground))" }}
        >
          {isSubmitting ? (
            <>
              <svg className="animate-spin -ml-1 mr-2 h-4 w-4 text-background" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
              </svg>
              {t("cta.securing")}
            </>
          ) : (
            t("cta.proceed")
          )}
        </button>
      </div>
    </form>
  );
}
