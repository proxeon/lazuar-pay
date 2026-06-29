"use client";

import { useState, useEffect } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { getCheckoutStatus, type ProductDto } from "../lib/api";

interface CheckoutSuccessViewProps {
  tenantSlug: string;
  product: ProductDto;
}

export function CheckoutSuccessView({ tenantSlug, product }: CheckoutSuccessViewProps) {
  const searchParams = useSearchParams();
  const subId = searchParams.get("sub_id");
  
  const [status, setStatus] = useState<"VERIFYING" | "SUCCESS" | "TIMEOUT" | "ERROR">("VERIFYING");
  const [accessToken, setAccessToken] = useState<string | null>(null);

  useEffect(() => {
    if (!subId) {
      setStatus("ERROR");
      return;
    }

    let attempts = 0;
    const maxAttempts = 10;
    let timeoutId: NodeJS.Timeout;

    const verifyPayment = async () => {
      attempts++;

      try {
        const response = await getCheckoutStatus(subId);

        if (response.status === "ACTIVE" || response.status === "COMPLETED") {
          if (response.token) setAccessToken(response.token);
          setStatus("SUCCESS");
          return;
        }
      } catch (err) {
      }

      if (attempts >= maxAttempts) {
        setStatus("TIMEOUT");
      } else {
        timeoutId = setTimeout(verifyPayment, 2500);
      }
    };

    verifyPayment();

    return () => clearTimeout(timeoutId);
  }, [subId]);

  if (status === "ERROR") {
    return (
      <div className="min-h-screen flex flex-col items-center justify-center p-4">
        <div className="bg-card border border-border/60 shadow-sm p-8 sm:p-12 rounded-none max-w-md w-full text-center">
          <svg className="h-8 w-8 text-rose-500 mx-auto mb-6" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <circle cx="12" cy="12" r="10"></circle>
            <line x1="12" y1="8" x2="12" y2="12"></line>
            <line x1="12" y1="16" x2="12.01" y2="16"></line>
          </svg>
          <h1 className="text-xl font-semibold text-foreground mb-3">Invalid Session</h1>
          <p className="text-sm text-muted-foreground leading-relaxed">
            We could not verify your session. Please check your email for access links or contact support if you completed a payment.
          </p>
        </div>
      </div>
    );
  }

  if (status === "VERIFYING") {
    return (
      <div className="min-h-screen flex flex-col items-center justify-center p-4">
        <div className="bg-card border border-border/60 shadow-sm p-8 sm:p-12 rounded-none max-w-md w-full text-center">
          <svg className="animate-spin h-8 w-8 text-muted-foreground mx-auto mb-6" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M21 12a9 9 0 1 1-6.219-8.56" />
          </svg>
          <h1 className="text-xl font-semibold text-foreground mb-3">Verifying Transaction...</h1>
          <p className="text-sm text-muted-foreground leading-relaxed">
            Please wait while we securely verify your transaction with the payment provider.
          </p>
        </div>
      </div>
    );
  }

  if (status === "TIMEOUT") {
    return (
      <div className="min-h-screen flex flex-col items-center justify-center p-4">
        <div className="bg-card border border-border/60 shadow-sm p-8 sm:p-12 rounded-none max-w-md w-full text-center">
          <div className="flex items-center justify-center w-16 h-16 bg-amber-50 dark:bg-amber-950/30 rounded-full mx-auto mb-6">
            <svg className="h-8 w-8 text-amber-600 dark:text-amber-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          </div>
          <h1 className="text-xl font-semibold text-foreground mb-3">Processing Payment</h1>
          <p className="text-sm text-muted-foreground mb-8 leading-relaxed">
            We are still processing your payment for <strong className="text-foreground">{product.name}</strong>. Please check your email in a few minutes for your receipt and access links.
          </p>
          <Link href={`/${tenantSlug}/portal`} className="block w-full">
            <button className="w-full h-12 text-sm font-bold tracking-wide uppercase border border-border bg-background hover:bg-accent text-foreground rounded-none transition-colors">
              Go to Dashboard
            </button>
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="min-h-screen flex flex-col items-center justify-center p-4">
      <div className="bg-card border border-border/60 shadow-sm p-8 sm:p-12 rounded-none max-w-md w-full text-center">
        <div className="flex items-center justify-center w-16 h-16 bg-emerald-50 dark:bg-emerald-950/30 rounded-full mx-auto mb-6">
          <svg 
            className="h-8 w-8 text-emerald-600 dark:text-emerald-500" 
            fill="none" 
            viewBox="0 0 24 24" 
            stroke="currentColor" 
            strokeWidth={2}
          >
            <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
          </svg>
        </div>
        <h1 className="text-2xl font-semibold text-foreground mb-3">Order Complete!</h1>
        <p className="text-sm text-muted-foreground mb-8 leading-relaxed">
          Your order for <strong className="text-foreground">{product.name}</strong> is confirmed. Please check your email for your receipt, digital downloads, and community access links.
        </p>

        <Link href={accessToken ? `/${tenantSlug}/portal?token=${encodeURIComponent(accessToken)}` : `/${tenantSlug}/portal`} className="block w-full">
          <button className="w-full h-12 text-sm font-bold tracking-wide uppercase bg-foreground text-background hover:bg-foreground/90 rounded-none transition-colors">
            Go to Dashboard
          </button>
        </Link>
      </div>
    </div>
  );
}
