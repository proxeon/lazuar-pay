import { ReactNode } from "react";
import { CheckoutContext } from "../types";

export function cn(...classes: (string | undefined | null | false)[]) {
  return classes.filter(Boolean).join(" ");
}

interface OrderSummaryCardProps {
  context: CheckoutContext;
  onCustomPriceChange: (price: number) => void;
  onQuantityChange: (qty: number) => void;
  promoCodeSlot?: ReactNode;
}

export function OrderSummaryCard({ context, onCustomPriceChange, onQuantityChange, promoCodeSlot }: OrderSummaryCardProps) {
  const finalPriceToDisplay = context.finalPrice !== null ? context.finalPrice : context.currentPrice;

  const handlePriceInput = (e: React.ChangeEvent<HTMLInputElement>) => {
    const val = parseFloat(e.target.value);
    if (!isNaN(val)) {
      onCustomPriceChange(val);
    }
  };

  const handlePriceBlur = (e: React.FocusEvent<HTMLInputElement>) => {
    const val = parseFloat(e.target.value);
    if (isNaN(val) || val < context.minimumPrice) {
      onCustomPriceChange(context.minimumPrice);
    }
  };

  const handleQuantityInput = (e: React.ChangeEvent<HTMLInputElement>) => {
    let val = parseInt(e.target.value, 10);
    if (isNaN(val) || val < 1) val = 1;
    onQuantityChange(val);
  };

  return (
    <div className="border border-border/60 bg-card p-6 shadow-sm rounded-none space-y-4">
      <h3 className="text-xs font-bold uppercase tracking-widest text-muted-foreground">Order Summary</h3>
      
      <div className="pb-4 border-b border-border/40">
        <h4 className="text-lg font-semibold text-foreground leading-tight mb-1">{context.itemName}</h4>
        {context.audience && <p className="text-sm text-muted-foreground">{context.audience}</p>}
      </div>

      <div className="space-y-4">
        {context.pricingModel === "FIXED" && context.interval === "one_time" && (
          <div className="flex items-center justify-between">
            <span className="text-sm text-muted-foreground">Quantity</span>
            <input 
              type="number" 
              min="1"
              step="1"
              value={context.quantity.toString()}
              onChange={handleQuantityInput}
              className="w-16 h-8 border border-border/60 bg-background px-2 text-sm text-center focus:outline-none focus:border-foreground"
            />
          </div>
        )}

        <div className="flex items-center justify-between">
          <span className="text-sm text-muted-foreground">Subtotal</span>
          {context.pricingModel === "PWYW" && !context.isCouponApplied ? (
             <div className="flex items-center gap-2">
                <span className="text-sm font-medium text-muted-foreground">{context.currency}</span>
                <input 
                  type="number" 
                  min={context.minimumPrice}
                  step="1"
                  value={(context.currentPrice / context.quantity).toString()}
                  onChange={handlePriceInput}
                  onBlur={handlePriceBlur}
                  className="w-20 h-8 border border-border/60 bg-background px-2 text-sm text-right focus:outline-none focus:border-foreground"
                />
             </div>
          ) : (
            <span className={cn("text-sm font-medium", context.isCouponApplied ? "line-through text-muted-foreground" : "text-foreground")}>
              {context.currency} {context.currentPrice.toFixed(2)}
            </span>
          )}
        </div>
        
        {context.isCouponApplied && context.discountAmount !== null && (
          <div className="flex items-center justify-between text-emerald-600 dark:text-emerald-400">
            <span className="text-sm font-medium">Discount</span>
            <span className="text-sm font-bold">- {context.currency} {context.discountAmount.toFixed(2)}</span>
          </div>
        )}
      </div>

      <div className="pt-2 space-y-2">
        {context.fulfillmentTargets.some(t => t.startsWith("http")) && (
          <div className="flex items-center gap-2 text-xs font-medium text-blue-700 dark:text-blue-400 bg-blue-50 dark:bg-blue-950/30 border border-blue-200 dark:border-blue-900/50 px-3 py-2">
            <svg className="h-4 w-4 shrink-0" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M13 10V3L4 14h7v7l9-11h-7z" />
            </svg>
            Includes API Delivery / SaaS Access
          </div>
        )}
      </div>

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
        {context.interval !== "one_time" && (
          <div className="mt-1 text-right">
            <span className="text-xs text-muted-foreground uppercase tracking-widest">
              Billed {context.interval === "mo" ? "Monthly" : "Annually"}
            </span>
          </div>
        )}
      </div>
    </div>
  );
}
