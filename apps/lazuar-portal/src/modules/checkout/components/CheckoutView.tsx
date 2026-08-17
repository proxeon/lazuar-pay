"use client";

import { useState } from "react";
import { CheckoutLayout } from "./CheckoutLayout";
import { OrderSummaryCard } from "./OrderSummaryCard";
import { PromoCodeInput } from "./PromoCodeInput";
import { CheckoutForm } from "./CheckoutForm";
import { validateCouponCode, type ProductDto } from "../lib/api";
import { grossBreakdown, productSignalsSst } from "../lib/grossBreakdown";
import { localizeCheckoutError } from "../i18n/errors";
import { useCheckoutT } from "../i18n/CheckoutI18n";
import {
  CheckoutContext,
  CheckoutAuthContext,
  CHECKOUT_QUANTITY_MIN,
  CHECKOUT_QUANTITY_MAX,
} from "../types";

interface CheckoutViewProps {
  tenantSlug: string;
  product: ProductDto;
  initialAuthContext: CheckoutAuthContext;
  isCancelled?: boolean;
  workspaceName?: string;
}

export function CheckoutView({ tenantSlug, product, initialAuthContext, isCancelled, workspaceName }: CheckoutViewProps) {
  const { t } = useCheckoutT();
  const [authContext, setAuthContext] = useState<CheckoutAuthContext>(initialAuthContext);
  const [couponCode, setCouponCode] = useState("");
  const [isCouponValidating, setIsCouponValidating] = useState(false);
  const [couponError, setCouponError] = useState<string | null>(null);
  const [globalError, setGlobalError] = useState<string | null>(null);
  
  const prices = product.prices ?? [];
  const defaultInterval = product.interval;
  const [selectedInterval, setSelectedInterval] = useState(defaultInterval);
  const selectedPrice = prices.find((p) => p.interval === selectedInterval);
  const unitPrice = selectedPrice?.amount ?? product.price;
  const [quantity, setQuantity] = useState(1);
  const [customPrice, setCustomPrice] = useState<number>(unitPrice);
  const quantityAdjustable = product.pricing_model === "FIXED" && (product.interval === "one_time" || product.interval === "mo" || product.interval === "yr");
  const trialDays = product.trial_days ?? 0;
  const [discountAmount, setDiscountAmount] = useState<number | null>(null);
  const [finalPrice, setFinalPrice] = useState<number | null>(null);
  const [isCouponApplied, setIsCouponApplied] = useState(false);

  const unitNet = product.pricing_model === "PWYW" ? customPrice : unitPrice;
  const basePriceForQuantity = unitNet * quantity;
  const sstTaxType = product.sst_tax_type ?? "06";
  const sstRatePercent = product.sst_rate_percent ?? 0;
  const merchantHasSst = productSignalsSst(sstTaxType, sstRatePercent);
  const unitNetAfterCoupon =
    isCouponApplied && discountAmount !== null
      ? Math.max(0, unitNet - discountAmount / quantity)
      : unitNet;
  const isTrialToday = trialDays > 0 && selectedInterval !== "one_time";
  const todayBreakdown = grossBreakdown(
    isTrialToday ? 0 : unitNetAfterCoupon,
    quantity,
    sstTaxType,
    sstRatePercent,
    merchantHasSst,
  );
  const recurringBreakdown = grossBreakdown(
    unitNetAfterCoupon,
    quantity,
    sstTaxType,
    sstRatePercent,
    merchantHasSst,
  );

  const handleApplyCoupon = async (code: string) => {
    setIsCouponValidating(true);
    setCouponError(null);
    setGlobalError(null);

    try {
      const data = await validateCouponCode(tenantSlug, product.slug, code, {
        interval: selectedInterval,
        priceId: selectedPrice?.id,
        quantity,
      });
      setDiscountAmount(data.discount_amount);
      setFinalPrice(Math.max(0, data.final_price));
      setIsCouponApplied(true);
      setCouponCode(code);
    } catch (err: any) {
      setCouponError(localizeCheckoutError(err.message, t));
      setIsCouponApplied(false);
      setDiscountAmount(null);
      setFinalPrice(null);
    } finally {
      setIsCouponValidating(false);
    }
  };

  const handleRemoveCoupon = () => {
    setCouponCode("");
    setDiscountAmount(null);
    setFinalPrice(null);
    setIsCouponApplied(false);
    setCouponError(null);
    setGlobalError(null);
  };

  const handleQuantityChange = (newQty: number) => {
    if (!quantityAdjustable) {
      return;
    }

    const clamped = Math.min(
      CHECKOUT_QUANTITY_MAX,
      Math.max(CHECKOUT_QUANTITY_MIN, Math.trunc(newQty) || CHECKOUT_QUANTITY_MIN),
    );
    setQuantity(clamped);
    if (isCouponApplied) {
      handleRemoveCoupon();
    }
  };

  const handleCustomPriceChange = (newPrice: number) => {
    setCustomPrice(newPrice);
    if (isCouponApplied) {
      handleRemoveCoupon();
    }
  };

  const handleSetGuestMode = (isGuest: boolean) => {
    setAuthContext((prev) => ({ ...prev, isGuestMode: isGuest }));
  };

  const handleError = (errorMsg: string) => {
    setGlobalError(localizeCheckoutError(errorMsg, t));
  };

  const checkoutContext: CheckoutContext = {
    itemName: product.name,
    pricingModel: product.pricing_model,
    basePrice: unitPrice,
    minimumPrice: product.minimum_price,
    currentPrice: isTrialToday ? 0 : basePriceForQuantity,
    interval: selectedInterval,
    supportsOffSession: product.supports_off_session,
    currency: product.currency,
    discountAmount: discountAmount,
    finalPrice: finalPrice,
    isCouponApplied: isCouponApplied,
    fulfillmentTargets: product.fulfillment_targets || [],
    quantity,
    quantityAdjustable,
    trialDays: selectedInterval === "one_time" ? 0 : trialDays,
    taxAmount: todayBreakdown.lineTax,
    sstRatePercent,
    sstTaxType: todayBreakdown.taxType,
    grossAmount: todayBreakdown.gross,
    recurringGrossAmount: recurringBreakdown.gross,
  };

  return (
    <div className="w-full max-w-5xl mx-auto px-4 py-4 sm:py-8 md:py-12">
      {isCancelled && !globalError && (
        <div className="mb-6 p-4 bg-amber-50 border border-amber-200 text-amber-800 text-sm font-medium break-words">
          {t("banner.cancelled")}
        </div>
      )}

      {globalError && (
        <div className="mb-6 p-4 bg-red-50 border border-red-200 text-red-600 text-sm font-medium break-words">
          {globalError}
        </div>
      )}

      {prices.length > 1 && (
        <div className="mb-6 flex gap-2">
          {prices.map((p) => (
            <button
              key={p.id}
              type="button"
              onClick={() => {
                setSelectedInterval(p.interval);
                handleRemoveCoupon();
              }}
              className={`h-9 px-4 text-[11px] font-bold uppercase tracking-widest border ${
                selectedInterval === p.interval
                  ? "bg-foreground text-background border-foreground"
                  : "bg-background text-foreground border-border"
              }`}
            >
              {p.interval === "yr" ? "Yearly" : "Monthly"}
            </button>
          ))}
        </div>
      )}

      <CheckoutLayout
        formSlot={
          <CheckoutForm
            tenantSlug={tenantSlug}
            product={product}
            authContext={authContext}
            isCouponApplied={isCouponApplied}
            couponCode={couponCode}
            quantity={quantityAdjustable ? quantity : 1}
            interval={selectedInterval}
            priceId={selectedPrice?.id}
            workspaceName={workspaceName}
            onSetGuestMode={handleSetGuestMode}
            onError={handleError}
          />
        }
        summarySlot={
          <OrderSummaryCard
            context={checkoutContext}
            onQuantityChange={handleQuantityChange}
            onCustomPriceChange={handleCustomPriceChange}
            promoCodeSlot={
              <PromoCodeInput
                isApplied={isCouponApplied}
                isValidating={isCouponValidating}
                error={couponError}
                onApply={handleApplyCoupon}
                onRemove={handleRemoveCoupon}
              />
            }
          />
        }
      />
    </div>
  );
}
