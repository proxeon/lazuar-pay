namespace LazuarApi.One;

// Integrator workspace provision (multi-product; Aura-compatible aliases)
model ProvisionWorkspaceRequestDto {
  /**
   * Legacy Aura org id (GUID). Prefer external_org_id.
   * Required when external_org_id is omitted. For product "aura" must be a GUID.
   */
  aura_org_id?: string;
  /**
   * External org / tenant id for the product (idempotency key with external_product).
   * Alias of aura_org_id — either field is accepted.
   */
  external_org_id?: string;
  /**
   * Product slug bound on Organization.ExternalProduct (default "aura").
   * e.g. aura, demo-app. Lowercase [a-z][a-z0-9_-]*.
   */
  external_product?: string;
  /** Display name for the Hub workspace (used on first create only). */
  display_name: string;
  /** Optional slug; if omitted Hub generates {product}-{compact}. */
  slug?: string;
  /** Optional: if user exists, grant workspace membership. Does not create users. */
  owner_email?: string;
  /**
   * Workspace membership role when owner_email matches an existing GlobalUser.
   * Default ADMIN. Allowed: ADMIN | SUPER_ADMIN (never grants global system admin).
   */
  owner_role?: string;
  /** Default true → sk_test_ bootstrap key. */
  is_test_mode?: boolean;
  /** Default "{product} bootstrap" (Aura: "Aura bootstrap"). */
  key_name?: string;
  /** Absolute HTTPS URL of integrator webhook receiver (Connect inline path). */
  webhook_url?: string;
  /**
   * Optional event filter for the provisioned webhook endpoint.
   * Omit/empty → payment.completed + payment.failed defaults.
   */
  webhook_enabled_events?: string[];
}

model ProvisionWorkspaceApiKeyDto {
  id?: string;
  prefix?: string;
  hint?: string;
  scopes: string[];
  /** Full secret — only on first create; null on idempotent re-call. */
  plain_key?: string;
}

/** Outbound webhook registered at provision (or healed on re-provision). */
model ProvisionWorkspaceWebhookDto {
  id?: string;
  url?: string;
  is_active?: boolean;
  enabled_events: string[];
  /** Full signing secret — only when newly created this call. */
  secret_key?: string;
  has_secret?: boolean;
  secret_hint?: string;
}

model ProvisionWorkspaceOwnerDto {
  /** true if membership exists after this call (created or pre-existing). */
  attached: boolean;
  /** attached | user_not_found | not_requested */
  status: string;
  /** ADMIN | SUPER_ADMIN when attached */
  role?: string;
  email?: string;
}

model ProvisionWorkspaceResponseDto {
  workspace_id: string;
  slug: string;
  /** Legacy alias of external_org_id (always populated for Aura clients). */
  aura_org_id: string;
  /** Normalized external org id (same value as aura_org_id). */
  external_org_id?: string;
  /** Product slug that was bound (default aura). */
  external_product?: string;
  created: boolean;
  api_key: ProvisionWorkspaceApiKeyDto;
  /** Present when a webhook endpoint was registered (create or prior). */
  webhook?: ProvisionWorkspaceWebhookDto;
  owner?: ProvisionWorkspaceOwnerDto;
}
