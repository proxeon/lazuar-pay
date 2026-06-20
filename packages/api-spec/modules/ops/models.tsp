// packages/api-spec/modules/ops/models.tsp
namespace LazuarApi.Ops;

model ChatRequestDto {
  message: string;
  conversation_id?: string;
}

model ChatResponseDto {
  message: string;
  proposed_action?: ProposedActionDto;
}

model ProposedActionDto {
  idempotency_key: string;
  tool_name: string;
  intent_title: string;
  severity: string;
  human_readable_summary: string;
  command_payload: unknown;
}

model UiRequestDto {
  tool_name: string;
  schema_json: unknown;
  prefill_data?: unknown;
  is_resolved: boolean;
}

model ChatStreamChunkDto {
  type: string;
  content?: string;
  tool_name?: string;
  executed_tools?: string[];
  proposed_action?: ProposedActionDto;
  ui_request?: UiRequestDto;
}

model OpsConversationDto {
  id: string; // GUID
  title: string;
  updated_at: utcDateTime;
}

model OpsMessageDto {
  id: string; // GUID
  conversation_id: string; // GUID
  role: string;
  content: string;
  executed_tools?: string[];
  proposed_action?: ProposedActionDto;
  ui_request?: UiRequestDto;
  is_resolved?: boolean;
  created_at: utcDateTime;
}

model RenameConversationRequestDto {
  title: string;
}
