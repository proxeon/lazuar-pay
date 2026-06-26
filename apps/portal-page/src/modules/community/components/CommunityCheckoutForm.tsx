"use client";

import { useState } from "react";
import Link from "next/link";
import { CheckoutAuthContext } from "../../checkout/types";
import { IdentityBanner } from "../../checkout/components/IdentityBanner";
import { submitCheckout, PublicCheckoutRequestDto } from "../lib/api";

interface CommunityCheckoutFormProps {
  tenantSlug: string;
  planSlug: string;
  authContext: CheckoutAuthContext;
  isCouponApplied: boolean;
  couponCode: string;
  onSetGuestMode: (isGuest: boolean) => void;
  onSuccessZeroAmount: () => void;
}

export function CommunityCheckoutForm({
  tenantSlug,
  planSlug,
  authContext,
  isCouponApplied,
  couponCode,
  onSetGuestMode,
  onSuccessZeroAmount
}: CommunityCheckoutFormProps) {
  const [name, setName] = useState(authContext.userName || "");
  const [email, setEmail] = useState(authContext.userEmail || "");
  const [phone, setPhone] = useState("");
  
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

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
    setError(null);

    const payload: PublicCheckoutRequestDto = {
      tenant_slug: tenantSlug,
      plan_slug: planSlug,
      name: name,
      email: email,
      phone: phone,
      is_guest_checkout: authContext.isGuestMode,
      coupon_code: isCouponApplied ? couponCode : undefined
    };

    try {
      const result = await submitCheckout(payload);
      
      if (result.is_zero_amount_bypass) {
        onSuccessZeroAmount();
      } else {
        window.location.href = result.url;
      }
    } catch (err: any) {
      setError(err.message || "An error occurred during checkout.");
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

      {error && (
        <div className="p-3 bg-red-50 border border-red-200 text-red-600 text-sm font-medium">
          {error}
        </div>
      )}

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
        <p className="text-[11px] text-muted-foreground">Used for weekly class links and reminders.</p>
      </div>

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
