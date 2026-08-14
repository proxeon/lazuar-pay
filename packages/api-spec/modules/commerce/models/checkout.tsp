namespace LazuarApi.Commerce;

model PublicCheckoutRequestDto {
  tenant_slug: string;
  product_slug: string;
  session_id?: string; // GUID
  name: string;
  email: string;
  phone?: string;
  tax_id?: string;
  company_name?: string;
  address_line1?: string;
  city?: string;
  postal_code?: string;
  state_code?: string;
  country_code?: string;
  quantity?: int32;
  is_guest_checkout?: boolean;
  coupon_code?: string;
  /**
   * Opaque string map persisted onto the Commerce Subscription (P09 / P10.22).
   * Send aura_org_id, optional type=saas_subscription, optional billing_interval.
   * Pay overwrites type unless the client sent saas_subscription; always stamps tenant_id.
   */
  metadata?: Record<string>;
}

model CheckoutResponse {
  url: string;
  is_zero_amount_bypass?: boolean;
}

model CheckoutStatusResponse {
  status: string;
  /**
   * Deprecated: anonymous status poll no longer mints portal magic tokens.
   * Use email magic links or authenticated portal access instead.
   */
  token?: string;
}

model ValidateCouponResponseDto {
  is_valid: boolean;
  discount_amount: float64;
  final_price: float64;
  error_message?: string;
}
