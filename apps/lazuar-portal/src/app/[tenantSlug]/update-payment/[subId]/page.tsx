import { notFound, redirect } from "next/navigation";
import { serverClient } from "../../../../modules/core/lib/server-client";
import { fetchWorkspaceBranding } from "../../../../modules/core/lib/branding";
import { ShieldCheck, AlertTriangle, CreditCard } from "lucide-react";
import Link from "next/link";

export default async function UpdatePaymentPage({
  params,
  searchParams,
}: {
  params: Promise<{ tenantSlug: string; subId: string }>;
  searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
  const { tenantSlug, subId } = await params;
  const resolvedSearchParams = await searchParams;
  const token = resolvedSearchParams.token as string | undefined;
  if (!token) {
    notFound();
  }

  const branding = await fetchWorkspaceBranding(tenantSlug);

  const { data, error } = await serverClient.GET("/public/commerce/checkout/{subId}/arrears", {
    params: { path: { subId }, query: { token } },
    next: { revalidate: 0 }
  });

  if (error || !data) {
    notFound();
  }

  const reminderOnly = Boolean((data as { is_reminder_only?: boolean }).is_reminder_only);
  const isPastDue = data.status === "PAST_DUE" || data.status === "SUSPENDED";
  const isSuspended = data.status === "SUSPENDED";
  const isActive = data.status === "ACTIVE";

  async function handleUpdatePayment() {
    "use server";
    const { data: checkoutData, error: checkoutError } = await serverClient.POST(
      "/public/commerce/checkout/{subId}/update-payment",
      { params: { path: { subId }, query: { token } } },
    );

    if (checkoutData?.url) {
      redirect(checkoutData.url);
    }

    if (checkoutError) {
      redirect(`/${tenantSlug}/update-payment/${subId}?token=${token}&err=1`);
    }
  }

  return (
    <div className="min-h-screen flex flex-col items-center justify-center p-4 bg-zinc-50 dark:bg-black font-sans text-foreground">
      <div className="bg-card border border-border/60 shadow-sm p-8 sm:p-12 rounded-none max-w-md w-full">
        {(branding?.logo_url || branding?.name) && (
          <div className="flex items-center justify-center mb-6">
            {branding.logo_url ? (
              // eslint-disable-next-line @next/next/no-img-element
              <img src={branding.logo_url} alt={branding.name} className="max-h-8 object-contain" />
            ) : (
              <p className="text-sm font-semibold">{branding.name}</p>
            )}
          </div>
        )}

        {isActive && reminderOnly ? (
          <div className="text-center space-y-6">
            <h1 className="text-xl font-semibold text-foreground mb-2">Invoice each cycle</h1>
            <p className="text-sm text-muted-foreground leading-relaxed">
              {data.product_name} is paid by invoice each cycle. We will email the next Billplz link.
              There is no card on file to update.
            </p>
            <Link href={token ? `/${tenantSlug}/portal?token=${encodeURIComponent(token)}` : `/${tenantSlug}/portal`} className="block w-full">
              <button className="w-full h-12 text-xs font-bold tracking-widest uppercase bg-foreground text-background hover:bg-foreground/90 rounded-none transition-colors">
                Go to Dashboard
              </button>
            </Link>
          </div>
        ) : isActive ? (
          <div className="space-y-8">
            <div className="text-center space-y-4">
              <CreditCard className="h-8 w-8 mx-auto text-muted-foreground" />
              <h1 className="text-xl font-semibold text-foreground">Update how you pay {data.product_name}</h1>
              <p className="text-sm text-muted-foreground leading-relaxed">
                A small verification charge (RM 1) confirms the new method. Your billing date does not change.
              </p>
            </div>
            <form action={handleUpdatePayment}>
              <button
                type="submit"
                className="w-full h-14 text-sm font-bold tracking-wide uppercase text-background rounded-none transition-colors"
                style={{ backgroundColor: "var(--brand, var(--foreground))" }}
              >
                Update payment method
              </button>
            </form>
          </div>
        ) : !isPastDue ? (
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
            <Link href={token ? `/${tenantSlug}/portal?token=${encodeURIComponent(token)}` : `/${tenantSlug}/portal`} className="block w-full">
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
                  Payment is due for <strong className="text-foreground">{data.product_name}</strong>.
                  {isSuspended ? " Your access has been temporarily suspended until the balance is cleared." : " Please complete payment to avoid service interruption."}
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
                Complete Payment
              </button>
            </form>
          </div>
        )}
      </div>
    </div>
  );
}
