import { Suspense } from "react";
import { CheckoutSuccessView } from "../../../../../modules/checkout/components/CheckoutSuccessView";

export default async function CustomCheckoutSuccessPage({
  params,
}: {
  params: Promise<{ tenantSlug: string }>;
}) {
  const { tenantSlug } = await params;

  return (
    <Suspense fallback={
      <div className="flex-1 flex flex-col items-center justify-center p-4 w-full">
        <svg className="animate-spin h-8 w-8 text-muted-foreground" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2">
          <path d="M21 12a9 9 0 1 1-6.219-8.56" />
        </svg>
      </div>
    }>
      <CheckoutSuccessView
        tenantSlug={tenantSlug}
        displayName="Payment request"
      />
    </Suspense>
  );
}
