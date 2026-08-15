namespace LazuarApi.One;

/** List/detail metadata — never includes full signing secret. */
model WebhookEndpointDto {
  id: string; // GUID
  url: string;
  is_active: boolean;
  created_at: utcDateTime;
  /** Empty array means the endpoint receives all event types. */
  enabled_events: string[];
  has_secret: boolean;
  /** Last 4 characters of the signing secret for display. */
  secret_hint?: string;
}

model CreateWebhookEndpointRequestDto {
  url: string;
  is_active?: boolean = true;
  /** Empty or omitted = all events. */
  enabled_events?: string[];
}

/** Create response — includes full secret once on first create. */
model CreateWebhookEndpointResponseDto {
  id: string; // GUID
  url: string;
  /** Full signing secret — returned only once on first create. Omitted/null on idempotent same-URL create. */
  secret_key?: string;
  is_active: boolean;
  enabled_events: string[];
  created_at: utcDateTime;
}

model RotateWebhookSecretResponseDto {
  id: string;
  /** Full signing secret — returned only once on rotate. */
  secret_key: string;
}

model UpdateWebhookEndpointRequestDto {
  url: string;
  is_active: boolean;
  /** When provided, replaces the endpoint event filter (empty = all). */
  enabled_events?: string[];
}

/** @deprecated Prefer create + update-by-id multi-endpoint API. */
model SaveWebhookEndpointRequestDto {
  url: string;
  is_active: boolean;
}

/** Shallow delivery attempt. Payload is not included. POST …/redeliver returns a new PENDING row. */
model WebhookDeliveryLogDto {
  id: string; // GUID
  event_type: string;
  status: string;
  attempt_count: int32;
  last_error?: string;
  created_at: utcDateTime;
}
