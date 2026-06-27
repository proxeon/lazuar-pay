// apps/portal-page/src/modules/checkout/types.ts

export interface CheckoutContext {
  itemName: string;
  audience?: string;
  price: number;
  interval?: string;
  currency: string;
  discountAmount: number | null;
  finalPrice: number | null;
  isCouponApplied: boolean;
  fulfillmentTargets: string[];
}

export interface CheckoutAuthContext {
  userName?: string;
  userEmail?: string;
  isAdminOfTenant: boolean;
  isGuestMode: boolean;
}
