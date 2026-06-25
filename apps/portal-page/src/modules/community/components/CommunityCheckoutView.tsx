"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { CheckoutLayout } from "../../checkout/components/CheckoutLayout";
import { OrderSummaryCard } from "../../checkout/components/OrderSummaryCard";
import { PromoCodeInput } from "../../checkout/components/PromoCodeInput";
import { CommunityCheckoutForm } from "./CommunityCheckoutForm";
import { validateCouponCode, type CommunityPlanDto } from "../lib/api";
import { CheckoutContext, CheckoutAuthContext } from "../../checkout/types";

interface CommunityCheckoutViewProps {
  tenantSlug: string;
  plan: CommunityPlanDto;
  initialAuthContext: CheckoutAuthContext;
  isCancelled?: boolean;
}

export function CommunityCheckoutView({ tenantSlug, plan, initialAuthContext, isCancelled }: CommunityCheckoutViewProps) {
  const router = useRouter();

  const [authContext, setAuthContext] = useState<CheckoutAuthContext>(initialAuthContext);
  const [couponCode, setCouponCode] = useState("");
  const [isCouponValidating, setIsCouponValidating] = useState(false);
  const [couponError, setCouponError] = useState<string | null>(null);
  const [discountAmount, setDiscountAmount] = useState<number | null>(null);
  const [finalPrice, setFinalPrice] = useState<number | null>(null);
  const [isCouponApplied, setIsCouponApplied] = useState(false);

  const handleApplyCoupon = async (code: string) => {
    setIsCouponValidating(true);
    setCouponError(null);

    try {
      const data = await validateCouponCode(tenantSlug, plan.slug, code);
      setDiscountAmount(data.discount_amount);
      setFinalPrice(data.final_price);
      setIsCouponApplied(true);
      setCouponCode(code);
    } catch (err: any) {
      setCouponError(err.message || "Invalid promo code.");
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
  };

  const handleSetGuestMode = (isGuest: boolean) => {
    setAuthContext((prev) => ({ ...prev, isGuestMode: isGuest }));
  };

  const handleSuccessZeroAmount = () => {
    router.push(`/${tenantSlug}/community/${plan.slug}/success`);
  };

  const checkoutContext: CheckoutContext = {
    itemName: plan.name,
    audience: plan.audience,
    price: plan.price,
    interval: plan.interval,
    currency: "MYR",
    discountAmount: discountAmount,
    finalPrice: finalPrice,
    isCouponApplied: isCouponApplied
  };

  return (
    <div className="w-full max-w-5xl mx-auto px-4 py-8 md:py-12">
      {isCancelled && (
        <div className="mb-6 p-4 bg-amber-50 border border-amber-200 text-amber-800 text-sm font-medium">
          Payment was cancelled or failed. Please try again or use a different payment method.
        </div>
      )}

      <CheckoutLayout
        formSlot={
          <CommunityCheckoutForm
            tenantSlug={tenantSlug}
            planSlug={plan.slug}
            authContext={authContext}
            isCouponApplied={isCouponApplied}
            couponCode={couponCode}
            onSetGuestMode={handleSetGuestMode}
            onSuccessZeroAmount={handleSuccessZeroAmount}
          />
        }
        summarySlot={
          <OrderSummaryCard
            context={checkoutContext}
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
