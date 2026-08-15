"use client";

import { useState } from "react";
import Link from "next/link";
import { CheckoutAuthContext } from "../types";
import { IdentityBanner } from "./IdentityBanner";
import { submitCheckout, PublicCheckoutRequestDto, ProductDto } from "../lib/api";

interface CheckoutFormProps {
  tenantSlug: string;
  product: ProductDto;
  authContext: CheckoutAuthContext;
  isCouponApplied: boolean;
  couponCode: string;
  quantity: number;
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
  onSetGuestMode,
  onError
}: CheckoutFormProps) {
  const config = product.checkout_configuration;

  const [name, setName] = useState(authContext.userName || "");
  const [email, setEmail] = useState(authContext.userEmail || "");
  const [phone, setPhone] = useState("");
  
  // [MVP-HIDE]
  // const [isCompany, setIsCompany] = useState(false);
  // const [companyName, setCompanyName] = useState("");
  // const [taxId, setTaxId] = useState("");

  const [addressLine1, setAddressLine1] = useState("");
  const [city, setCity] = useState("");
  const [postalCode, setPostalCode] = useState("");
  const [stateCode, setStateCode] = useState("");
  const [countryCode, setCountryCode] = useState("MY");
  
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
      company_name: undefined, // [MVP-HIDE] config.requires_tax_id && isCompany ? companyName : undefined,
      tax_id: undefined, // [MVP-HIDE] config.requires_tax_id && isCompany ? taxId : undefined,
      address_line1: config.requires_address ? addressLine1 : undefined,
      city: config.requires_address ? city : undefined,
      postal_code: config.requires_address ? postalCode : undefined,
      state_code: config.requires_address ? stateCode : undefined,
      country_code: config.requires_address ? countryCode : undefined,
      quantity: quantity,
      is_guest_checkout: authContext.isGuestMode,
      coupon_code: isCouponApplied ? couponCode : undefined
    };

    try {
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
      onError(err.message || "An error occurred during checkout.");
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
        <h3 className="text-xs font-bold uppercase tracking-widest text-muted-foreground">Account Details</h3>
        
        <div className="space-y-2">
          <label htmlFor="name" className="text-sm font-semibold text-foreground">Full Name</label>
          <input
            id="name" 
            type="text" 
            required
            value={name} 
            onChange={e => setName(e.target.value)}
            disabled={isProfileLocked}
            className="flex h-12 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors disabled:opacity-60 disabled:cursor-not-allowed focus:outline-none focus:ring-1 focus:ring-foreground"
          />
        </div>

        <div className="space-y-2">
          <label htmlFor="email" className="text-sm font-semibold text-foreground">Email Address</label>
          <input
            id="email" 
            type="email" 
            required
            value={email} 
            onChange={e => setEmail(e.target.value)}
            disabled={isProfileLocked}
            className="flex h-12 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors disabled:opacity-60 disabled:cursor-not-allowed focus:outline-none focus:ring-1 focus:ring-foreground"
          />
        </div>

        {config.requires_phone && (
          <div className="space-y-2">
            <label htmlFor="phone" className="text-sm font-semibold text-foreground">WhatsApp Number</label>
            <input
              id="phone" 
              type="tel" 
              required
              value={phone} 
              onChange={e => setPhone(e.target.value)}
              className="flex h-12 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-foreground"
              placeholder="+60 12-345 6789"
            />
            <p className="text-[11px] text-muted-foreground">Required for delivery and important updates.</p>
          </div>
        )}
      </div>

      {(config.requires_tax_id || config.requires_address) && (
        <div className="space-y-4 border-b border-border/60 pb-6">
          <h3 className="text-xs font-bold uppercase tracking-widest text-muted-foreground">Billing Details</h3>
          
          {/* [MVP-HIDE]
          {config.requires_tax_id && (
            <div className="space-y-4">
              <label className="flex items-center gap-2 cursor-pointer w-fit group">
                <input 
                  type="checkbox" 
                  checked={isCompany} 
                  onChange={e => setIsCompany(e.target.checked)} 
                  className="rounded-sm border-border/60 bg-background text-foreground focus:ring-foreground" 
                />
                <span className="text-sm font-medium text-foreground group-hover:text-foreground/80 transition-colors">I am buying on behalf of a company</span>
              </label>

              {isCompany && (
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4 pt-2">
                  <div className="space-y-2">
                    <label className="text-sm font-semibold text-foreground">Company Name *</label>
                    <input required type="text" value={companyName} onChange={e => setCompanyName(e.target.value)} className="flex h-12 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-foreground" />
                  </div>
                  <div className="space-y-2">
                    <label className="text-sm font-semibold text-foreground">Tax Identification Number (TIN) *</label>
                    <input required type="text" value={taxId} onChange={e => setTaxId(e.target.value)} className="flex h-12 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-foreground" placeholder="e.g. C12345678" />
                  </div>
                </div>
              )}
            </div>
          )}
          */}

          {config.requires_address && (
            <div className="space-y-4 pt-2">
              <div className="space-y-2">
                {/* [MVP-HIDE] <label className="text-sm font-semibold text-foreground">{isCompany ? "Company Address *" : "Billing Address *"}</label> */}
                <label className="text-sm font-semibold text-foreground">Billing Address *</label>
                <input required type="text" value={addressLine1} onChange={e => setAddressLine1(e.target.value)} className="flex h-12 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-foreground" placeholder="Street Address" />
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <input required type="text" value={city} onChange={e => setCity(e.target.value)} className="flex h-12 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-foreground" placeholder="City" />
                </div>
                <div className="space-y-2">
                  <input required type="text" value={postalCode} onChange={e => setPostalCode(e.target.value)} className="flex h-12 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-foreground" placeholder="Postal Code" />
                </div>
              </div>
              <div className="grid grid-cols-2 gap-4">
                <div className="space-y-2">
                  <input required type="text" value={stateCode} onChange={e => setStateCode(e.target.value)} className="flex h-12 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-foreground" placeholder="State" />
                </div>
                <div className="space-y-2">
                  <input required type="text" value={countryCode} onChange={e => setCountryCode(e.target.value)} className="flex h-12 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-foreground" placeholder="Country Code (e.g. MY)" />
                </div>
              </div>
            </div>
          )}
        </div>
      )}

      <div className="pt-4">
        <p className="text-[11px] text-muted-foreground leading-relaxed mb-4">
          By proceeding, you agree to Lazuar's <Link href="/legal/terms" target="_blank" className="underline hover:text-foreground transition-colors">Terms of Service</Link> and <Link href="/legal/privacy" target="_blank" className="underline hover:text-foreground transition-colors">Privacy Policy</Link>, and acknowledge that your purchase is a direct transaction with the Creator.
        </p>
        <button 
          type="submit" 
          disabled={isSubmitting} 
          className="w-full h-14 text-sm font-bold tracking-wide uppercase bg-foreground text-background hover:bg-foreground/90 rounded-none disabled:opacity-50 transition-colors flex items-center justify-center"
        >
          {isSubmitting ? (
            <>
              <svg className="animate-spin -ml-1 mr-2 h-4 w-4 text-background" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24">
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4"></circle>
                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4zm2 5.291A7.962 7.962 0 014 12H0c0 3.042 1.135 5.824 3 7.938l3-2.647z"></path>
              </svg>
              Securing Data...
            </>
          ) : (
            "Proceed to Payment"
          )}
        </button>
      </div>
    </form>
  );
}
