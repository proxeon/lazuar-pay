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
  pricing_model: string;
  minimum_price: float64;
  currency: string;
  interval: string;
  is_active: boolean;
  gateway_name: string;
  @doc("Computed from gateway_name. True only for Stripe and CHIP Collect.")
  supports_off_session: boolean;
  fulfillment_targets: string[];
  checkout_configuration: CheckoutConfigurationDto;
  sst_tax_type?: string;
  sst_rate_percent?: float64;
  trial_days?: int32;
  prices?: ProductPriceDto[];
}

model ProductPriceDto {
  id: string;
  interval: string;
  amount: float64;
  is_default?: boolean;
}

model CreateProductRequestDto {
  name: string;
  slug: string;
  price: float64;
  pricing_model: string;
  minimum_price: float64;
  currency: string;
  interval: string;
  gateway_name: string;
  requires_address: boolean;
  requires_tax_id: boolean;
  requires_phone: boolean;
  sst_tax_type?: string;
  sst_rate_percent?: float64;
  trial_days?: int32;
  yearly_price?: float64;
  fulfillment_targets: string[];
}

model UpdateProductRequestDto {
  name: string;
  slug: string;
  price: float64;
  pricing_model: string;
  minimum_price: float64;
  currency: string;
  interval: string;
  is_active: boolean;
  gateway_name: string;
  requires_address: boolean;
  requires_tax_id: boolean;
  requires_phone: boolean;
  sst_tax_type?: string;
  sst_rate_percent?: float64;
  trial_days?: int32;
  yearly_price?: float64;
  fulfillment_targets: string[];
}
