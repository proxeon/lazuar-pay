import { notFound } from "next/navigation";
import { serverClient } from "../../../../modules/core/lib/server-client";
import { QuoteView } from "../../../../modules/checkout/components/QuoteView";
import { fetchWorkspaceBranding } from "../../../../modules/core/lib/branding";

export default async function CustomPaymentRequestPage({
  params,
  searchParams,
}: {
  params: Promise<{ tenantSlug: string; sessionId: string }>;
  searchParams: Promise<{ [key: string]: string | string[] | undefined }>;
}) {
  const { tenantSlug, sessionId } = await params;
  const isCancelled = (await searchParams).cancelled === "true";

  const { data: checkout, error: checkoutError } = await serverClient.GET("/public/commerce/{tenantSlug}/custom-checkouts/{sessionId}", {
    params: { path: { tenantSlug, sessionId } },
    next: { revalidate: 0 }
  });

  if (checkoutError || !checkout) {
    notFound();
  }

  const branding = await fetchWorkspaceBranding(tenantSlug);

  const { data: profile } = checkout.is_b2b_required
    ? await serverClient.GET("/public/billing/{tenantSlug}/profile", {
        params: { path: { tenantSlug } },
        next: { revalidate: 3600 }
      })
    : { data: undefined };

  return (
    <div className="min-h-screen flex flex-col bg-zinc-50 dark:bg-black font-sans text-foreground py-12 md:py-16 px-4">
      <QuoteView
        tenantSlug={tenantSlug}
        checkout={checkout}
        branding={branding}
        profile={checkout.is_b2b_required ? profile : null}
        isCancelled={isCancelled}
      />
    </div>
  );
}
