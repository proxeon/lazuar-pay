"use client";

import { useState } from "react";
import { browserClient } from "../lib/api";
import { components } from "@repo/api-types-ts";
import { ShieldCheck, MessageCircle, Video, ExternalLink, Loader2, AlertCircle } from "lucide-react";

type PortalSubscription = components["schemas"]["Commerce.PortalSubscriptionDto"];
type AuthUser = components["schemas"]["One.AuthUser"];

interface CommunityPortalViewProps {
  tenantSlug: string;
  subscription: PortalSubscription;
  user: AuthUser;
  token: string;
}

export function cn(...classes: (string | undefined | null | false)[]) {
  return classes.filter(Boolean).join(" ");
}

export function CommunityPortalView({ tenantSlug, subscription: initialSubscription, user, token }: CommunityPortalViewProps) {
  const [sub, setSub] = useState<PortalSubscription>(initialSubscription);
  const [isCancelling, setIsCancelling] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const handleCancel = async () => {
    if (!window.confirm("Are you sure you want to cancel your subscription? You will lose access at the end of your billing cycle.")) {
      return;
    }
    
    setIsCancelling(true);
    setError(null);

    try {
      const { error: apiError } = await browserClient.POST("/public/commerce/{tenantSlug}/portal/cancel", {
        params: { path: { tenantSlug }, query: { token } },
        body: { subscription_id: sub.id }
      });

      if (apiError) throw new Error(apiError.detail || "Failed to cancel subscription.");

      setSub(prev => ({ ...prev, status: "CANCELED" }));
    } catch (err: any) {
      setError(err.message);
    } finally {
      setIsCancelling(false);
    }
  };

  const isActive = sub.status === "ACTIVE" || sub.status === "PAST_DUE" || sub.status === "SUSPENDED";
  const isCancelled = sub.status === "CANCELED";
  const isPastDue = sub.status === "PAST_DUE";
  const nextDateStr = sub.current_period_end
    ? new Date(sub.current_period_end).toLocaleDateString("en-MY", { year: "numeric", month: "short", day: "numeric" })
    : "N/A";

  const displayName = user.name || "Member";
  const displayEmail = user.email || "";

  return (
    <div className="w-full max-w-3xl mx-auto space-y-6">
      
      {/* Checkout-Style Identity Banner */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between p-4 bg-emerald-50/50 border border-emerald-200 dark:bg-emerald-950/20 dark:border-emerald-900 gap-2">
        <p className="text-[11px] font-bold uppercase tracking-widest text-emerald-700 dark:text-emerald-500 flex items-center gap-1.5">
          <ShieldCheck size={14} /> Logged in as {displayName}
        </p>
        <p className="text-[11px] font-medium text-emerald-600 dark:text-emerald-500/80 truncate">
          {displayEmail}
        </p>
      </div>

      {error && (
        <div className="p-4 bg-red-50 border border-red-200 text-red-600 text-sm font-medium flex items-start gap-2">
          <AlertCircle size={16} className="mt-0.5 shrink-0" />
          <span>{error}</span>
        </div>
      )}

      {/* Unified Master Card (Matches CheckoutLayout) */}
      <div className="w-full bg-card border border-border/60 shadow-sm p-6 sm:p-8 rounded-none flex flex-col gap-8">
        
        {/* Hero Section */}
        <div className="pb-6 border-b border-border/40">
          <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 mb-2">
            <h1 className="text-2xl md:text-3xl font-semibold tracking-tight text-foreground leading-tight">
              {sub.product_name}
            </h1>
            <div className="shrink-0">
              {isActive && !isPastDue && <span className="bg-emerald-50 text-emerald-700 border border-emerald-200 text-[10px] font-bold uppercase tracking-widest px-2.5 py-1 rounded-none inline-block">Active</span>}
              {isPastDue && <span className="bg-amber-50 text-amber-700 border border-amber-200 text-[10px] font-bold uppercase tracking-widest px-2.5 py-1 rounded-none inline-block">Payment Due</span>}
              {isCancelled && <span className="bg-rose-50 text-rose-700 border border-rose-200 text-[10px] font-bold uppercase tracking-widest px-2.5 py-1 rounded-none inline-block">Cancelled</span>}
            </div>
          </div>
          <p className="text-sm text-muted-foreground">
            {isActive ? "Thank you for being a member. Your access is currently active." : "Your subscription is inactive. You no longer have access to community resources."}
          </p>
        </div>

        {/* Access Links Section */}
        {isActive && (
          <div className="pb-6 border-b border-border/40 space-y-4">
            <h3 className="text-xs font-bold uppercase tracking-widest text-muted-foreground">Your Access Links</h3>
            
            <div className="flex flex-col gap-3">
              <a href="#" className="flex items-center justify-between p-4 border border-border/60 bg-secondary/20 hover:bg-secondary/50 hover:border-foreground/40 transition-colors group rounded-none">
                <div className="flex items-center gap-3">
                  <MessageCircle size={18} className="text-muted-foreground group-hover:text-blue-600 transition-colors" />
                  <span className="font-semibold text-sm text-foreground">Join Private Telegram Group</span>
                </div>
                <ExternalLink size={14} className="text-muted-foreground group-hover:text-foreground transition-colors" />
              </a>
              
              <a href="#" className="flex items-center justify-between p-4 border border-border/60 bg-secondary/20 hover:bg-secondary/50 hover:border-foreground/40 transition-colors group rounded-none">
                <div className="flex items-center gap-3">
                  <Video size={18} className="text-muted-foreground group-hover:text-indigo-600 transition-colors" />
                  <span className="font-semibold text-sm text-foreground">Weekly Zoom Access</span>
                </div>
                <ExternalLink size={14} className="text-muted-foreground group-hover:text-foreground transition-colors" />
              </a>
            </div>
            
            <p className="text-[11px] text-muted-foreground pt-1">
              * Note: Do not share these private links. Access is actively monitored.
            </p>
          </div>
        )}

        {/* Billing Overview Section */}
        <div className={cn("space-y-4", !isActive && "pb-6 border-b border-border/40")}>
          <h3 className="text-xs font-bold uppercase tracking-widest text-muted-foreground">Billing Overview</h3>
          
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-6 bg-secondary/20 border border-border/60 p-5 rounded-none">
            <div>
              <p className="text-[10px] font-bold uppercase tracking-widest text-muted-foreground mb-1">Product</p>
              <p className="text-lg font-mono text-foreground font-semibold truncate">{sub.product_name}</p>
            </div>
            <div>
              <p className="text-[10px] font-bold uppercase tracking-widest text-muted-foreground mb-1">Next Billing Date</p>
              <p className="text-base font-medium text-foreground">{nextDateStr}</p>
            </div>
            <div>
              <p className="text-[10px] font-bold uppercase tracking-widest text-muted-foreground mb-1">Current Status</p>
              <p className="text-base font-medium text-foreground capitalize">{sub.status.replace("_", " ").toLowerCase()}</p>
            </div>
          </div>
        </div>

        {/* Danger Zone (Cancellation) */}
        {isActive && (
          <div className="pt-2">
            <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 p-5 bg-red-50/30 dark:bg-red-950/10 border border-red-100 dark:border-red-900/50 rounded-none">
              <div>
                <h4 className="text-sm font-semibold text-foreground">Cancel Subscription</h4>
                <p className="text-xs text-muted-foreground mt-1 leading-relaxed">
                  You will retain access to the community until the end of your current billing cycle on {nextDateStr}.
                </p>
              </div>
              <button 
                onClick={handleCancel} 
                disabled={isCancelling}
                className="w-full sm:w-auto shrink-0 h-10 px-6 border border-red-200 dark:border-red-900/80 bg-background text-xs font-bold uppercase tracking-widest text-red-600 dark:text-red-500 hover:bg-red-50 dark:hover:bg-red-950/30 transition-all disabled:opacity-50 flex items-center justify-center gap-2 rounded-none"
              >
                {isCancelling ? <Loader2 size={14} className="animate-spin" /> : "Cancel Plan"}
              </button>
            </div>
          </div>
        )}

      </div>
    </div>
  );
}
