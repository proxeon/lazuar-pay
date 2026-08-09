// apps/lazuar-admin/src/types/chat.ts
import type { ProposedActionDto, UiRequestDto } from "../lib/api-client";

export interface Message {
  id: string;
  role: "user" | "assistant" | "system";
  content: string;
  isStreaming?: boolean;
  executedTools?: string[];
  proposedAction?: ProposedActionDto;
  uiRequest?: UiRequestDto;
}
