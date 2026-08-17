export const CHECKOUT_QUANTITY_MIN = 1;
export const CHECKOUT_QUANTITY_MAX = 99;

export interface CheckoutContext {
  itemName: string;
  audience?: string;
  pricingModel: string;
  basePrice: number;
  minimumPrice: number;
  currentPrice: number;
  interval?: string;
  supportsOffSession?: boolean;
  currency: string;
  discountAmount: number | null;
  finalPrice: number | null;
  isCouponApplied: boolean;
  fulfillmentTargets: string[];
  quantity: number;
  quantityAdjustable: boolean;
  trialDays?: number;
  taxAmount: number;
  sstRatePercent: number;
  sstTaxType?: string;
  grossAmount: number;
  recurringGrossAmount: number;
}

export interface CheckoutAuthContext {
  userName?: string;
  userEmail?: string;
  isAdminOfTenant: boolean;
  isGuestMode: boolean;
}
