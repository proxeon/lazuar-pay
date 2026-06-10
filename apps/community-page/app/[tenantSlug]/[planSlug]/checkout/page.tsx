"use client";

import { use, useState, useEffect } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ArrowLeft, LockKeyhole, Loader2, AlertCircle } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { browserClient, type CommunityPlan, type EntitlementDto } from "@/lib/api-client";

export default function CheckoutPage({ params }: { params: Promise<{ tenantSlug: string; planSlug: string }> }) {
  const resolvedParams = use(params);
  const { tenantSlug, planSlug } = resolvedParams;
  const router = useRouter();

  const [pkg, setPkg] = useState<CommunityPlan | null>(null);
  const [globalUser, setGlobalUser] = useState<any>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isFull, setIsFull] = useState(false);

  const [isGuestMode, setIsGuestMode] = useState(false);
  const [isAdminOfTenant, setIsAdminOfTenant] = useState(false);

  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [phone, setPhone] = useState("");

  useEffect(() => {
    async function loadCheckoutData() {
      try {
        const { data: planData, error: planError } = await browserClient.GET("/public/community/{tenantSlug}/plans/{slug}", {
          params: { path: { tenantSlug, slug: planSlug } }
        });
        
        if (planError || !planData) {
          router.replace(`/${tenantSlug}`);
          return;
        }
        
        setPkg(planData);
        if (planData.is_full) setIsFull(true);

        const { data: authData } = await browserClient.GET("/one/auth/me");
        if (authData) {
          setGlobalUser(authData);
          setName(authData.name);
          setEmail(authData.email);

          const { data: entitlements } = await browserClient.GET("/one/me/entitlements");
          if (entitlements) {
            const isAdmin = entitlements.some(
              (e: EntitlementDto) => e.workspace_slug === tenantSlug && (e.role === "ADMIN" || e.role === "SUPER_ADMIN")
            );
            setIsAdminOfTenant(isAdmin);
          }
        }
      } catch (err) {
        console.error("Failed to load checkout data", err);
      } finally {
        setIsLoading(false);
      }
    }

    loadCheckoutData();
  }, [tenantSlug, planSlug, router]);

  const enableGuestMode = () => {
    setIsGuestMode(true);
    setName("");
    setEmail("");
  };

  const disableGuestMode = () => {
    setIsGuestMode(false);
    if (globalUser) {
      setName(globalUser.name);
      setEmail(globalUser.email);
    }
  };

  const handleSubmit = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setIsSubmitting(true);
    
    const { data, error } = await browserClient.POST("/public/community/checkout", {
      body: {
        tenant_slug: tenantSlug,
        plan_slug: planSlug,
        name: name,
        email: email,
        phone: phone,
        is_guest_checkout: isGuestMode
      }
    });

    if (error) {
      toast.error("Checkout Failed", { description: error.detail || "An error occurred" });
      setIsSubmitting(false);
    } else if (data) {
      toast.success("Details secured.", { description: "Routing to secure payment gateway..." });
      window.location.href = data.url;
    }
  };

  if (isLoading || !pkg) {
    return <div className="min-h-screen flex items-center justify-center"><Loader2 className="animate-spin h-8 w-8 text-muted-foreground" /></div>;
  }

  if (isFull) {
     return (
       <div className="min-h-screen flex flex-col bg-zinc-50 dark:bg-black items-center justify-center">
         <div className="bg-card border border-border/60 shadow-sm p-8 sm:p-12 rounded-none max-w-lg text-center">
            <AlertCircle className="h-6 w-6 text-red-500 mx-auto mb-4" />
            <h1 className="text-xl font-semibold mb-2">Program is Full</h1>
            <p className="text-sm text-muted-foreground mb-6">This program is currently not accepting new enrollments.</p>
            <Link href={`/${tenantSlug}/${planSlug}`}><Button variant="outline" className="rounded-none">Back to Program</Button></Link>
         </div>
       </div>
     );
  }

  return (
    <div className="min-h-screen flex flex-col bg-zinc-50 dark:bg-black">
      <header className="sticky top-0 z-40 w-full bg-card border-b border-border/60">
        <div className="max-w-5xl mx-auto px-4 h-14 flex items-center justify-between">
          <Link href={`/${tenantSlug}/${planSlug}`} className="inline-flex items-center gap-2 -ml-2 px-2 py-1.5 text-muted-foreground hover:text-foreground transition-all">
            <ArrowLeft className="h-4 w-4" />
            <span className="text-sm font-medium">Back</span>
          </Link>
          <div className="flex items-center gap-1.5 text-muted-foreground"><LockKeyhole className="h-3.5 w-3.5" /><span className="text-xs font-semibold uppercase tracking-widest">Secure Checkout</span></div>
        </div>
      </header>

      <main className="flex-1 w-full max-w-5xl mx-auto px-4 py-8 md:py-16">
        <div className="mb-8">
          <span className="text-xs font-bold uppercase tracking-widest text-muted-foreground">Step 1 of 2</span>
          <h1 className="text-2xl md:text-3xl font-semibold tracking-tight text-foreground mt-2">Your Details</h1>
        </div>

        <div className="flex flex-col-reverse lg:flex-row gap-6 items-start">
          <div className="flex-1 w-full bg-card border border-border/60 shadow-sm p-6 sm:p-8 rounded-none">
            <form onSubmit={handleSubmit} className="space-y-6">
              
              {globalUser && (
                <div className="mb-4">
                  {isGuestMode ? (
                    <div className="flex items-center justify-between p-3 bg-zinc-100 border border-zinc-200 dark:bg-zinc-900 dark:border-zinc-800">
                      <p className="text-[11px] font-bold uppercase tracking-widest text-zinc-600 dark:text-zinc-400">
                        Checking out as Guest
                      </p>
                      <button type="button" onClick={disableGuestMode} className="text-[11px] font-bold uppercase tracking-widest text-[#09090b] hover:underline dark:text-zinc-300">
                        Use my Lazuar account
                      </button>
                    </div>
                  ) : isAdminOfTenant ? (
                    <div className="flex items-center justify-between p-3 bg-blue-50 border border-blue-200 dark:bg-blue-950/30 dark:border-blue-900">
                      <p className="text-[11px] font-bold uppercase tracking-widest text-blue-700 dark:text-blue-400">
                        Viewing as Workspace Admin
                      </p>
                      <button type="button" onClick={enableGuestMode} className="text-[11px] font-bold uppercase tracking-widest text-blue-700 dark:text-blue-400 hover:underline">
                        Checkout as Guest
                      </button>
                    </div>
                  ) : (
                    <div className="flex items-center justify-between p-3 bg-emerald-50/50 border border-emerald-200 dark:bg-emerald-950/20 dark:border-emerald-900">
                      <p className="text-[11px] font-bold uppercase tracking-widest text-emerald-700 dark:text-emerald-500">
                        ✓ Logged in as {globalUser.name}
                      </p>
                      <button type="button" onClick={enableGuestMode} className="text-[11px] font-bold uppercase tracking-widest text-emerald-700 dark:text-emerald-500 hover:underline">
                        Checkout as Guest
                      </button>
                    </div>
                  )}
                </div>
              )}

              <div className="space-y-2">
                <label htmlFor="name" className="text-sm font-semibold text-foreground">Full Name</label>
                <input 
                  id="name" type="text" required 
                  value={name} onChange={e => setName(e.target.value)}
                  disabled={!!globalUser && !isGuestMode}
                  className="flex h-12 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors disabled:opacity-60 disabled:cursor-not-allowed" 
                />
              </div>
              <div className="space-y-2">
                <label htmlFor="email" className="text-sm font-semibold text-foreground">Email Address</label>
                <input 
                  id="email" type="email" required 
                  value={email} onChange={e => setEmail(e.target.value)}
                  disabled={!!globalUser && !isGuestMode}
                  className="flex h-12 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors disabled:opacity-60 disabled:cursor-not-allowed" 
                />
              </div>
              <div className="space-y-2">
                <label htmlFor="phone" className="text-sm font-semibold text-foreground">WhatsApp Number</label>
                <input 
                  id="phone" type="tel" required 
                  value={phone} onChange={e => setPhone(e.target.value)}
                  className="flex h-12 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors" 
                  placeholder="+60 12-345 6789"
                />
                <p className="text-[11px] text-muted-foreground">Used for weekly class links and reminders.</p>
              </div>
              <div className="pt-4">
                <Button type="submit" size="lg" disabled={isSubmitting} className="w-full h-14 text-sm font-bold tracking-wide uppercase bg-foreground text-background hover:bg-foreground/90 rounded-none">
                  {isSubmitting ? <><Loader2 className="mr-2 h-4 w-4 animate-spin" /> Securing Data...</> : "Proceed to Payment"}
                </Button>
              </div>
            </form>
          </div>

          <div className="w-full lg:w-[380px] shrink-0">
            <div className="border border-border/60 bg-card p-6 shadow-sm rounded-none">
              <h3 className="text-xs font-bold uppercase tracking-widest text-muted-foreground mb-4">Order Summary</h3>
              <div className="pb-4 border-b border-border/40 mb-4">
                <h4 className="text-lg font-semibold text-foreground leading-tight mb-1">{pkg.name}</h4>
                <p className="text-sm text-muted-foreground">{pkg.audience}</p>
              </div>
              <div className="flex items-center justify-between mb-2">
                <span className="text-sm text-muted-foreground">Subtotal</span>
                <span className="text-sm font-medium text-foreground">RM {pkg.price.toFixed(2)}</span>
              </div>
              <div className="bg-secondary/40 border border-border/60 p-4 rounded-none mt-4">
                <div className="flex items-center justify-between">
                  <span className="text-base font-semibold text-foreground">Total Due Today</span>
                  <span className="text-xl font-bold tracking-tighter text-foreground">RM {pkg.price.toFixed(2)}</span>
                </div>
              </div>
            </div>
          </div>
        </div>
      </main>
    </div>
  );
}
