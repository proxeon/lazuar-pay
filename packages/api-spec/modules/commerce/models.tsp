namespace LazuarApi.Commerce;

model CheckoutConfigurationDto {
  requires_address: boolean;
  requires_tax_id: boolean;
  requires_phone: boolean;
}

model ProductDto {
  id: string; // GUID
  slug: string;
  name: string;
  price: float64;
  currency: string;
  interval: string;
  is_active: boolean;
  fulfillment_targets: string[];
  checkout_configuration: CheckoutConfigurationDto;
}

model CreateProductRequestDto {
  name: string;
  slug: string;
  price: float64;
  currency: string;
  interval: string;
  requires_address: boolean;
  requires_tax_id: boolean;
  requires_phone: boolean;
  fulfillment_targets: string[];
}

model UpdateProductRequestDto {
  name: string;
  slug: string;
  price: float64;
  currency: string;
  interval: string;
  is_active: boolean;
  requires_address: boolean;
  requires_tax_id: boolean;
  requires_phone: boolean;
  fulfillment_targets: string[];
}

model PublicCheckoutRequestDto {
  tenant_slug: string;
  product_slug: string;
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
}

model PortalSubscriptionDto {
  id: string; // GUID
  product_id: string; // GUID
  product_name: string;
  status: string;
  current_period_end?: utcDateTime;
}

model PortalOrderDto {
  id: string; // GUID
  product_id: string; // GUID
  product_name: string;
  status: string;
  created_at: utcDateTime;
}

model AggregatedPortalDataResponse {
  subscriptions: PortalSubscriptionDto[];
  orders: PortalOrderDto[];
}
