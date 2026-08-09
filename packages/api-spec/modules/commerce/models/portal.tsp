namespace LazuarApi.Commerce;

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

model GenerateCustomerPortalRequestDto {
  customer_email: string;
  return_url: string;
}

model GenerateCustomerPortalResponseDto {
  url: string;
}

model CancelPortalRequest {
  subscription_id: string; // GUID
}

model ArrearsSummaryDto {
  product_name: string;
  amount: float64;
  currency: string;
  status: string;
}
