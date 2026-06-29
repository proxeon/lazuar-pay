import { Suspense } from "react";
import { notFound } from "next/navigation";
import { serverClient } from "../../../../../modules/core/lib/server-client";
import { CheckoutSuccessView } from "../../../../../modules/checkout/components/CheckoutSuccessView";

export default async function UniversalSuccessPage({
  params,
}: {
  params: Promise<{ tenantSlug: string; productSlug: string }>;
}) {
  const { tenantSlug, productSlug } = await params;

  const { data: product, error } = await serverClient.GET("/public/commerce/{tenantSlug}/products/{slug}", {
    params: { path: { tenantSlug, slug: productSlug } },
    next: { revalidate: 60 }
  });

  if (error || !product) {
    notFound();
  }

  return (
    <Suspense fallback={
      <div className="min-h-screen flex items-center justify-center p-4">
        <svg className="animate-spin h-8 w-8 text-muted-foreground" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <path d="M21 12a9 9 0 1 1-6.219-8.56" />
        </svg>
      </div>
    }>
      <CheckoutSuccessView tenantSlug={tenantSlug} product={product} />
    </Suspense>
  );
}
