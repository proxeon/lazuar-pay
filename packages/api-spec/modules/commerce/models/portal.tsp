namespace LazuarApi.Commerce;

model PortalSubscriptionDto {
  id: string; // GUID
  product_id: string; // GUID
  product_name: string;
  status: string;
  /** Paid-through instant (`NextBillingDate`). */
  current_period_end?: utcDateTime;
  cancel_at_period_end: boolean;
  is_reminder_only: boolean;
  quantity?: int32;
  pending_product_id?: string;
  pending_product_name?: string;
  trial_ends_at?: utcDateTime;
  /** HMAC download for the latest stored receipt / tax invoice, if any. */
  document_url?: string;
  document_label?: string;
}

model PortalPlanDto {
  id: string;
  name: string;
  interval: string;
  amount: float64;
  currency: string;
}

model PortalChangePlanRequest {
  subscription_id: string;
  product_id?: string;
  prorate?: boolean;
  apply?: string;
}

model PortalDocumentDto {
  id: string;
  document_number?: string;
  type: string;
  issued_at: utcDateTime;
  amount: float64;
  currency: string;
  lhdn_status?: string;
  download_url?: string;
}

model PortalDocumentsResponse {
  items: PortalDocumentDto[];
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
  /** Default true. Immediate if false, or if there is no remaining paid period. */
  at_period_end?: boolean = true;
}

model KeepPortalRequest {
  subscription_id: string;
}

model RequestPortalMagicLinkRequest {
  email: string;
}

model ArrearsSummaryDto {
  product_name: string;
  amount: float64;
  currency: string;
  status: string;
  is_reminder_only: boolean;
}
