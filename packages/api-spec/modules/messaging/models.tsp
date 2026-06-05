// packages/api-spec/modules/messaging/models.tsp

namespace LazuarApi.Messaging;

model MessageTemplateDto {
  id: string; // GUID
  name: string;
  subject: string;
  body: string;
  is_default: boolean;
  required_variables: string[];
  optional_variables: string[];
  updated_at: utcDateTime;
}

model UpdateTemplateRequestDto {
  subject: string;
  body: string;
}

model TestReminderRequestDto {
  template_name: string;
  channel?: string;
}

model TestReminderResponse {
  success: boolean;
  sent_to: string;
}
