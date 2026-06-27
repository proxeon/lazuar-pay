// apps/portal-page/src/modules/checkout/components/OrderSummaryCard.tsx
import { ReactNode } from "react";
import { CheckoutContext } from "../types";

export function cn(...classes: (string | undefined | null | false)[]) {
  return classes.filter(Boolean).join(" ");
}

interface OrderSummaryCardProps {
  context: CheckoutContext;
  promoCodeSlot?: ReactNode;
}

export function OrderSummaryCard({ context, promoCodeSlot }: OrderSummaryCardProps) {
  const finalPriceToDisplay = context.finalPrice !== null ? context.finalPrice : context.price;
  const hasVault = context.fulfillmentTargets.includes("internal:vault");
  const hasCommunity = context.fulfillmentTargets.includes("internal:community");

  return (
    <div className="border border-border/60 bg-card p-6 shadow-sm rounded-none space-y-4">
      <h3 className="text-xs font-bold uppercase tracking-widest text-muted-foreground">Order Summary</h3>
      
      <div className="pb-4 border-b border-border/40">
        <h4 className="text-lg font-semibold text-foreground leading-tight mb-1">{context.itemName}</h4>
        {context.audience && <p className="text-sm text-muted-foreground">{context.audience}</p>}
      </div>

      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <span className="text-sm text-muted-foreground">Subtotal</span>
          <span className={cn("text-sm font-medium", context.isCouponApplied ? "line-through text-muted-foreground" : "text-foreground")}>
            {context.currency} {context.price.toFixed(2)}
          </span>
        </div>
        
        {context.isCouponApplied && context.discountAmount !== null && (
          <div className="flex items-center justify-between text-emerald-600 dark:text-emerald-400">
            <span className="text-sm font-medium">Discount</span>
            <span className="text-sm font-bold">- {context.currency} {context.discountAmount.toFixed(2)}</span>
          </div>
        )}
      </div>

      {(hasVault || hasCommunity) && (
        <div className="pt-2 space-y-2">
          {hasVault && (
            <div className="flex items-center gap-2 text-xs font-medium text-emerald-700 dark:text-emerald-400 bg-emerald-50 dark:bg-emerald-950/30 border border-emerald-200 dark:border-emerald-900/50 px-3 py-2">
              <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M4 16v1a3 3 0 003 3h10a3 3 0 003-3v-1m-4-4l-4 4m0 0l-4-4m4 4V4" />
              </svg>
              Includes Digital Download
            </div>
          )}
          {hasCommunity && (
            <div className="flex items-center gap-2 text-xs font-medium text-blue-700 dark:text-blue-400 bg-blue-50 dark:bg-blue-950/30 border border-blue-200 dark:border-blue-900/50 px-3 py-2">
              <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
                <path strokeLinecap="round" strokeLinejoin="round" d="M12 4.354a4 4 0 110 5.292M15 21H3v-1a6 6 0 0112 0v1zm0 0h6v-1a6 6 0 00-9-5.197M13 7a4 4 0 11-8 0 4 4 0 018 0z" />
              </svg>
              Includes Private Community Access
            </div>
          )}
        </div>
      )}

      {promoCodeSlot && (
        <div className="pt-4 border-t border-border/40">
          {promoCodeSlot}
        </div>
      )}

      <div className="bg-secondary/40 border border-border/60 p-4 rounded-none mt-4">
        <div className="flex items-center justify-between">
          <span className="text-base font-semibold text-foreground">Total Due Today</span>
          <span className="text-xl font-bold tracking-tighter text-foreground">
            {context.currency} {finalPriceToDisplay.toFixed(2)}
          </span>
        </div>
      </div>
    </div>
  );
}
