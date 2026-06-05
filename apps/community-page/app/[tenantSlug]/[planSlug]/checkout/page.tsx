"use client";

import { use, useState, useEffect } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { ArrowLeft, LockKeyhole, Loader2, AlertCircle } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { browserClient, type CommunityPlan } from "@/lib/api-client";

export default function CheckoutPage({ params }: { params: Promise<{ tenantSlug: string; planSlug: string }> }) {
  const resolvedParams = use(params);
  const { tenantSlug, planSlug } = resolvedParams;
  const router = useRouter();

  const [pkg, setPkg] = useState<CommunityPlan | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isFull, setIsFull] = useState(false);

  useEffect(() => {
    browserClient.GET("/public/community/{tenantSlug}/plans/{slug}", {
      params: { path: { tenantSlug: tenantSlug, slug: planSlug } }
    }).then(({ data, error }) => {
      if (error || !data) {
        router.replace(`/${tenantSlug}`);
        return;
      }
      setPkg(data);
      if (data.is_full) setIsFull(true);
      setIsLoading(false);
    });
  }, [tenantSlug, planSlug, router]);

  const handleSubmit = async (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setIsSubmitting(true);

    const formData = new FormData(e.currentTarget);
    
    const { data, error } = await browserClient.POST("/public/community/checkout", {
      body: {
        tenant_slug: tenantSlug,
        plan_slug: planSlug,
        name: formData.get("name") as string,
        email: formData.get("email") as string,
        phone: formData.get("phone") as string
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
      <div className="min-h-screen flex flex-col bg-zinc-50 dark:bg-black">
        <header className="sticky top-0 z-40 w-full bg-card border-b border-border/60">
          <div className="max-w-5xl mx-auto px-4 h-14 flex items-center justify-between">
            <Link href={`/${tenantSlug}/${planSlug}`} className="inline-flex items-center gap-2 -ml-2 px-2 py-1.5 text-muted-foreground hover:text-foreground transition-all group" aria-label="Go back">
              <ArrowLeft className="h-4 w-4 transition-transform group-hover:-translate-x-0.5" />
              <span className="text-sm font-medium">Back</span>
            </Link>
          </div>
        </header>

        <main className="flex-1 w-full max-w-lg mx-auto px-4 py-16 flex flex-col items-center justify-center text-center">
          <div className="bg-card border border-border/60 shadow-sm p-8 sm:p-12 rounded-none w-full">
            <div className="flex items-center justify-center w-12 h-12 bg-red-50 dark:bg-red-950/30 rounded-full mx-auto mb-6">
              <AlertCircle className="h-6 w-6 text-red-500" />
            </div>
            <h1 className="text-xl font-semibold text-foreground mb-3">Program is Full</h1>
            <p className="text-sm text-muted-foreground mb-6 leading-relaxed">
              <strong>{pkg.name}</strong> has reached maximum capacity and is currently not accepting new enrollments.
              Please check back later or contact us to join the waitlist for the next intake.
            </p>
            <Link href={`/${tenantSlug}/${planSlug}`}><Button variant="outline" className="rounded-none">← Back to Program Details</Button></Link>
          </div>
        </main>
      </div>
    );
  }

  return (
    <div className="min-h-screen flex flex-col bg-zinc-50 dark:bg-black">
      <header className="sticky top-0 z-40 w-full bg-card border-b border-border/60">
        <div className="max-w-5xl mx-auto px-4 h-14 flex items-center justify-between">
          <Link href={`/${tenantSlug}/${planSlug}`} className="inline-flex items-center gap-2 -ml-2 px-2 py-1.5 text-muted-foreground hover:text-foreground transition-all group" aria-label="Go back">
            <ArrowLeft className="h-4 w-4 transition-transform group-hover:-translate-x-0.5" />
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
              <div className="space-y-2">
                <label htmlFor="name" className="text-sm font-semibold text-foreground">Full Name</label>
                <input id="name" name="name" type="text" required placeholder="e.g. Akmal Firdaus" className="flex h-12 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors" />
              </div>
              <div className="space-y-2">
                <label htmlFor="email" className="text-sm font-semibold text-foreground">Email Address</label>
                <input id="email" name="email" type="email" required placeholder="e.g. akmal@lazuar.com" className="flex h-12 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors" />
                <p className="text-[11px] text-muted-foreground">Your receipt and portal access will be sent here.</p>
              </div>
              <div className="space-y-2">
                <label htmlFor="phone" className="text-sm font-semibold text-foreground">WhatsApp Number</label>
                <input id="phone" name="phone" type="tel" required placeholder="+60 12-345 6789" className="flex h-12 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors" />
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
              <div className="flex items-center justify-between mb-6">
                <span className="text-sm text-muted-foreground">Billing Cycle</span>
                <span className="text-sm font-medium text-foreground capitalize">Every {pkg.interval}</span>
              </div>
              <div className="bg-secondary/40 border border-border/60 p-4 rounded-none">
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
