"use client";

import { use, useState, useEffect, Suspense } from "react";
import { useSearchParams } from "next/navigation";
import Link from "next/link";
import { ArrowLeft, Loader2, LogOut } from "lucide-react";
import { toast } from "sonner";
import { browserClient, type CommunitySubscription } from "@/lib/api-client";

function PortalContent({ tenantSlug }: { tenantSlug: string }) {
  const searchParams = useSearchParams();
  const token = searchParams.get("token");

  const [isLoading, setIsLoading] = useState(true);
  const [sub, setSub] = useState<CommunitySubscription | null>(null);
  const [user, setUser] = useState<any>(null);

  useEffect(() => {
    async function checkAuthAndLoad() {
      try {
        const { data: authData, error: authError } = await browserClient.GET("/one/auth/me");
        
        if (authError || !authData) {
          const returnUrl = encodeURIComponent(window.location.href);
          window.location.href = `http://localhost:3003/login?returnUrl=${returnUrl}`;
          return;
        }

        setUser(authData);

        if (token) {
          const { data: portalData } = await browserClient.GET("/public/community/{tenantSlug}/portal", {
            params: { path: { tenantSlug }, query: { token } }
          });
          if (portalData) {
            setSub(portalData.subscription);
          }
        }
      } catch (err) {
        console.error("Failed to authenticate session", err);
      } finally {
        setIsLoading(false);
      }
    }

    checkAuthAndLoad();
  }, [token, tenantSlug]);

  const handleUpdateContact = async (e: React.FormEvent) => {
    e.preventDefault();
    toast.success("Note: Global identity updates must be done via Lazuar Ops.");
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

  const handleLogout = async () => {
    await browserClient.POST("/one/auth/logout");
    window.location.href = "http://localhost:3003/login";
  };

  if (isLoading) {
    return <div className="min-h-screen flex items-center justify-center"><Loader2 className="animate-spin h-8 w-8 text-muted-foreground" /></div>;
  }

  if (!sub) {
    return (
      <div className="min-h-screen flex flex-col bg-zinc-50 dark:bg-black">
        <header className="sticky top-0 z-40 w-full bg-card border-b border-border/60">
          <div className="max-w-5xl mx-auto px-4 h-14 flex items-center justify-between">
            <Link href={`/${tenantSlug}`} className="inline-flex items-center gap-2 -ml-2 px-2 py-1.5 text-muted-foreground hover:text-foreground transition-all">
              <ArrowLeft className="h-4 w-4" />
              <span className="text-sm font-medium">Back to Catalog</span>
            </Link>
            <div className="flex items-center gap-4">
              <span className="text-xs font-bold uppercase tracking-widest text-foreground">{user?.name}</span>
            </div>
          </div>
        </header>
        <main className="flex-1 w-full max-w-3xl mx-auto px-4 py-16 text-center">
          <h1 className="text-2xl font-semibold mb-4 text-foreground">Welcome to the Ecosystem, {user?.name}</h1>
          <p className="text-muted-foreground mb-8 text-sm">Please manage your master identity and active subscriptions from your global launchpad.</p>
          <a href="http://localhost:3003/community/dashboard" className="h-12 px-8 inline-flex items-center bg-foreground text-background text-sm font-bold uppercase tracking-widest rounded-none hover:bg-foreground/90 transition-colors">
            Go to Lazuar Ops
          </a>
        </main>
      </div>
    );
  }

  const isActive = sub.status === "ACTIVE" || sub.status === "PAST_DUE" || sub.status === "GRACE_PERIOD";
  const isCancelled = sub.status === "CANCELLED" || sub.status === "CANCELED";
  const isPastDue = sub.status === "PAST_DUE" || sub.status === "GRACE_PERIOD";
  const nextDateStr = sub.next_billing_date ? new Date(sub.next_billing_date).toLocaleDateString("en-MY", { year: 'numeric', month: 'short', day: 'numeric' }) : "N/A";

  return (
    <div className="min-h-screen flex flex-col bg-zinc-50 dark:bg-black">
      <header className="sticky top-0 z-40 w-full bg-card border-b border-border/60">
        <div className="max-w-5xl mx-auto px-4 h-14 flex items-center justify-between">
          <Link href={`/${tenantSlug}`} className="inline-flex items-center gap-2 -ml-2 px-2 py-1.5 text-muted-foreground hover:text-foreground transition-all">
            <ArrowLeft className="h-4 w-4" />
            <span className="text-sm font-medium hidden sm:inline">Back to Catalog</span>
          </Link>
          <div className="flex items-center gap-4">
            <span className="text-xs font-bold uppercase tracking-widest text-foreground hidden sm:inline">{user?.name}</span>
            <button onClick={handleLogout} className="text-xs text-muted-foreground hover:text-foreground flex items-center gap-1.5 uppercase font-bold tracking-widest">
              <LogOut size={14} /> Logout
            </button>
          </div>
        </div>
      </header>

      <main className="flex-1 w-full max-w-3xl mx-auto px-4 py-8 md:py-12 space-y-12">
        <div className="bg-card border border-border/60 shadow-sm p-6 rounded-none">
          <h3 className="text-xs font-bold uppercase tracking-widest text-foreground mb-6">Contact Details</h3>
          <form onSubmit={handleUpdateContact} className="grid grid-cols-1 md:grid-cols-4 gap-4 items-end">
            <div className="space-y-1.5">
              <label className="text-[10px] font-bold uppercase tracking-widest text-muted-foreground">Name</label>
              <input type="text" readOnly value={user?.name || sub.customer_name} className="flex h-10 w-full rounded-none border border-border/60 bg-secondary/50 px-3 py-1 text-sm focus-visible:outline-none" />
            </div>
            <div className="space-y-1.5 md:col-span-2">
              <label className="text-[10px] font-bold uppercase tracking-widest text-muted-foreground">Email (Global Identity)</label>
              <input type="email" readOnly value={user?.email || sub.customer_email} className="flex h-10 w-full rounded-none border border-border/60 bg-secondary/50 px-3 py-1 text-sm focus-visible:outline-none" />
            </div>
            <button type="submit" className="w-full h-10 rounded-none border border-border/60 bg-background text-[10px] font-bold uppercase tracking-widest text-foreground hover:bg-secondary transition-colors">
              Manage in Ops
            </button>
          </form>
        </div>

        <div className="space-y-6">
          <div className="flex items-center gap-3">
            <h2 className="text-xl font-bold tracking-tight text-foreground">{sub.plan_name}</h2>
            {isActive && !isPastDue && <span className="bg-emerald-50 text-emerald-700 border border-emerald-200 text-[10px] font-bold uppercase tracking-widest px-2 py-0.5">Active</span>}
            {isPastDue && <span className="bg-amber-50 text-amber-700 border border-amber-200 text-[10px] font-bold uppercase tracking-widest px-2 py-0.5">Payment Due</span>}
            {isCancelled && <span className="bg-rose-50 text-rose-700 border border-rose-200 text-[10px] font-bold uppercase tracking-widest px-2 py-0.5">Cancelled</span>}
          </div>

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
                <button onClick={handleCancel} className="text-xs font-bold uppercase tracking-widest text-rose-600 hover:text-rose-700 border border-transparent hover:border-rose-200 hover:bg-rose-50 px-4 py-3 transition-colors text-center w-full">
                  Cancel Subscription
                </button>
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
