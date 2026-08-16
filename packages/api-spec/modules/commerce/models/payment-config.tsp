namespace LazuarApi.Commerce;

model PaymentConfigDto {
  gateway_type: string;
  /** @deprecated Never populated; use has_api_key + api_key_hint. */
  api_key?: string;
  merchant_id?: string;
  /** @deprecated Never populated; use has_webhook_secret + webhook_secret_hint. */
  webhook_secret?: string;
  /** @deprecated Never populated; use has_secret_key + secret_key_hint. */
  secret_key?: string;
  /** When false, credentials are retained but gateway is not used for new checkouts/charges. */
  is_active: boolean;
  /** True when an API key is stored (encrypted). Full key is never returned. */
  has_api_key: boolean;
  /** Masked hint, e.g. last 4 characters of the plaintext key. */
  api_key_hint?: string;
  /** True when a webhook secret is stored (encrypted). */
  has_webhook_secret: boolean;
  /** Masked hint for the webhook secret. */
  webhook_secret_hint?: string;
  /** True when a Stripe-style secret key is stored (same column as api_key). */
  has_secret_key: boolean;
  /** Masked hint for the secret key. */
  secret_key_hint?: string;
  /** test or live. Owns Billplz sandbox vs www. */
  environment?: string;
}

model SavePaymentConfigRequestDto {
  gateway_type: string;
  /** Omit or empty to keep existing encrypted key. */
  api_key?: string;
  collection_id?: string;
  /** Omit or empty to keep existing webhook secret. */
  webhook_secret?: string;
  /** Stripe secret key; omit or empty to keep existing. */
  secret_key?: string;
  /** Soft-disable without deleting credentials. Defaults to true on create. */
  is_active?: boolean;
  environment?: string;
}
