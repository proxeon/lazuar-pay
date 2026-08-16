import { ReactNode } from "react";
import { currencySymbol, formatMoney } from "../i18n/format";
import { useCheckoutT } from "../i18n/CheckoutI18n";
import { CheckoutContext, CHECKOUT_QUANTITY_MIN, CHECKOUT_QUANTITY_MAX } from "../types";

export function cn(...classes: (string | undefined | null | false)[]) {
  return classes.filter(Boolean).join(" ");
}

interface OrderSummaryCardProps {
  context: CheckoutContext;
  onCustomPriceChange: (price: number) => void;
  onQuantityChange?: (qty: number) => void;
  promoCodeSlot?: ReactNode;
}

export function OrderSummaryCard({ context, onCustomPriceChange, onQuantityChange, promoCodeSlot }: OrderSummaryCardProps) {
  const { t, locale } = useCheckoutT();
  const money = (amount: number) => formatMoney(locale, context.currency || "MYR", amount);
  const finalPriceToDisplay = context.finalPrice !== null ? context.finalPrice : context.currentPrice;
  const intervalLabel = context.interval === "mo"
    ? t("summary.intervalMonth")
    : context.interval === "yr"
      ? t("summary.intervalYear")
      : null;
  const isRecurring = intervalLabel !== null;
  const discountLabel = context.quantity > 1 && context.isCouponApplied
    ? t("summary.discountEach", { n: context.quantity })
    : t("summary.discount");

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

  return (
    <div className="border border-border/60 bg-card p-6 shadow-sm rounded-none space-y-4">
      <h3 className="text-xs font-bold uppercase tracking-widest text-muted-foreground">{t("summary.title")}</h3>
      
      <div className="pb-4 border-b border-border/40">
        <h4 className="text-lg font-semibold text-foreground leading-tight mb-1">{context.itemName}</h4>
        {context.audience && <p className="text-sm text-muted-foreground">{context.audience}</p>}
      </div>

      <div className="space-y-2">
        {context.quantityAdjustable && (
          <div className="flex items-center justify-between">
            <span className="text-sm text-muted-foreground">{t("summary.quantity")}</span>
            <div className="flex items-center border border-border/60">
              <button
                type="button"
                aria-label={t("summary.decreaseQty")}
                disabled={context.quantity <= CHECKOUT_QUANTITY_MIN}
                onClick={() => onQuantityChange?.(context.quantity - 1)}
                className="h-8 w-8 text-sm font-medium text-foreground disabled:opacity-40"
              >
                −
              </button>
              <input
                type="number"
                min={CHECKOUT_QUANTITY_MIN}
                max={CHECKOUT_QUANTITY_MAX}
                step={1}
                value={context.quantity}
                onChange={(e) => {
                  const val = parseInt(e.target.value, 10);
                  if (!Number.isNaN(val)) {
                    onQuantityChange?.(val);
                  }
                }}
                className="h-8 w-12 border-x border-border/60 bg-background px-1 text-center text-sm focus:outline-none"
              />
              <button
                type="button"
                aria-label={t("summary.increaseQty")}
                disabled={context.quantity >= CHECKOUT_QUANTITY_MAX}
                onClick={() => onQuantityChange?.(context.quantity + 1)}
                className="h-8 w-8 text-sm font-medium text-foreground disabled:opacity-40"
              >
                +
              </button>
            </div>
          </div>
        )}

        <div className="flex items-center justify-between">
          <span className="text-sm text-muted-foreground">
            {context.quantityAdjustable && context.quantity > 1
              ? t("summary.unitTimesQty", { amount: money(context.basePrice), n: context.quantity })
              : t("summary.subtotal")}
          </span>
          {context.pricingModel === "PWYW" && !context.isCouponApplied ? (
             <div className="flex items-center gap-2">
                <span className="text-sm font-medium text-muted-foreground">{currencySymbol(locale, context.currency || "MYR")}</span>
                <input 
                  type="number" 
                  min={context.minimumPrice}
                  step="1"
                  value={context.currentPrice.toString()}
                  onChange={handlePriceInput}
                  onBlur={handlePriceBlur}
                  className="w-20 h-8 border border-border/60 bg-background px-2 text-sm text-right focus:outline-none focus:border-foreground"
                />
             </div>
          ) : (
            <span className={cn("text-sm font-medium", context.isCouponApplied ? "line-through text-muted-foreground" : "text-foreground")}>
              {money(context.currentPrice)}
            </span>
          )}
        </div>
        
        {context.isCouponApplied && context.discountAmount !== null && (
          <div className="flex items-center justify-between text-emerald-600 dark:text-emerald-400">
            <span className="text-sm font-medium">{discountLabel}</span>
            <span className="text-sm font-bold">- {money(context.discountAmount)}</span>
          </div>
        )}
      </div>

      {promoCodeSlot && (
        <div className="pt-4 border-t border-border/40">
          {promoCodeSlot}
        </div>
      )}

      <div className="bg-secondary/40 border border-border/60 p-4 rounded-none mt-4 space-y-2">
        <div className="flex items-center justify-between">
          <span className="text-base font-semibold text-foreground">{t("summary.total")}</span>
          <span className="text-xl font-bold tracking-tighter text-foreground">
            {money(finalPriceToDisplay)}
          </span>
        </div>
        {isRecurring && (
          <p className="text-xs text-muted-foreground">
            {t("summary.thenRecurring", { amount: money(finalPriceToDisplay), interval: intervalLabel })}
          </p>
        )}
      </div>

      {isRecurring && context.supportsOffSession === false && (
        <p className="text-xs text-amber-800 bg-amber-50 border border-amber-200 px-3 py-2 leading-relaxed">
          {t("summary.notAutoDebit")}
        </p>
      )}
      {isRecurring && context.supportsOffSession && (
        <p className="text-xs text-muted-foreground">
          {t("summary.cardSaved")}
        </p>
      )}
    </div>
  );
}
