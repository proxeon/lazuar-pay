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

model ChatStreamChunkDto {
  type: string;
  content?: string;
  tool_name?: string;
  proposed_action?: ProposedActionDto;
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
  tool_status?: string;
  proposed_action?: ProposedActionDto;
  created_at: utcDateTime;
}
