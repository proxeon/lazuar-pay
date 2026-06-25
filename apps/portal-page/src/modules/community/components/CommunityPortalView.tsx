"use client";

import { useState } from "react";
import { browserClient } from "../lib/api";
import { components } from "@repo/api-types-ts";
import { ShieldCheck, MessageCircle, Video, ExternalLink, Loader2 } from "lucide-react";

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
  const [sub, setSub] = useState<CommunitySubscription>(initialSubscription);
  const [isCancelling, setIsCancelling] = useState(false);
  const [error, setError] = useState<string | null>(null);

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

  const displayName = user.name || sub.customer_name;
  const displayEmail = user.email || sub.customer_email;
  const initials = displayName ? displayName.substring(0, 2).toUpperCase() : "ME";

  return (
    <div className="w-full max-w-3xl mx-auto space-y-6">
      
      {/* Tier 1: Identity & Support */}
      <div className="bg-card border border-border/60 shadow-sm rounded-xl p-6">
        <div className="flex items-center gap-4">
          <div className="h-12 w-12 rounded-full bg-foreground text-background flex items-center justify-center font-bold text-sm tracking-widest">
            {initials}
          </div>
          <div>
            <h2 className="text-lg font-bold tracking-tight text-foreground">{displayName}</h2>
            <p className="text-sm text-muted-foreground">{displayEmail}</p>
          </div>
        </div>
        <div className="mt-5 pt-4 border-t border-border/40">
          <p className="text-xs text-muted-foreground flex items-center gap-1.5">
            <ShieldCheck size={14} className="text-emerald-600" />
            Your identity is securely managed by Lazuar. To update your billing email, please contact support.
          </p>
        </div>
      </div>

      {error && (
        <div className="p-4 bg-red-50 border border-red-200 text-red-600 text-sm font-medium rounded-xl">
          {error}
        </div>
      )}

      {/* Tier 2: The Value / Fulfillment */}
      <div className="bg-card border border-border/60 shadow-sm rounded-xl overflow-hidden">
        <div className="p-6 sm:p-8 bg-gradient-to-b from-secondary/30 to-background border-b border-border/40">
          <div className="flex flex-wrap items-center gap-3 mb-2">
            <h1 className="text-2xl sm:text-3xl font-bold tracking-tight text-foreground">{sub.plan_name}</h1>
            {isActive && !isPastDue && <span className="bg-emerald-50 text-emerald-700 border border-emerald-200 text-[10px] font-bold uppercase tracking-widest px-2 py-0.5 rounded-full">Active</span>}
            {isPastDue && <span className="bg-amber-50 text-amber-700 border border-amber-200 text-[10px] font-bold uppercase tracking-widest px-2 py-0.5 rounded-full">Payment Due</span>}
            {isCancelled && <span className="bg-rose-50 text-rose-700 border border-rose-200 text-[10px] font-bold uppercase tracking-widest px-2 py-0.5 rounded-full">Cancelled</span>}
          </div>
          <p className="text-sm text-muted-foreground">
            {isActive ? "Thank you for being a member. Your access is currently active." : "Your subscription is inactive. You no longer have access to community resources."}
          </p>
        </div>

        {isActive && (
          <div className="p-6 sm:p-8">
            <h3 className="text-xs font-bold uppercase tracking-widest text-muted-foreground mb-4">Your Access Links</h3>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              <a href="#" className="flex items-center justify-between p-4 border border-border/60 rounded-xl hover:border-foreground hover:shadow-sm transition-all group">
                <div className="flex items-center gap-3">
                  <div className="h-10 w-10 rounded-full bg-blue-50 dark:bg-blue-900/30 flex items-center justify-center text-blue-600 dark:text-blue-400">
                    <MessageCircle size={18} />
                  </div>
                  <span className="font-semibold text-sm group-hover:underline underline-offset-2">Join Telegram Group</span>
                </div>
                <ExternalLink size={14} className="text-muted-foreground group-hover:text-foreground" />
              </a>
              
              <a href="#" className="flex items-center justify-between p-4 border border-border/60 rounded-xl hover:border-foreground hover:shadow-sm transition-all group">
                <div className="flex items-center gap-3">
                  <div className="h-10 w-10 rounded-full bg-indigo-50 dark:bg-indigo-900/30 flex items-center justify-center text-indigo-600 dark:text-indigo-400">
                    <Video size={18} />
                  </div>
                  <span className="font-semibold text-sm group-hover:underline underline-offset-2">Weekly Zoom Calls</span>
                </div>
                <ExternalLink size={14} className="text-muted-foreground group-hover:text-foreground" />
              </a>
            </div>
            <p className="text-[11px] text-muted-foreground mt-4">
              * Note: Do not share these private links. Access is actively monitored.
            </p>
          </div>
        )}
      </div>

      {/* Tier 3: Billing & Danger Zone */}
      <div className="bg-card border border-border/60 shadow-sm rounded-xl overflow-hidden">
        <div className="p-6 sm:p-8">
          <h3 className="text-xs font-bold uppercase tracking-widest text-muted-foreground mb-6">Billing Overview</h3>
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-6">
            <div>
              <p className="text-[10px] font-bold uppercase tracking-widest text-muted-foreground mb-1">Subscription Price</p>
              <p className="text-xl font-mono text-foreground font-semibold">RM {sub.plan_price.toFixed(2)}</p>
            </div>
            <div>
              <p className="text-[10px] font-bold uppercase tracking-widest text-muted-foreground mb-1">Next Billing Date</p>
              <p className="text-lg font-medium text-foreground">{nextDateStr}</p>
            </div>
            <div>
              <p className="text-[10px] font-bold uppercase tracking-widest text-muted-foreground mb-1">Status</p>
              <p className="text-lg font-medium text-foreground capitalize">{sub.status.replace("_", " ").toLowerCase()}</p>
            </div>
          </div>
        </div>

        {isActive && (
          <div className="p-6 sm:px-8 sm:py-6 bg-secondary/40 border-t border-border/60 flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4">
            <div>
              <h4 className="text-sm font-semibold text-foreground">Cancel Subscription</h4>
              <p className="text-xs text-muted-foreground mt-1">You will retain access until the end of your current billing cycle.</p>
            </div>
            <button 
              onClick={handleCancel} 
              disabled={isCancelling}
              className="shrink-0 h-10 px-5 rounded-lg border border-border/80 bg-background text-xs font-bold uppercase tracking-widest text-muted-foreground hover:border-rose-200 hover:bg-rose-50 hover:text-rose-600 transition-all disabled:opacity-50 flex items-center justify-center gap-2"
            >
              {isCancelling ? <Loader2 size={14} className="animate-spin" /> : "Cancel Plan"}
            </button>
          </div>
        )}
      </div>

    </div>
  );
}
