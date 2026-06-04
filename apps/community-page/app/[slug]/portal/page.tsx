// apps/community-page/app/[slug]/portal/page.tsx

"use client";

import { use, useState, useEffect, Suspense } from "react";
import { useSearchParams, useRouter } from "next/navigation";
import Link from "next/link";
import { ArrowLeft, Loader2, LogOut, CheckCircle2, CreditCard, Mail } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { requestMagicLink, getPortalData, updatePortalContact, cancelPortalSubscription, PortalData } from "@/lib/api";

function PortalContent({ slug }: { slug: string }) {
  const searchParams = useSearchParams();
  const router = useRouter();
  const token = searchParams.get("token");

  const [isLoading, setIsLoading] = useState(!!token);
  const [data, setData] = useState<PortalData | null>(null);

  // Login Form State
  const [email, setEmail] = useState("");
  const [isSending, setIsSending] = useState(false);
  const [sent, setSent] = useState(false);

  // Update Contact State
  const [editName, setEditName] = useState("");
  const [editEmail, setEditEmail] = useState("");
  const [editPhone, setEditPhone] = useState("");
  const [isUpdating, setIsUpdating] = useState(false);

  useEffect(() => {
    if (token) {
      getPortalData(token)
        .then((res) => {
          setData(res);
          setEditName(res.customer.name);
          setEditEmail(res.customer.email);
          setEditPhone(res.customer.phone);
        })
        .catch(() => {
          toast.error("Invalid or expired access link.");
          router.replace(`/${slug}/portal`);
        })
        .finally(() => setIsLoading(false));
    }
  }, [token, slug, router]);

  const handleRequestLink = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSending(true);
    try {
      await requestMagicLink(email);
      setSent(true);
      toast.success("Login link sent to your email!");
    } catch (err: any) {
      toast.error(err.message || "Failed to send link");
    } finally {
      setIsSending(false);
    }
  };

  const handleUpdateContact = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!token) return;
    setIsUpdating(true);
    try {
      await updatePortalContact(token, { name: editName, email: editEmail, phone: editPhone });
      toast.success("Contact details updated successfully.");
      setData(prev => prev ? { ...prev, customer: { ...prev.customer, name: editName, email: editEmail, phone: editPhone } } : null);
    } catch (err: any) {
      toast.error(err.message || "Failed to update details.");
    } finally {
      setIsUpdating(false);
    }
  };

  const handleCancel = async (subscriptionId: string) => {
    if (!token) return;
    if (!window.confirm("Are you sure you want to cancel your subscription? You will lose access at the end of your billing cycle.")) return;
    try {
      await cancelPortalSubscription(token, subscriptionId);
      toast.success("Subscription cancelled successfully.");
      setData(prev => prev ? { 
        ...prev, 
        subscriptions: prev.subscriptions.map(s => s.id === subscriptionId ? { ...s, status: "CANCELLED" } : s) 
      } : null);
    } catch (err: any) {
      toast.error(err.message || "Failed to cancel subscription.");
    }
  };

  if (isLoading) {
    return <div className="min-h-screen flex items-center justify-center"><Loader2 className="animate-spin h-8 w-8 text-muted-foreground" /></div>;
  }

  // ─── LOGIN VIEW ──────────────────────────────────────────────────────────
  if (!token || !data) {
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
                placeholder="e.g. akmal@lazuar.com" 
                className="flex h-12 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring" 
              />
              <Button type="submit" disabled={isSending || !email} className="w-full h-12 text-sm font-bold tracking-wide uppercase bg-foreground text-background hover:bg-foreground/90 rounded-none">
                {isSending ? <Loader2 className="mr-2 h-4 w-4 animate-spin" /> : "Send Magic Link"}
              </Button>
            </form>
          )}

          <div className="mt-8 text-center">
            <Link href={`/${slug}`} className="text-xs font-semibold uppercase tracking-widest text-muted-foreground hover:text-foreground">
              ← Return to Program
            </Link>
          </div>
        </div>
      </div>
    );
  }

  // ─── DASHBOARD VIEW ──────────────────────────────────────────────────────

  return (
    <div className="min-h-screen flex flex-col bg-zinc-50 dark:bg-black">
      <header className="sticky top-0 z-40 w-full bg-card border-b border-border/60">
        <div className="max-w-5xl mx-auto px-4 h-14 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Link href={`/${slug}`} className="inline-flex items-center gap-2 -ml-2 px-2 py-1.5 text-muted-foreground hover:text-foreground transition-all group">
              <ArrowLeft className="h-4 w-4 transition-transform group-hover:-translate-x-0.5" />
              <span className="text-sm font-medium hidden sm:inline">Back</span>
            </Link>
          </div>
          <div className="flex items-center gap-4">
            <span className="text-xs font-bold uppercase tracking-widest text-foreground hidden sm:inline">{data.customer.name}</span>
            <button onClick={() => router.push(`/${slug}/portal`)} className="text-xs text-muted-foreground hover:text-foreground flex items-center gap-1.5 uppercase font-bold tracking-widest">
              <LogOut size={14} /> Logout
            </button>
          </div>
        </div>
      </header>

      <main className="flex-1 w-full max-w-5xl mx-auto px-4 py-8 md:py-12 space-y-12">
        
        {/* Global Profile Form */}
        <div className="bg-card border border-border/60 shadow-sm p-6 rounded-none">
          <h3 className="text-xs font-bold uppercase tracking-widest text-foreground mb-6">Contact Details</h3>
          <form onSubmit={handleUpdateContact} className="grid grid-cols-1 md:grid-cols-4 gap-4 items-end">
            <div className="space-y-1.5">
              <label className="text-[10px] font-bold uppercase tracking-widest text-muted-foreground">Name</label>
              <input type="text" required value={editName} onChange={e => setEditName(e.target.value)} className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring" />
            </div>
            <div className="space-y-1.5">
              <label className="text-[10px] font-bold uppercase tracking-widest text-muted-foreground">Email</label>
              <input type="email" required value={editEmail} onChange={e => setEditEmail(e.target.value)} className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring" />
            </div>
            <div className="space-y-1.5">
              <label className="text-[10px] font-bold uppercase tracking-widest text-muted-foreground">Phone</label>
              <input type="tel" required value={editPhone} onChange={e => setEditPhone(e.target.value)} className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring" />
            </div>
            <Button type="submit" disabled={isUpdating} variant="outline" className="w-full rounded-none uppercase font-bold tracking-widest text-xs h-10">
              {isUpdating ? <Loader2 className="h-4 w-4 animate-spin" /> : "Save Changes"}
            </Button>
          </form>
        </div>

        {/* Subscriptions Loop */}
        <div className="space-y-12">
          {data.subscriptions.length === 0 ? (
            <div className="text-center py-12 text-muted-foreground">No active subscriptions found.</div>
          ) : (
            data.subscriptions.map(sub => {
              const isActive = sub.status === "ACTIVE" || sub.status === "PAST_DUE" || sub.status === "GRACE_PERIOD";
              const isCancelled = sub.status === "CANCELLED" || sub.status === "CANCELED";
              const isPastDue = sub.status === "PAST_DUE" || sub.status === "GRACE_PERIOD";

              const nextDateStr = sub.next_billing_date 
                ? new Date(sub.next_billing_date).toLocaleDateString("en-MY", { year: 'numeric', month: 'short', day: 'numeric' })
                : "N/A";

              return (
                <div key={sub.id} className="space-y-6">
                  <div className="flex items-center gap-3">
                    <h2 className="text-xl font-bold tracking-tight text-foreground">{sub.plan.name}</h2>
                    {isActive && !isPastDue && <span className="bg-emerald-50 text-emerald-700 border border-emerald-200 text-[10px] font-bold uppercase tracking-widest px-2 py-0.5">Active</span>}
                    {isPastDue && <span className="bg-amber-50 text-amber-700 border border-amber-200 text-[10px] font-bold uppercase tracking-widest px-2 py-0.5">Payment Due</span>}
                    {isCancelled && <span className="bg-rose-50 text-rose-700 border border-rose-200 text-[10px] font-bold uppercase tracking-widest px-2 py-0.5">Cancelled</span>}
                  </div>

                  <div className="grid grid-cols-1 lg:grid-cols-3 gap-8 items-start">
                    
                    {/* Status Card */}
                    <div className="lg:col-span-1 bg-card border border-border/60 shadow-sm p-6 rounded-none space-y-6">
                      <div>
                        <p className="text-sm font-mono text-muted-foreground mb-1">RM {sub.plan.price.toFixed(2)} / {sub.plan.interval}</p>
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

                      <div className="flex flex-col gap-3">
                        {isActive && (
                          <Link href={`/${sub.plan.slug}/checkout`}>
                            <Button className="w-full rounded-none uppercase font-bold tracking-widest text-xs h-12">
                              Make Renewal Payment
                            </Button>
                          </Link>
                        )}
                        {isActive && (
                          <button onClick={() => handleCancel(sub.id)} className="text-xs font-bold uppercase tracking-widest text-rose-600 hover:text-rose-700 border border-transparent hover:border-rose-200 hover:bg-rose-50 px-4 py-3 transition-colors text-center w-full">
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

                    {/* Payment History Table */}
                    <div className="lg:col-span-2 bg-card border border-border/60 shadow-sm rounded-none overflow-hidden">
                      <div className="p-6 border-b border-border/60 flex items-center gap-2">
                         <CreditCard size={16} className="text-muted-foreground" />
                         <h3 className="text-xs font-bold uppercase tracking-widest text-foreground">Payment History</h3>
                      </div>
                      {sub.payments.length === 0 ? (
                        <div className="p-8 text-center text-sm text-muted-foreground">No payment records found.</div>
                      ) : (
                        <div className="overflow-x-auto">
                          <table className="w-full text-sm text-left">
                            <thead className="bg-secondary/50 text-[10px] font-bold uppercase tracking-widest text-muted-foreground">
                              <tr>
                                <th className="px-6 py-3 font-medium">Date</th>
                                <th className="px-6 py-3 font-medium">Amount</th>
                                <th className="px-6 py-3 font-medium">Method</th>
                                <th className="px-6 py-3 font-medium text-right">Status</th>
                              </tr>
                            </thead>
                            <tbody className="divide-y divide-border/40">
                              {sub.payments.map((p) => (
                                <tr key={p.id} className="hover:bg-secondary/20">
                                  <td className="px-6 py-4 whitespace-nowrap text-foreground">
                                    {new Date(p.created_at).toLocaleDateString("en-MY", { year: 'numeric', month: 'short', day: 'numeric' })}
                                  </td>
                                  <td className="px-6 py-4 whitespace-nowrap font-mono font-medium">
                                    RM {p.amount.toFixed(2)}
                                  </td>
                                  <td className="px-6 py-4 whitespace-nowrap text-muted-foreground text-xs uppercase tracking-wider">
                                    {p.payment_method.replace("_", " ")}
                                  </td>
                                  <td className="px-6 py-4 whitespace-nowrap text-right">
                                    <span className={`inline-flex px-2 py-0.5 text-[10px] font-bold uppercase tracking-widest border ${
                                      p.status === "CONFIRMED" ? "bg-emerald-50 text-emerald-700 border-emerald-200" : "bg-secondary text-muted-foreground border-border/60"
                                    }`}>
                                      {p.status}
                                    </span>
                                  </td>
                                </tr>
                              ))}
                            </tbody>
                          </table>
                        </div>
                      )}
                    </div>
                  </div>
                </div>
              );
            })
          )}
        </div>

      </main>
    </div>
  );
}

export default function PortalPage({ params }: { params: Promise<{ slug: string }> }) {
  const resolvedParams = use(params);
  return (
    <Suspense fallback={<div className="min-h-screen flex items-center justify-center"><Loader2 className="animate-spin h-8 w-8 text-muted-foreground" /></div>}>
      <PortalContent slug={resolvedParams.slug} />
    </Suspense>
  );
}
