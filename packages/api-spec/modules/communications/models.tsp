namespace LazuarApi.Communications;

model MessageTemplateDto {
  id: string; // GUID
  name: string;
  channel: string;
  subject: string;
  email_body: string;
  whatsapp_body: string;
  is_default: boolean;
  required_variables: string[];
  optional_variables: string[];
  updated_at: utcDateTime;
}

model CreateTemplateRequestDto {
  name: string;
  subject: string;
  email_body: string;
  whatsapp_body: string;
  channel: string;
  required_variables: string[];
  optional_variables: string[];
}

model UpdateTemplateRequestDto {
  subject: string;
  email_body: string;
  whatsapp_body: string;
}

model TemplateVariableDto {
  tag: string;
  description: string;
}

model TemplateVariableCategoryDto {
  title: string;
  items: TemplateVariableDto[];
}

model TemplatePreviewRequestDto {
  subject: string;
  email_body: string;
  whatsapp_body: string;
}

model TemplatePreviewResponseDto {
  html_email_preview: string;
  text_whatsapp_preview: string;
  subject_content: string;
}

model TestReminderRequestDto {
  template_name: string;
  channel?: string;
}

model TestReminderResponse {
  success: boolean;
  sent_to: string;
}

/**
 * Create broadcast. v1 fans out to all ACTIVE/PAST_DUE subscribers with marketing consent.
 * Targeting filters (plan/status/reminder-only) are not productized yet — do not re-add
 * until ISubscriberQueryService + Broadcast storage + fan-out honor them end-to-end.
 */
model CreateBroadcastRequestDto {
  subject: string;
  email_body: string;
  whatsapp_body: string;
  channel: string;
}

model EmailConfigDto {
  /** True when a Resend API key is stored (encrypted). Full key is never returned. */
  has_api_key: boolean;
  /** Masked hint, e.g. last 4 characters of the plaintext key. */
  api_key_hint?: string;
  sender_email?: string;
  is_active: boolean;
}

model SaveEmailConfigRequestDto {
  /** Omit or leave empty to keep the existing encrypted key. Required on first save. */
  api_key?: string;
  sender_email: string;
  is_active: boolean;
}

/**
 * Broadcast fan-out status for admin polling.
 * credits_reserved / credits_used are reserved for future cost models;
 * v1 broadcasts are free and always return 0.
 */
model BroadcastStatusDto {
  id: string;
  status: string;
  total_recipients: int32;
  sent_count: int32;
  suppressed_count: int32;
  failed_count: int32;
  /** Reserved; v1 broadcasts are free — always 0. */
  credits_reserved: int32;
  /** Reserved; v1 broadcasts are free — always 0. */
  credits_used: int32;
  created_at: utcDateTime;
  completed_at?: utcDateTime;
  failure_reason?: string;
}

/**
 * Pre-send cost preview for a broadcast audience.
 * Credit cost fields are reserved for future models; v1 is free (zeros / always sufficient).
 */
model BroadcastCostPreviewDto {
  recipient_count: int32;
  /** Reserved; v1 free — always 0. */
  credits_per_recipient: int32;
  /** Reserved; v1 free — always 0. */
  total_credits: int32;
  sufficient_credits: boolean;
  available_credits: int32;
}
