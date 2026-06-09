import { useState, useRef, useEffect } from "react";
import { Send, Loader2, Bot, User, Activity } from "lucide-react";
import ReactMarkdown from "react-markdown";
import remarkGfm from "remark-gfm";
import { cn } from "../lib/utils";
import ActionApprovalCard from "./ActionApprovalCard";
import type { ProposedActionDto, ChatStreamChunkDto } from "../lib/api-client";

interface Message {
  id: string;
  role: "user" | "assistant" | "system";
  content: string;
  isStreaming?: boolean;
  toolStatus?: string;
  proposedAction?: ProposedActionDto;
}

interface OpsChatWorkspaceProps {
  activeConversationId: string | null;
  messages: Message[];
  setMessages: (updater: (prev: Message[]) => Message[]) => void;
}

export default function OpsChatWorkspace({
  activeConversationId,
  messages,
  setMessages
}: OpsChatWorkspaceProps) {
  const [input, setInput] = useState("");
  const [isProcessing, setIsProcessing] = useState(false);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  };

  useEffect(() => {
    scrollToBottom();
  }, [messages]);

  const executeStreamCall = async (payloadMessage: string, targetAssistantMsgId: string) => {
    try {
      const response = await fetch("http://localhost:8080/api/v1/ops/chat/stream", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ message: payloadMessage }),
        credentials: "include"
      });

      if (!response.ok) throw new Error("Stream connection failed");

      const reader = response.body?.getReader();
      const decoder = new TextDecoder();

      if (reader) {
        let buffer = "";
        while (true) {
          const { done, value } = await reader.read();
          if (done) break;

          buffer += decoder.decode(value, { stream: true });
          const lines = buffer.split("\n\n");
          buffer = lines.pop() || "";

          for (const line of lines) {
            if (line.startsWith("data: ")) {
              const dataStr = line.slice(6);
              if (dataStr === "[DONE]") break;

              try {
                const chunk: ChatStreamChunkDto = JSON.parse(dataStr);

                setMessages((prev) =>
                  prev.map((msg) => {
                    if (msg.id !== targetAssistantMsgId) return msg;

                    if (chunk.type === "text" && chunk.content) {
                      return { ...msg, content: msg.content + chunk.content, toolStatus: undefined };
                    } else if (chunk.type === "tool_status" && chunk.tool_name) {
                      return { ...msg, toolStatus: `Running ${chunk.tool_name}...` };
                    } else if (chunk.type === "proposed_action" && chunk.proposed_action) {
                      return { ...msg, proposedAction: chunk.proposed_action, toolStatus: undefined };
                    }
                    return msg;
                  })
                );
              } catch (e) {
                console.error("Failed to parse SSE chunk", e);
              }
            }
          }
        }
      }
    } catch (error) {
      setMessages((prev) =>
        prev.map((msg) =>
          msg.id === targetAssistantMsgId
            ? { ...msg, content: "Network error occurred." }
            : msg
        )
      );
    } finally {
      setMessages((prev) =>
        prev.map((msg) =>
          msg.id === targetAssistantMsgId
            ? { ...msg, isStreaming: false, toolStatus: undefined }
            : msg
        )
      );
      setIsProcessing(false);
    }
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!input.trim() || isProcessing || !activeConversationId) return;

    const userText = input.trim();
    setInput("");

    const userMsgId = Date.now().toString();
    setMessages((prev) => [
      ...prev,
      { id: userMsgId, role: "user", content: userText }
    ]);

    const assistantMsgId = (Date.now() + 1).toString();
    setMessages((prev) => [
      ...prev,
      { id: assistantMsgId, role: "assistant", content: "", isStreaming: true }
    ]);
    setIsProcessing(true);

    await executeStreamCall(userText, assistantMsgId);
  };

  const handleActionResolved = async (success: boolean, message?: string) => {
    const systemFeedback = success
      ? `[System: The action was executed successfully.]`
      : `[System: The action failed or was cancelled. Reason: ${message}]`;

    setMessages((prev) => [
      ...prev,
      { id: Date.now().toString(), role: "system", content: systemFeedback }
    ]);

    const assistantMsgId = (Date.now() + 1).toString();
    setMessages((prev) => [
      ...prev,
      { id: assistantMsgId, role: "assistant", content: "", isStreaming: true }
    ]);
    setIsProcessing(true);

    await executeStreamCall(systemFeedback, assistantMsgId);
  };

  if (!activeConversationId) {
    return (
      <div className="flex-1 flex flex-col items-center justify-center text-sm text-[#71717a]">
        <span>No operations thread selected. Create a new chat to begin.</span>
      </div>
    );
  }

  return (
    <div className="flex-1 flex flex-col h-full bg-white overflow-hidden relative">
      {/* Scrollable Message List Container */}
      <div className="flex-1 overflow-y-auto px-4 py-8">
        <div className="max-w-3xl mx-auto space-y-8">
          {messages
            .filter((m) => m.role !== "system")
            .map((msg) => (
              <div
                key={msg.id}
                className={cn(
                  "flex gap-4 w-full",
                  msg.role === "user" ? "justify-end" : "justify-start"
                )}
              >
                {/* Profile Avatar Panel */}
                <div
                  className={cn(
                    "flex h-8 w-8 shrink-0 items-center justify-center rounded-none text-xs font-semibold select-none",
                    msg.role === "user" ? "bg-[#f4f4f5] text-[#52525b]" : "bg-[#09090b] text-white"
                  )}
                >
                  {msg.role === "user" ? <User size={14} /> : <Bot size={14} />}
                </div>

                {/* Content Stream Viewport */}
                <div
                  className={cn(
                    "flex flex-col max-w-[85%] min-w-0",
                    msg.role === "user" ? "items-end" : "items-start"
                  )}
                >
                  {msg.content && (
                    <div
                      className={cn(
                        "text-[14px] leading-relaxed break-words whitespace-pre-wrap font-sans antialiased",
                        msg.role === "user"
                          ? "bg-[#f4f4f5] px-4 py-3 text-[#09090b] border border-[#e5e5e5]"
                          : "text-[#1a1a1a] py-1 prose prose-sm max-w-none"
                      )}
                    >
                      {msg.role === "user" ? (
                        msg.content
                      ) : (
                        <ReactMarkdown remarkPlugins={[remarkGfm]}>
                          {msg.content}
                        </ReactMarkdown>
                      )}
                    </div>
                  )}

                  {msg.toolStatus && (
                    <div className="flex items-center gap-2 px-3 py-1.5 mt-2 bg-[#fafafa] border border-[#e5e5e5] text-[11px] font-mono text-[#71717a]">
                      <Activity size={12} className="animate-pulse text-[#09090b]" />
                      {msg.toolStatus}
                    </div>
                  )}

                  {msg.proposedAction && (
                    <div className="mt-3 w-full">
                      <ActionApprovalCard
                        action={msg.proposedAction}
                        onResolved={handleActionResolved}
                      />
                    </div>
                  )}
                </div>
              </div>
            ))}
          <div ref={messagesEndRef} />
        </div>
      </div>

      {/* Input Form Panel */}
      <div className="p-4 bg-white border-t border-[#e5e5e5] shrink-0">
        <div className="max-w-3xl mx-auto">
          <form onSubmit={handleSubmit} className="relative flex items-center">
            <input
              type="text"
              value={input}
              onChange={(e) => setInput(e.target.value)}
              disabled={isProcessing}
              placeholder="Ask the agent to verify metrics or run operational queries..."
              className="w-full h-11 pl-4 pr-12 text-[13px] bg-[#f4f4f5] border border-transparent rounded-none focus:outline-none focus:bg-white focus:border-[#09090b] focus:ring-1 focus:ring-[#09090b] transition-all disabled:opacity-50"
            />
            <button
              type="submit"
              disabled={!input.trim() || isProcessing}
              className="absolute right-1.5 h-8 w-8 flex items-center justify-center bg-[#09090b] text-white rounded-none hover:bg-[#27272a] disabled:opacity-50 disabled:bg-[#e5e5e5] disabled:text-[#a1a1aa] transition-colors"
            >
              {isProcessing ? (
                <Loader2 size={14} className="animate-spin" />
              ) : (
                <Send size={14} />
              )}
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}
