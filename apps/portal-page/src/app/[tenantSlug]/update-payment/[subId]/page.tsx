import { notFound, redirect } from "next/navigation";
import { serverClient } from "../../../../modules/core/lib/server-client";
import { ShieldCheck, AlertTriangle, CreditCard, Loader2 } from "lucide-react";
import Link from "next/link";

export default async function UpdatePaymentPage({
  params,
}: {
  params: Promise<{ tenantSlug: string; subId: string }>;
}) {
  const { tenantSlug, subId } = await params;

  const { data, error } = await serverClient.GET("/public/commerce/checkout/{subId}/arrears", {
    params: { path: { subId } },
    next: { revalidate: 0 }
  });

  if (error || !data) {
    notFound();
  }

  const isPastDue = data.status === "PAST_DUE" || data.status === "SUSPENDED";
  const isSuspended = data.status === "SUSPENDED";

  async function handleUpdatePayment() {
    "use server";
    const { data: checkoutData } = await serverClient.POST("/public/commerce/checkout/{subId}/update-payment", {
      params: { path: { subId } }
    });

    if (checkoutData?.url) {
      redirect(checkoutData.url);
    }
  }

  return (
    <div className="min-h-screen flex flex-col items-center justify-center p-4 bg-zinc-50 dark:bg-black font-sans text-foreground">
      <div className="bg-card border border-border/60 shadow-sm p-8 sm:p-12 rounded-none max-w-md w-full">
        
        {!isPastDue ? (
          <div className="text-center space-y-6">
            <div className="flex items-center justify-center w-16 h-16 bg-emerald-50 dark:bg-emerald-950/30 rounded-full mx-auto">
              <ShieldCheck className="h-8 w-8 text-emerald-600 dark:text-emerald-500" />
            </div>
            <div>
              <h1 className="text-xl font-semibold text-foreground mb-2">Account in Good Standing</h1>
              <p className="text-sm text-muted-foreground leading-relaxed">
                Your subscription to <strong className="text-foreground">{data.product_name}</strong> is currently active and does not require an immediate payment update.
              </p>
            </div>
            <Link href={`/${tenantSlug}/portal`} className="block w-full">
              <button className="w-full h-12 text-xs font-bold tracking-widest uppercase bg-foreground text-background hover:bg-foreground/90 rounded-none transition-colors">
                Go to Dashboard
              </button>
            </Link>
          </div>
        ) : (
          <div className="space-y-8">
            <div className="text-center space-y-4">
              <div className="flex items-center justify-center w-16 h-16 bg-rose-50 dark:bg-rose-950/30 rounded-full mx-auto">
                <AlertTriangle className="h-8 w-8 text-rose-600 dark:text-rose-500" />
              </div>
              <div>
                <h1 className="text-xl font-semibold text-foreground mb-2">Action Required</h1>
                <p className="text-sm text-muted-foreground leading-relaxed">
                  Your payment for <strong className="text-foreground">{data.product_name}</strong> failed. 
                  {isSuspended ? " Your access has been temporarily suspended until the balance is cleared." : " Please update your payment method to avoid service interruption."}
                </p>
              </div>
            </div>

            <div className="bg-secondary/40 border border-border/60 p-5 rounded-none flex items-center justify-between">
              <div>
                <p className="text-[10px] font-bold uppercase tracking-widest text-muted-foreground mb-1">Amount Due</p>
                <p className="text-xl font-mono font-bold text-foreground">{data.currency} {data.amount.toFixed(2)}</p>
              </div>
              <CreditCard className="text-muted-foreground opacity-50" size={32} />
            </div>

            <form action={handleUpdatePayment}>
              <button 
                type="submit" 
                className="w-full h-14 text-sm font-bold tracking-wide uppercase bg-foreground text-background hover:bg-foreground/90 rounded-none transition-colors flex items-center justify-center gap-2"
              >
                Update Payment Method
              </button>
            </form>
          </div>
        )}
      </div>
    </div>
  );
}
