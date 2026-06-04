import Link from "next/link";
import { notFound } from "next/navigation";
import { Check, ArrowLeft, ShieldCheck, Users, AlertCircle } from "lucide-react";
import { Button } from "@/components/ui/button";
import { FaqAccordion } from "@/components/faq-accordion";
import { serverClient, TENANT_SLUG } from "@/lib/api-client";

const GENERAL_FAQ = [
  { id: "faq-cancel", question: "Can I cancel my subscription at any time?", answer: "Yes. You can cancel anytime — no lock-in contracts. Your access remains active until the end of your current billing period." },
  { id: "faq-payment", question: "What payment methods do you accept?", answer: "We accept all major credit/debit cards and online bank transfers (FPX) for Malaysian banks." },
  { id: "faq-access", question: "How do I access the classes after subscribing?", answer: "After payment, you'll receive a WhatsApp message with your private Telegram group invite link and weekly Zoom session link within minutes." },
  { id: "faq-miss", question: "What if I miss a live session?", answer: "All live sessions are recorded. Replays are shared in the private Telegram group within 24 hours of the session." },
  { id: "faq-refund", question: "Is there a refund policy?", answer: "We offer a full refund within the first 7 days if you're not satisfied. After that, you can simply cancel to stop future charges." },
];

export default async function PackageDetailPage({ params }: { params: Promise<{ slug: string }> }) {
  const resolvedParams = await params;
  
  const { data: pkg, error } = await serverClient.GET("/public/community/{tenantSlug}/plans/{slug}", {
    params: { path: { tenantSlug: TENANT_SLUG, slug: resolvedParams.slug } },
    next: { revalidate: 60 }
  });

  if (error || !pkg) {
    notFound();
  }

  const allFaqItems = [...(pkg.faq || []), ...GENERAL_FAQ];
  const showSpots = pkg.spots_remaining !== null && pkg.spots_remaining !== undefined;
  const isFull = pkg.is_full;

  return (
    <div className="min-h-screen flex flex-col bg-zinc-50 dark:bg-black pb-28 lg:pb-0">
      <header className="sticky top-0 z-40 w-full bg-card border-b border-border/60">
        <div className="max-w-5xl mx-auto px-4 h-14 flex items-center">
          <Link href="/" className="inline-flex items-center -ml-2 px-2 py-1.5 hover:opacity-70 transition-opacity">
            <span className="text-[15px] font-bold tracking-tight text-foreground">Lazuar</span>
          </Link>
        </div>
      </header>

      <main className="flex-1 w-full max-w-5xl mx-auto px-4 py-8 md:py-12">
        <div className="flex flex-col lg:flex-row gap-6 items-start">
          <div className="flex-1 w-full bg-card border border-border/60 shadow-sm p-6 sm:p-8 md:p-12 rounded-none flex flex-col gap-12">
            <div>
              <h1 className="text-3xl sm:text-4xl md:text-5xl font-semibold tracking-tight text-foreground leading-[1.1] mb-5 text-balance">{pkg.name}</h1>
              <p className="text-lg md:text-xl text-muted-foreground leading-relaxed text-balance">{pkg.long_description}</p>
            </div>

            {pkg.features && pkg.features.length > 0 && (
              <div>
                <h2 className="text-[11px] font-bold uppercase tracking-[0.2em] text-muted-foreground mb-6">Program Includes</h2>
                <ul className="space-y-5">
                  {pkg.features.map((feature, i) => (
                    <li key={i} className="flex items-start gap-4">
                      <div className="mt-0.5 flex h-5 w-5 shrink-0 items-center justify-center bg-secondary/50 rounded-none border border-border/60">
                        <Check className="h-3.5 w-3.5 text-foreground/80" strokeWidth={3} />
                      </div>
                      <span className="text-base font-medium text-foreground/90 leading-snug">{feature}</span>
                    </li>
                  ))}
                </ul>
              </div>
            )}

            {pkg.methodology && (
              <div className="border border-border/60 bg-zinc-50/50 dark:bg-zinc-900/30 p-6 rounded-none">
                <div className="flex items-center gap-2 mb-3">
                  <ShieldCheck className="h-4 w-4 text-foreground/70" strokeWidth={2.5} />
                  <h3 className="text-xs font-bold uppercase tracking-widest text-foreground">Our Standard</h3>
                </div>
                <p className="text-sm text-muted-foreground leading-relaxed">{pkg.methodology}</p>
              </div>
            )}
          </div>

          <div className="hidden lg:block w-[380px] shrink-0 sticky top-20">
            <div className="border border-border/60 bg-card p-8 shadow-sm rounded-none">
              <div className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground mb-6">Monthly Enrollment</div>
              <div className="flex items-baseline gap-1.5 mb-2">
                <span className="text-xl font-semibold text-muted-foreground">RM</span>
                <span className="text-6xl font-bold tracking-tighter text-foreground">{pkg.price.toFixed(2)}</span>
                <span className="text-sm font-semibold text-muted-foreground ml-1">/ {pkg.interval}</span>
              </div>

              {showSpots && !isFull && (
                <p className="text-sm font-medium text-amber-600 dark:text-amber-400 mb-4 flex items-center gap-1.5">
                  <Users className="h-3.5 w-3.5" /> 🔥 Only {pkg.spots_remaining} spots left
                </p>
              )}
              {isFull && (
                <div className="flex items-center gap-2 mb-4 p-3 bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-900 rounded-none">
                  <AlertCircle className="h-4 w-4 text-red-500 shrink-0" />
                  <p className="text-sm font-medium text-red-600 dark:text-red-400">Enrollment is currently closed.</p>
                </div>
              )}

              <p className="text-sm text-muted-foreground mb-8">Billed automatically. Modify or cancel anytime.</p>

              {isFull ? (
                <Button size="lg" disabled className="w-full h-14 text-sm font-bold tracking-wide uppercase rounded-none opacity-50 cursor-not-allowed">Enrollment Closed</Button>
              ) : (
                <Link href={`/${pkg.slug}/checkout`} className="block w-full">
                  <Button size="lg" className="w-full h-14 text-sm font-bold tracking-wide uppercase bg-foreground text-background hover:bg-foreground/90 rounded-none">Continue</Button>
                </Link>
              )}
            </div>
          </div>
        </div>

        <div className="mt-6 w-full bg-card border border-border/60 shadow-sm p-6 sm:p-8 md:p-12 rounded-none">
          <h2 className="text-lg font-semibold tracking-tight mb-6">Common Questions</h2>
          <FaqAccordion items={allFaqItems} defaultValue={allFaqItems[0]?.id} className="max-w-3xl" />
        </div>
      </main>

      <div className="fixed bottom-0 left-0 right-0 z-50 bg-card border-t border-border/60 p-4 pb-safe lg:hidden shadow-[0_-4px_24px_rgba(0,0,0,0.05)]">
        <div className="flex items-center justify-between gap-4 max-w-5xl mx-auto">
          <div className="flex flex-col justify-center">
            <span className="text-[9px] font-bold uppercase tracking-[0.15em] text-muted-foreground mb-0.5">Monthly</span>
            <div className="flex items-baseline gap-1">
              <span className="text-sm font-semibold text-muted-foreground">RM</span>
              <span className="text-3xl font-bold tracking-tighter text-foreground leading-none">{pkg.price.toFixed(2)}</span>
            </div>
            {showSpots && !isFull && <span className="text-[10px] font-medium text-amber-600 mt-0.5">{pkg.spots_remaining} spots left</span>}
          </div>

          {isFull ? (
            <Button disabled className="h-12 px-8 text-sm font-bold tracking-wide uppercase shrink-0 rounded-none opacity-50 cursor-not-allowed">Closed</Button>
          ) : (
            <Link href={`/${pkg.slug}/checkout`}>
              <Button className="h-12 px-8 text-sm font-bold tracking-wide uppercase shrink-0 shadow-sm bg-foreground text-background hover:bg-foreground/90 rounded-none">Continue</Button>
            </Link>
          )}
        </div>
      </div>
    </div>
  );
}
