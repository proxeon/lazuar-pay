namespace LazuarApi.Commerce;

model CustomLineItemDto {
  description: string;
  quantity: int32;
  unit_price: float64;
}

model CreateCustomCheckoutRequestDto {
  client_email: string;
  client_name: string;
  line_items: CustomLineItemDto[];
  expires_at?: utcDateTime;
  is_b2b_required: boolean;
  /** Preferred payment gateway (e.g. STRIPE, CHIP). Defaults to first configured tenant gateway. */
  gateway_name?: string;
  /** Explicit commercial due date (UTC). Wins over `terms`. */
  due_at?: utcDateTime;
  /** Payment terms when `due_at` is omitted: due_on_receipt | net_7 | net_15 | net_30. */
  terms?: string;
}

model CustomCheckoutDto {
  id: string; // GUID
  client_profile_id: string; // GUID
  client_name?: string;
  client_email?: string;
  status: string;
  expires_at: utcDateTime;
  due_at?: utcDateTime;
  is_b2b_required: boolean;
  line_items: CustomLineItemDto[];
  total_amount: float64;
  created_at: utcDateTime;
  /** Per-org sequential quote number (`QT-yyyy-#####`), allocated once. */
  document_number?: string;
  /** HMAC-signed public URL for draft proforma PDF (sig+exp). */
  draft_pdf_url?: string;
}

model CommerceDisputeDto {
  id: string;
  gateway_transaction_id: string;
  amount: float64;
  currency: string;
  status: string;
  subscription_id?: string;
  checkout_session_id?: string;
  created_at: utcDateTime;
}
