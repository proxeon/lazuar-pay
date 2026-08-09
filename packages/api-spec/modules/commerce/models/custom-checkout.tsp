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
}

model CustomCheckoutDto {
  id: string; // GUID
  client_profile_id: string; // GUID
  client_name?: string;
  client_email?: string;
  status: string;
  expires_at: utcDateTime;
  is_b2b_required: boolean;
  line_items: CustomLineItemDto[];
  total_amount: float64;
  created_at: utcDateTime;
  /** HMAC-signed public URL for draft proforma PDF (sig+exp). */
  draft_pdf_url?: string;
}
