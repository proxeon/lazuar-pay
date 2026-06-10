import type { ProposedActionDto } from "../lib/api-client";

export interface Message {
  id: string;
  role: "user" | "assistant" | "system";
  content: string;
  isStreaming?: boolean;
  toolStatus?: string;
  proposedAction?: ProposedActionDto;
}
