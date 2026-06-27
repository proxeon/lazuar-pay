"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { CheckoutLayout } from "./CheckoutLayout";
import { OrderSummaryCard } from "./OrderSummaryCard";
import { PromoCodeInput } from "./PromoCodeInput";
import { CheckoutForm } from "./CheckoutForm";
import { validateCouponCode, type ProductDto } from "../lib/api";
import { CheckoutContext, CheckoutAuthContext } from "../types";

interface CheckoutViewProps {
  tenantSlug: string;
  product: ProductDto;
  initialAuthContext: CheckoutAuthContext;
  isCancelled?: boolean;
}

export function CheckoutView({ tenantSlug, product, initialAuthContext, isCancelled }: CheckoutViewProps) {
  const router = useRouter();

  const [authContext, setAuthContext] = useState<CheckoutAuthContext>(initialAuthContext);
  const [couponCode, setCouponCode] = useState("");
  const [isCouponValidating, setIsCouponValidating] = useState(false);
  const [couponError, setCouponError] = useState<string | null>(null);
  
  const [quantity, setQuantity] = useState(1);
  const [discountAmount, setDiscountAmount] = useState<number | null>(null);
  const [finalPrice, setFinalPrice] = useState<number | null>(null);
  const [isCouponApplied, setIsCouponApplied] = useState(false);

  const basePriceForQuantity = product.price * quantity;

  const handleApplyCoupon = async (code: string) => {
    setIsCouponValidating(true);
    setCouponError(null);

    try {
      const data = await validateCouponCode(tenantSlug, product.slug, code);
      const discountRatio = data.discount_amount / product.price;
      const totalDiscount = basePriceForQuantity * discountRatio;
      
      setDiscountAmount(totalDiscount);
      setFinalPrice(Math.max(0, basePriceForQuantity - totalDiscount));
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

  const handleQuantityChange = (newQty: number) => {
    setQuantity(newQty);
    if (isCouponApplied) {
      handleRemoveCoupon();
    }
  };

  const handleSetGuestMode = (isGuest: boolean) => {
    setAuthContext((prev) => ({ ...prev, isGuestMode: isGuest }));
  };

  const handleSuccessZeroAmount = () => {
    router.push(`/${tenantSlug}/checkout/${product.slug}/success`);
  };

  const checkoutContext: CheckoutContext = {
    itemName: product.name,
    price: basePriceForQuantity,
    interval: product.interval,
    currency: product.currency,
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
          <CheckoutForm
            tenantSlug={tenantSlug}
            product={product}
            authContext={authContext}
            isCouponApplied={isCouponApplied}
            couponCode={couponCode}
            quantity={quantity}
            onQuantityChange={handleQuantityChange}
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
