namespace LazuarApi.Lhdn;

model LhdnItemDto {
  description: string;
  classification_code: string;
  quantity: float64;
  unit_price: float64;
  tax_rate: float64;
  tax_amount: float64;
  subtotal: float64;
}

model LhdnAddressDto {
  line1: string;
  line2?: string;
  line3?: string;
  city: string;
  postal_code: string;
  state_code: string;
  country_code: string;
}

model SubmitDocumentRequestDto {
  internal_id: string;
  document_type: string;
  issue_date: utcDateTime;
  buyer_name: string;
  buyer_tin: string;
  buyer_id_type: string;
  buyer_id_value: string;
  buyer_email?: string;
  buyer_phone?: string;
  buyer_address: LhdnAddressDto;
  items: LhdnItemDto[];
  total_excluding_tax: float64;
  total_tax: float64;
  total_including_tax: float64;
}

model LhdnDocumentResponseDto {
  internal_id: string;
  lhdn_uuid?: string;
  long_id?: string;
  status: string;
  qr_link?: string;
  error_message?: string;
  submitted_at: utcDateTime;
  validated_at?: utcDateTime;
}

model RegisterWebhookRequestDto {
  url: string;
  secret: string;
  events: string[];
}

model WebhookSubscriptionDto {
  id: string;
  url: string;
  events: string[];
  is_active: boolean;
  created_at: utcDateTime;
}
