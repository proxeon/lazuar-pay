namespace LazuarApi.Lhdn;

alias IdType = "BRN" | "NRIC" | "PASSPORT" | "ARMY";
alias TaxTypeCode = "01" | "02" | "03" | "04" | "05" | "06" | "E";
alias StateCode =
  | "01"
  | "02"
  | "03"
  | "04"
  | "05"
  | "06"
  | "07"
  | "08"
  | "09"
  | "10"
  | "11"
  | "12"
  | "13"
  | "14"
  | "15"
  | "16"
  | "17";
alias DocumentType = "01" | "02" | "03" | "04" | "11" | "12" | "13" | "14";

model ValidateTinRequestDto {
  tin: string;
  id_type: IdType;
  id_value: string;
}

model ValidateTinResponseDto {
  is_valid: boolean;
  tin: string;
  taxpayer_name?: string;
}

model LhdnItemDto {
  description: string;
  classification_code: string;
  quantity: float64;
  unit_price: float64;
  tax_rate: float64;
  tax_amount: float64;
  subtotal: float64;
  tax_type_code: TaxTypeCode;
}

model LhdnAddressDto {
  line1: string;
  line2?: string;
  line3?: string;
  city: string;
  postal_code: string;
  state_code: StateCode;
  country_code: string;
}

model SubmitDocumentRequestDto {
  internal_id: string;
  document_type: DocumentType;
  document_version?: string;
  issue_date: utcDateTime;
  original_lhdn_uuid?: string;
  adjustment_reason?: string;
  billing_period_start?: utcDateTime;
  billing_period_end?: utcDateTime;
  buyer_name: string;
  buyer_tin: string;
  buyer_id_type: IdType;
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
  is_test_mode: boolean;
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

model UpdateLhdnCertificateRequestDto {
  p12_base64_file: string;
  passphrase: string;
}

model CancelDocumentRequestDto {
  reason: string;
}

alias LhdnEnvironmentType = "PROD" | "SANDBOX";

/**
 * Tenant MyInvois / supplier profile for OrgAdmin.
 * Secrets are never returned in full — only presence flags and optional last-4 hints.
 */
model LhdnTenantConfigDto {
  supplier_tin: string;
  id_type: string;
  id_value: string;
  environment: LhdnEnvironmentType;
  msic_code?: string;
  intermediary_mode: boolean;
  myinvois_client_id?: string;
  /** True when a client secret is stored (value never returned). */
  has_client_secret: boolean;
  /** Last 4 characters of the stored client secret when present. */
  client_secret_hint?: string;
  /** True when a signing certificate is stored. */
  has_certificate: boolean;
  legal_name?: string;
  address_line1?: string;
  city?: string;
  state?: string;
  postal?: string;
  country?: string;
}

/**
 * Create or replace tenant LHDN profile. Omit client secret / leave empty to keep existing secret.
 */
model UpdateLhdnTenantConfigRequestDto {
  supplier_tin: string;
  id_type: string;
  id_value: string;
  environment: LhdnEnvironmentType;
  msic_code?: string;
  intermediary_mode?: boolean;
  myinvois_client_id?: string;
  /** When omitted or empty, existing secret is preserved. */
  myinvois_client_secret?: string;
  legal_name?: string;
  address_line1?: string;
  city?: string;
  state?: string;
  postal?: string;
  country?: string;
}

// API key DTOs are defined on LazuarApi.One (platform ownership). Re-export aliases for LHDN product docs.
alias GenerateApiKeyRequestDto = LazuarApi.One.GenerateApiKeyRequestDto;
alias GenerateApiKeyResponseDto = LazuarApi.One.GenerateApiKeyResponseDto;
alias ApiKeyDto = LazuarApi.One.ApiKeyDto;
