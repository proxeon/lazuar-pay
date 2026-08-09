// apps/lazuar-portal/src/modules/checkout/components/CheckoutLayout.tsx
import { ReactNode } from "react";

interface CheckoutLayoutProps {
  formSlot: ReactNode;
  summarySlot: ReactNode;
}

export function CheckoutLayout({ formSlot, summarySlot }: CheckoutLayoutProps) {
  return (
    <div className="flex flex-col-reverse lg:flex-row gap-6 items-start">
      <div className="flex-1 w-full bg-card border border-border/60 shadow-sm p-6 sm:p-8 rounded-none">
        {formSlot}
      </div>
      <div className="w-full lg:w-[380px] shrink-0">
        {summarySlot}
      </div>
    </div>
  );
}
