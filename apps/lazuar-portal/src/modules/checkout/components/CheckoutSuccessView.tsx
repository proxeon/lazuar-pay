"use client";

import { useState, useEffect } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { interpolateNodes, useCheckoutT } from "../i18n/CheckoutI18n";
import { getCheckoutStatus, type ProductDto } from "../lib/api";

interface CheckoutSuccessViewProps {
  tenantSlug: string;
  product: ProductDto;
}

const POLL_INTERVAL_MS = 3000;
const MAX_ATTEMPTS = 20;

export function CheckoutSuccessView({ tenantSlug, product }: CheckoutSuccessViewProps) {
  const { t } = useCheckoutT();
  const searchParams = useSearchParams();
  const subId = searchParams.get("sub_id");
  const productName = <strong className="text-foreground">{product.name}</strong>;

  const [status, setStatus] = useState<"VERIFYING" | "SUCCESS" | "TIMEOUT" | "EXPIRED" | "ERROR">("VERIFYING");
  const [accessToken, setAccessToken] = useState<string | null>(null);
  const [pollKey, setPollKey] = useState(0);

  useEffect(() => {
    if (!subId) {
      setStatus("ERROR");
      return;
    }

    let attempts = 0;
    let timeoutId: ReturnType<typeof setTimeout>;
    let cancelled = false;

    const verifyPayment = async () => {
      attempts++;

      try {
        const response = await getCheckoutStatus(tenantSlug, subId);

        if (cancelled) return;

        // Paid only when commerce session is COMPLETED. Never treat ACTIVE / PENDING / EXPIRED as success.
        if (response.status === "COMPLETED") {
          if (response.token) setAccessToken(response.token);
          setStatus("SUCCESS");
          return;
        }

        if (response.status === "EXPIRED") {
          setStatus("EXPIRED");
          return;
        }
      } catch {
        // Swallow and retry — HTTP errors are not paid.
      }

      if (cancelled) return;

      if (attempts >= MAX_ATTEMPTS) {
        setStatus("TIMEOUT");
      } else {
        timeoutId = setTimeout(verifyPayment, POLL_INTERVAL_MS);
      }
    };

    setStatus("VERIFYING");
    verifyPayment();

    return () => {
      cancelled = true;
      clearTimeout(timeoutId);
    };
  }, [subId, tenantSlug, pollKey]);

  if (status === "ERROR") {
    return (
      <div className="flex-1 flex flex-col items-center justify-center p-4 w-full">
        <div className="bg-card border border-border/60 shadow-sm p-6 sm:p-8 md:p-12 rounded-none max-w-md w-full text-center">
          <svg className="h-8 w-8 text-rose-500 mx-auto mb-6" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <circle cx="12" cy="12" r="10"></circle>
            <line x1="12" y1="8" x2="12" y2="12"></line>
            <line x1="12" y1="16" x2="12.01" y2="16"></line>
          </svg>
          <h1 className="text-xl font-semibold text-foreground mb-3">{t("success.invalidTitle")}</h1>
          <p className="text-sm text-muted-foreground leading-relaxed">
            {t("success.invalidBody")}
          </p>
        </div>
      </div>
    );
  }

  if (status === "VERIFYING") {
    return (
      <div className="flex-1 flex flex-col items-center justify-center p-4 w-full">
        <div className="bg-card border border-border/60 shadow-sm p-6 sm:p-8 md:p-12 rounded-none max-w-md w-full text-center">
          <svg className="animate-spin h-8 w-8 text-muted-foreground mx-auto mb-6" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M21 12a9 9 0 1 1-6.219-8.56" />
          </svg>
          <h1 className="text-xl font-semibold text-foreground mb-3">{t("success.verifyingTitle")}</h1>
          <p className="text-sm text-muted-foreground leading-relaxed">
            {t("success.verifyingBody")}
          </p>
        </div>
      </div>
    );
  }

  if (status === "EXPIRED") {
    return (
      <div className="flex-1 flex flex-col items-center justify-center p-4 w-full">
        <div className="bg-card border border-border/60 shadow-sm p-6 sm:p-8 md:p-12 rounded-none max-w-md w-full text-center">
          <svg className="h-8 w-8 text-rose-500 mx-auto mb-6" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <circle cx="12" cy="12" r="10"></circle>
            <line x1="12" y1="8" x2="12" y2="12"></line>
            <line x1="12" y1="16" x2="12.01" y2="16"></line>
          </svg>
          <h1 className="text-xl font-semibold text-foreground mb-3">{t("success.expiredTitle")}</h1>
          <p className="text-sm text-muted-foreground mb-8 leading-relaxed">
            {interpolateNodes(t("success.expiredBody"), { product: productName })}
          </p>
          <Link href={`/${tenantSlug}/checkout/${product.slug}`} className="block w-full">
            <button className="w-full h-12 text-sm font-bold tracking-wide uppercase border border-border bg-background hover:bg-accent text-foreground rounded-none transition-colors">
              {t("success.returnCheckout")}
            </button>
          </Link>
        </div>
      </div>
    );
  }

  if (status === "TIMEOUT") {
    return (
      <div className="flex-1 flex flex-col items-center justify-center p-4 w-full">
        <div className="bg-card border border-border/60 shadow-sm p-6 sm:p-8 md:p-12 rounded-none max-w-md w-full text-center">
          <div className="flex items-center justify-center w-16 h-16 bg-amber-50 dark:bg-amber-950/30 rounded-full mx-auto mb-6">
            <svg className="h-8 w-8 text-amber-600 dark:text-amber-500" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M12 8v4l3 3m6-3a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
          </div>
          <h1 className="text-xl font-semibold text-foreground mb-3">{t("success.timeoutTitle")}</h1>
          <p className="text-sm text-muted-foreground mb-8 leading-relaxed">
            {interpolateNodes(t("success.timeoutBody"), { product: productName })}
          </p>
          <button
            type="button"
            onClick={() => {
              setStatus("VERIFYING");
              setPollKey((key) => key + 1);
            }}
            className="w-full h-12 text-sm font-bold tracking-wide uppercase border border-border bg-background hover:bg-accent text-foreground rounded-none transition-colors mb-3"
          >
            {t("success.checkAgain")}
          </button>
          <Link href={`/${tenantSlug}/portal`} className="block w-full">
            <button className="w-full h-12 text-sm font-bold tracking-wide uppercase border border-border bg-background hover:bg-accent text-foreground rounded-none transition-colors">
              {t("success.dashboard")}
            </button>
          </Link>
        </div>
      </div>
    );
  }

  return (
    <div className="flex-1 flex flex-col items-center justify-center p-4 w-full">
      <div className="bg-card border border-border/60 shadow-sm p-6 sm:p-8 md:p-12 rounded-none max-w-md w-full text-center">
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
        <h1 className="text-2xl font-semibold text-foreground mb-3">{t("success.completeTitle")}</h1>
        <p className="text-sm text-muted-foreground mb-8 leading-relaxed">
          {interpolateNodes(t("success.completeBody"), { product: productName })}
        </p>

        <Link href={accessToken ? `/${tenantSlug}/portal?token=${encodeURIComponent(accessToken)}` : `/${tenantSlug}/portal`} className="block w-full">
          <button className="w-full h-12 text-sm font-bold tracking-wide uppercase bg-foreground text-background hover:bg-foreground/90 rounded-none transition-colors">
            {t("success.dashboard")}
          </button>
        </Link>
      </div>
    </div>
  );
}
