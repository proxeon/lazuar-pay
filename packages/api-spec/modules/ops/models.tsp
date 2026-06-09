namespace LazuarApi.Ops;

model ChatRequestDto {
  message: string;
}

model ActionApprovalRequestDto {
  idempotency_key: string;
}

model ProposedActionDto {
  idempotency_key: string;
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
