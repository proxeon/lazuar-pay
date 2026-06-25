// apps/portal-page/src/modules/community/components/CommunityPortalView.tsx
"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { browserClient } from "../lib/api";
import { components } from "@repo/api-types-ts";

type CommunitySubscription = components["schemas"]["Community.CommunitySubscriptionDto"];
type AuthUser = components["schemas"]["One.AuthUser"];

interface CommunityPortalViewProps {
  tenantSlug: string;
  subscription: CommunitySubscription;
  user: AuthUser;
  token: string;
}

export function cn(...classes: (string | undefined | null | false)[]) {
  return classes.filter(Boolean).join(" ");
}

export function CommunityPortalView({ tenantSlug, subscription: initialSubscription, user, token }: CommunityPortalViewProps) {
  const router = useRouter();
  const [sub, setSub] = useState<CommunitySubscription>(initialSubscription);
  const [isCancelling, setIsCancelling] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleUpdateContact = async (e: React.FormEvent) => {
    e.preventDefault();
    alert("Note: Global identity updates must be done via Lazuar Ops.");
  };

  const handleCancel = async () => {
    if (!window.confirm("Are you sure you want to cancel your subscription? You will lose access at the end of your billing cycle.")) {
      return;
    }
    
    setIsCancelling(true);
    setError(null);

    try {
      const { error: apiError } = await browserClient.POST("/public/community/{tenantSlug}/portal/cancel", {
        params: { path: { tenantSlug }, query: { token } },
        body: { subscription_id: sub.id }
      });

      if (apiError) throw new Error(apiError.detail || "Failed to cancel subscription.");

      setSub(prev => ({ ...prev, status: "CANCELLED" }));
    } catch (err: any) {
      setError(err.message);
    } finally {
      setIsCancelling(false);
    }
  };

  const isActive = sub.status === "ACTIVE" || sub.status === "PAST_DUE" || sub.status === "GRACE_PERIOD";
  const isCancelled = sub.status === "CANCELLED" || sub.status === "CANCELED";
  const isPastDue = sub.status === "PAST_DUE" || sub.status === "GRACE_PERIOD";
  const nextDateStr = sub.next_billing_date 
    ? new Date(sub.next_billing_date).toLocaleDateString("en-MY", { year: 'numeric', month: 'short', day: 'numeric' }) 
    : "N/A";

  return (
    <div className="space-y-12">
      <div className="bg-card border border-border/60 shadow-sm p-6 rounded-none">
        <h3 className="text-xs font-bold uppercase tracking-widest text-foreground mb-6">Contact Details</h3>
        <form onSubmit={handleUpdateContact} className="grid grid-cols-1 md:grid-cols-4 gap-4 items-end">
          <div className="space-y-1.5">
            <label className="text-[10px] font-bold uppercase tracking-widest text-muted-foreground">Name</label>
            <input 
              type="text" 
              readOnly 
              value={user.name || sub.customer_name} 
              className="flex h-10 w-full rounded-none border border-border/60 bg-secondary/50 px-3 py-1 text-sm focus-visible:outline-none" 
            />
          </div>
          <div className="space-y-1.5 md:col-span-2">
            <label className="text-[10px] font-bold uppercase tracking-widest text-muted-foreground">Email (Global Identity)</label>
            <input 
              type="email" 
              readOnly 
              value={user.email || sub.customer_email} 
              className="flex h-10 w-full rounded-none border border-border/60 bg-secondary/50 px-3 py-1 text-sm focus-visible:outline-none" 
            />
          </div>
          <button 
            type="submit" 
            className="w-full h-10 rounded-none border border-border/60 bg-background text-[10px] font-bold uppercase tracking-widest text-foreground hover:bg-secondary transition-colors"
          >
            Manage in Ops
          </button>
        </form>
      </div>

      <div className="space-y-6">
        <div className="flex flex-wrap items-center gap-3">
          <h2 className="text-xl font-bold tracking-tight text-foreground">{sub.plan_name}</h2>
          {isActive && !isPastDue && <span className="bg-emerald-50 text-emerald-700 border border-emerald-200 text-[10px] font-bold uppercase tracking-widest px-2 py-0.5">Active</span>}
          {isPastDue && <span className="bg-amber-50 text-amber-700 border border-amber-200 text-[10px] font-bold uppercase tracking-widest px-2 py-0.5">Payment Due</span>}
          {isCancelled && <span className="bg-rose-50 text-rose-700 border border-rose-200 text-[10px] font-bold uppercase tracking-widest px-2 py-0.5">Cancelled</span>}
        </div>

        {error && (
          <div className="p-3 bg-red-50 border border-red-200 text-red-600 text-sm font-medium">
            {error}
          </div>
        )}

        <div className="bg-card border border-border/60 shadow-sm p-6 rounded-none space-y-6">
          <p className="text-sm font-mono text-muted-foreground mb-1">RM {sub.plan_price.toFixed(2)}</p>

          <div className="grid grid-cols-2 gap-4 pb-6 border-b border-border/40">
              <div>
                <p className="text-[10px] font-bold uppercase tracking-widest text-muted-foreground mb-1">Next Billing</p>
                <p className="text-sm font-medium text-foreground">{nextDateStr}</p>
              </div>
              <div>
                <p className="text-[10px] font-bold uppercase tracking-widest text-muted-foreground mb-1">Status</p>
                <p className="text-sm font-medium text-foreground">{sub.status.replace("_", " ")}</p>
              </div>
          </div>

          <div className="flex flex-col gap-3 max-w-sm">
            {isActive && (
              <button 
                onClick={handleCancel} 
                disabled={isCancelling}
                className="text-xs font-bold uppercase tracking-widest text-rose-600 hover:text-rose-700 border border-transparent hover:border-rose-200 hover:bg-rose-50 px-4 py-3 transition-colors text-center w-full disabled:opacity-50 flex items-center justify-center gap-2"
              >
                {isCancelling && (
                  <svg className="animate-spin h-3 w-3" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round">
                    <path d="M21 12a9 9 0 1 1-6.219-8.56" />
                  </svg>
                )}
                Cancel Subscription
              </button>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
