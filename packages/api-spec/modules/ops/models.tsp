namespace LazuarApi.Ops;

model ChatRequestDto {
  message: string;
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
