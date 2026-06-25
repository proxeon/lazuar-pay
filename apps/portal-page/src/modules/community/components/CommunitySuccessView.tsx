"use client";

import { useState, useEffect } from "react";
import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { browserClient, type CommunityPlanDto } from "../lib/api";

interface CommunitySuccessViewProps {
  tenantSlug: string;
  plan: CommunityPlanDto;
}

function parseBracketParams(searchParams: URLSearchParams): Record<string, string> {
  const result: Record<string, string> = {};
  searchParams.forEach((value, key) => {
    const match = key.match(/^([^\[]+)\[([^\]]+)\]$/);
    if (match) {
      result[match[2]] = value;
    } else {
      result[key] = value;
    }
  });
  return result;
}

export function CommunitySuccessView({ tenantSlug, plan }: CommunitySuccessViewProps) {
  const searchParams = useSearchParams();
  const token = searchParams.get("token") || "";
  
  const parsedParams = parseBracketParams(searchParams);
  const transactionId = parsedParams.id || parsedParams.transaction_id || null;

  const [status, setStatus] = useState<"VERIFYING" | "SUCCESS" | "TIMEOUT">("VERIFYING");

  useEffect(() => {
    let attempts = 0;
    const maxAttempts = 5;
    let timeoutId: NodeJS.Timeout;

    const verifyPayment = async () => {
      attempts++;

      try {
        const { data, error } = await browserClient.GET("/public/community/{tenantSlug}/portal", {
          params: { 
            path: { tenantSlug },
            query: { token }
          }
        });

        if (!error && data?.subscription?.status === "ACTIVE") {
          setStatus("SUCCESS");
          return;
        }
      } catch (err) {
      }

      if (attempts >= maxAttempts) {
        setStatus("TIMEOUT");
      } else {
        timeoutId = setTimeout(verifyPayment, 2000);
      }
    };

    verifyPayment();

    return () => clearTimeout(timeoutId);
  }, [tenantSlug, token]);

  if (status === "VERIFYING") {
    return (
      <div className="min-h-screen flex flex-col items-center justify-center p-4">
        <div className="bg-card border border-border/60 shadow-sm p-8 sm:p-12 rounded-none max-w-md w-full text-center">
          <svg className="animate-spin h-8 w-8 text-muted-foreground mx-auto mb-6" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
            <path d="M21 12a9 9 0 1 1-6.219-8.56" />
          </svg>
          <h1 className="text-xl font-semibold text-foreground mb-3">Verifying Payment...</h1>
          <p className="text-sm text-muted-foreground leading-relaxed">
            Please wait while we confirm your transaction with the payment provider.
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
            We are still processing your payment for <strong className="text-foreground">{plan.name}</strong>. Please check your email in a few minutes for your access links.
          </p>
          <Link href={`/${tenantSlug}/community/portal`} className="block w-full">
            <button className="w-full h-12 text-sm font-bold tracking-wide uppercase border border-border bg-background hover:bg-accent text-foreground rounded-none transition-colors">
              Go to Member Portal
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
        <h1 className="text-2xl font-semibold text-foreground mb-3">Payment Successful!</h1>
        <p className="text-sm text-muted-foreground mb-6 leading-relaxed">
          You are now subscribed to <strong className="text-foreground">{plan.name}</strong>. Please check your email and WhatsApp for your private community links and instructions.
        </p>
        
        {transactionId && (
          <div className="mb-8 p-3 bg-secondary/30 border border-border rounded-sm">
            <p className="text-[10px] font-bold uppercase tracking-widest text-muted-foreground mb-1">Transaction Ref</p>
            <p className="text-xs font-mono text-foreground">{transactionId}</p>
          </div>
        )}

        <Link href={`/${tenantSlug}/community/portal`} className="block w-full">
          <button className="w-full h-12 text-sm font-bold tracking-wide uppercase bg-foreground text-background hover:bg-foreground/90 rounded-none transition-colors">
            Go to Member Portal
          </button>
        </Link>
      </div>
    </div>
  );
}
