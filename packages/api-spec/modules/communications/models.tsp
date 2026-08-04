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

model CreateBroadcastRequestDto {
  subject: string;
  email_body: string;
  whatsapp_body: string;
  channel: string;
  target_plan_id?: string; // GUID
  target_status?: string;
  target_is_reminder_only?: boolean;
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
