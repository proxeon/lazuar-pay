"use client";

import { use, useState, useEffect, Suspense } from "react";
import { useSearchParams, useRouter } from "next/navigation";
import Link from "next/link";
import { ArrowLeft, Loader2, LogOut, CheckCircle2, Mail } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { browserClient, type CommunitySubscription } from "@/lib/api-client";

function PortalContent({ tenantSlug }: { tenantSlug: string }) {
  const searchParams = useSearchParams();
  const router = useRouter();
  const token = searchParams.get("token");

  const [isLoading, setIsLoading] = useState(!!token);
  const [sub, setSub] = useState<CommunitySubscription | null>(null);

  // Login Form State
  const [email, setEmail] = useState("");
  const [isSending, setIsSending] = useState(false);
  const [sent, setSent] = useState(false);

  useEffect(() => {
    if (token) {
      browserClient.GET("/public/community/{tenantSlug}/portal", {
        params: { path: { tenantSlug }, query: { token } }
      })
      .then(({ data, error }) => {
        if (error || !data) {
          toast.error("Invalid or expired access link.");
          router.replace(`/${tenantSlug}/portal`);
        } else {
          setSub(data.subscription);
        }
      })
      .finally(() => setIsLoading(false));
    }
  }, [token, tenantSlug, router]);

  const handleRequestLink = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSending(true);
    const { error } = await browserClient.POST("/public/community/{tenantSlug}/portal/magic-link", {
      params: { path: { tenantSlug } },
      body: { email }
    });

    setIsSending(false);

    if (error) {
      toast.error(error.detail || "Failed to send link");
    } else {
      setSent(true);
      toast.success("Login link sent to your email!");
    }
  };

  const handleUpdateContact = async (e: React.FormEvent) => {
    e.preventDefault();
    toast.success("Note: Contact updates must be done via your account manager at this time.");
  };

  const handleCancel = async () => {
    if (!token || !sub) return;
    if (!window.confirm("Are you sure you want to cancel your subscription? You will lose access at the end of your billing cycle.")) return;
    
    const { error } = await browserClient.POST("/public/community/{tenantSlug}/portal/cancel", {
      params: { path: { tenantSlug }, query: { token } },
      body: { subscription_id: sub.id }
    });

    if (error) {
      toast.error(error.detail || "Failed to cancel subscription.");
    } else {
      toast.success("Subscription cancelled successfully.");
      setSub(prev => prev ? { ...prev, status: "CANCELLED" } : null);
    }
  };

  if (isLoading) {
    return <div className="min-h-screen flex items-center justify-center"><Loader2 className="animate-spin h-8 w-8 text-muted-foreground" /></div>;
  }

  // ─── LOGIN VIEW ──────────────────────────────────────────────────────────
  if (!token || !sub) {
    return (
      <div className="min-h-screen flex flex-col bg-zinc-50 dark:bg-black items-center justify-center p-4">
        <div className="bg-card border border-border/60 shadow-sm p-8 sm:p-12 rounded-none max-w-md w-full">
          <div className="flex items-center justify-center w-12 h-12 bg-secondary/50 rounded-full mx-auto mb-6">
            <Mail className="h-5 w-5 text-foreground" />
          </div>
          <h1 className="text-xl font-semibold text-foreground mb-2 text-center">Subscriber Portal</h1>
          <p className="text-sm text-muted-foreground mb-8 text-center leading-relaxed">
            Enter your email to receive a secure, passwordless login link to manage your subscriptions.
          </p>

          {sent ? (
            <div className="p-4 bg-emerald-50 dark:bg-emerald-950/30 border border-emerald-200 dark:border-emerald-900 text-center">
              <CheckCircle2 className="h-6 w-6 text-emerald-600 mx-auto mb-2" />
              <p className="text-sm font-medium text-emerald-800 dark:text-emerald-400">Check your inbox. We've sent you a login link.</p>
            </div>
          ) : (
            <form onSubmit={handleRequestLink} className="space-y-4">
              <input 
                type="email" 
                required 
                value={email}
                onChange={e => setEmail(e.target.value)}
                placeholder="e.g. customer@example.com" 
                className="flex h-12 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring" 
              />
              <Button type="submit" disabled={isSending || !email} className="w-full h-12 text-sm font-bold tracking-wide uppercase bg-foreground text-background hover:bg-foreground/90 rounded-none">
                {isSending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : "Send Magic Link"}
              </Button>
            </form>
          )}

          <div className="mt-8 text-center">
            <Link href={`/${tenantSlug}`} className="text-xs font-semibold uppercase tracking-widest text-muted-foreground hover:text-foreground">
              ← Return to Catalog
            </Link>
          </div>
        </div>
      </div>
    );
  }

  // ─── DASHBOARD VIEW ──────────────────────────────────────────────────────
  const isActive = sub.status === "ACTIVE" || sub.status === "PAST_DUE" || sub.status === "GRACE_PERIOD";
  const isCancelled = sub.status === "CANCELLED" || sub.status === "CANCELED";
  const isPastDue = sub.status === "PAST_DUE" || sub.status === "GRACE_PERIOD";
  const nextDateStr = sub.next_billing_date ? new Date(sub.next_billing_date).toLocaleDateString("en-MY", { year: 'numeric', month: 'short', day: 'numeric' }) : "N/A";

  return (
    <div className="min-h-screen flex flex-col bg-zinc-50 dark:bg-black">
      <header className="sticky top-0 z-40 w-full bg-card border-b border-border/60">
        <div className="max-w-5xl mx-auto px-4 h-14 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Link href={`/${tenantSlug}`} className="inline-flex items-center gap-2 -ml-2 px-2 py-1.5 text-muted-foreground hover:text-foreground transition-all group">
              <ArrowLeft className="h-4 w-4 transition-transform group-hover:-translate-x-0.5" />
              <span className="text-sm font-medium hidden sm:inline">Back to Catalog</span>
            </Link>
          </div>
          <div className="flex items-center gap-4">
            <span className="text-xs font-bold uppercase tracking-widest text-foreground hidden sm:inline">{sub.customer_name}</span>
            <button onClick={() => router.push(`/${tenantSlug}/portal`)} className="text-xs text-muted-foreground hover:text-foreground flex items-center gap-1.5 uppercase font-bold tracking-widest">
              <LogOut size={14} /> Logout
            </button>
          </div>
        </div>
      </header>

      <main className="flex-1 w-full max-w-3xl mx-auto px-4 py-8 md:py-12 space-y-12">
        
        {/* Global Profile Form */}
        <div className="bg-card border border-border/60 shadow-sm p-6 rounded-none">
          <h3 className="text-xs font-bold uppercase tracking-widest text-foreground mb-6">Contact Details</h3>
          <form onSubmit={handleUpdateContact} className="grid grid-cols-1 md:grid-cols-4 gap-4 items-end">
            <div className="space-y-1.5">
              <label className="text-[10px] font-bold uppercase tracking-widest text-muted-foreground">Name</label>
              <input type="text" readOnly value={sub.customer_name} className="flex h-10 w-full rounded-none border border-border/60 bg-secondary/50 px-3 py-1 text-sm focus-visible:outline-none" />
            </div>
            <div className="space-y-1.5">
              <label className="text-[10px] font-bold uppercase tracking-widest text-muted-foreground">Email</label>
              <input type="email" readOnly value={sub.customer_email} className="flex h-10 w-full rounded-none border border-border/60 bg-secondary/50 px-3 py-1 text-sm focus-visible:outline-none" />
            </div>
            <div className="space-y-1.5">
              <label className="text-[10px] font-bold uppercase tracking-widest text-muted-foreground">Phone</label>
              <input type="tel" readOnly value={sub.customer_phone} className="flex h-10 w-full rounded-none border border-border/60 bg-secondary/50 px-3 py-1 text-sm focus-visible:outline-none" />
            </div>
            <Button type="submit" variant="outline" className="w-full rounded-none uppercase font-bold tracking-widest text-xs h-10">Request Edit</Button>
          </form>
        </div>

        {/* Subscription Status Card */}
        <div className="space-y-6">
          <div className="flex items-center gap-3">
            <h2 className="text-xl font-bold tracking-tight text-foreground">{sub.plan_name}</h2>
            {isActive && !isPastDue && <span className="bg-emerald-50 text-emerald-700 border border-emerald-200 text-[10px] font-bold uppercase tracking-widest px-2 py-0.5">Active</span>}
            {isPastDue && <span className="bg-amber-50 text-amber-700 border border-amber-200 text-[10px] font-bold uppercase tracking-widest px-2 py-0.5">Payment Due</span>}
            {isCancelled && <span className="bg-rose-50 text-rose-700 border border-rose-200 text-[10px] font-bold uppercase tracking-widest px-2 py-0.5">Cancelled</span>}
          </div>

          <div className="bg-card border border-border/60 shadow-sm p-6 rounded-none space-y-6">
            <div>
              <p className="text-sm font-mono text-muted-foreground mb-1">RM {sub.plan_price.toFixed(2)}</p>
            </div>

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
                <div className="text-center p-3 bg-secondary/50 border border-border/60 text-xs text-muted-foreground">
                  Renewal links are sent to your email and WhatsApp 3 days before your billing date.
                </div>
              )}
              {isActive && (
                <button onClick={handleCancel} className="text-xs font-bold uppercase tracking-widest text-rose-600 hover:text-rose-700 border border-transparent hover:border-rose-200 hover:bg-rose-50 px-4 py-3 transition-colors text-center w-full">
                  Cancel Subscription
                </button>
              )}
              {isCancelled && (
                <div className="text-center p-3 bg-secondary/50 border border-border/60 text-xs text-muted-foreground">
                  Access remains until {sub.current_period_end ? new Date(sub.current_period_end).toLocaleDateString("en-MY") : "end of term"}.
                </div>
              )}
            </div>
          </div>
        </div>

      </main>
    </div>
  );
}

export default function PortalPage({ params }: { params: Promise<{ tenantSlug: string }> }) {
  const resolvedParams = use(params);
  return (
    <Suspense fallback={<div className="min-h-screen flex items-center justify-center"><Loader2 className="animate-spin h-8 w-8 text-muted-foreground" /></div>}>
      <PortalContent tenantSlug={resolvedParams.tenantSlug} />
    </Suspense>
  );
}
