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
  fulfillment_targets: string[];
  checkout_configuration: CheckoutConfigurationDto;
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
  fulfillment_targets: string[];
}
