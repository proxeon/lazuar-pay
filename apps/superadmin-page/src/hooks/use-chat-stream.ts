// apps/ops-page/src/hooks/use-chat-stream.ts
import { useState } from "react";
import { API_URL, type ChatStreamChunkDto, type ProposedActionDto } from "../lib/api-client";
import type { Message } from "../types/chat";

export function useChatStream(
  activeConversationId: string | null | undefined,
  setMessages: (updater: (prev: Message[]) => Message[]) => void,
  onStreamComplete: (newConversationId?: string) => void
) {
  const [isProcessing, setIsProcessing] = useState(false);

  const executeStreamCall = async (payloadMessage: string, targetAssistantMsgId: string) => {
    let generatedConvId: string | undefined = undefined;
    const isNew = !activeConversationId || activeConversationId === "new";
    const tenantId = localStorage.getItem("ops_active_workspace_id") || "";

    try {
      const response = await fetch(`${API_URL}/ops/chat/stream`, {
        method: "POST",
        headers: { 
          "Content-Type": "application/json",
          "X-Tenant-Id": tenantId 
        },
        body: JSON.stringify({ 
          message: payloadMessage,
          conversation_id: isNew ? undefined : activeConversationId 
        }),
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

                if (chunk.type === "conversation_id" && chunk.content) {
                  generatedConvId = chunk.content;
                  continue;
                }

                setMessages((prev) =>
                  prev.map((msg) => {
                    if (msg.id !== targetAssistantMsgId) return msg;
                    if (chunk.type === "text" && chunk.content) return { ...msg, content: msg.content + chunk.content };
                    if (chunk.type === "tool_status" && chunk.executed_tools) return { ...msg, executedTools: chunk.executed_tools };
                    if (chunk.type === "proposed_action" && chunk.proposed_action) return { ...msg, proposedAction: chunk.proposed_action };
                    if (chunk.type === "ui_request" && chunk.ui_request) return { ...msg, uiRequest: chunk.ui_request };
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
      setMessages((prev) => prev.map((msg) => msg.id === targetAssistantMsgId ? { ...msg, content: "Network error occurred." } : msg));
    } finally {
      setMessages((prev) => prev.map((msg) => msg.id === targetAssistantMsgId ? { ...msg, isStreaming: false } : msg));
      setIsProcessing(false);
      onStreamComplete(isNew && generatedConvId ? generatedConvId : undefined);
    }
  };

  const handleSend = async (textToSend: string) => {
    if (!textToSend.trim() || isProcessing) return;

    const userText = textToSend.trim();
    const userMsgId = Date.now().toString();
    const assistantMsgId = (Date.now() + 1).toString();

    setMessages((prev) => [
      ...prev, 
      { id: userMsgId, role: "user", content: userText },
      { id: assistantMsgId, role: "assistant", content: "", isStreaming: true }
    ]);
    
    setIsProcessing(true);
    await executeStreamCall(userText, assistantMsgId);
  };

  const handleActionResolved = async (success: boolean, message?: string, actionRef?: ProposedActionDto) => {
    const systemFeedback = success
      ? `[System: The action was executed successfully. Waiting for next instruction.]`
      : `[System: The action failed or was cancelled. Reason: ${message}]`;

    if (activeConversationId && activeConversationId !== "new") {
      try {
        await fetch(`${API_URL}/ops/chat/conversations/${activeConversationId}/system-message`, {
          method: "POST",
          headers: { 
            "Content-Type": "application/json",
            "X-Tenant-Id": localStorage.getItem("ops_active_workspace_id") || ""
          },
          body: JSON.stringify({ message: systemFeedback }),
          credentials: "include"
        });
      } catch (e) {
        console.error("Failed to persist system message", e);
      }
    }

    setMessages((prev) => [...prev, { id: Date.now().toString(), role: "system", content: systemFeedback }]);

    if (!success && actionRef && message !== "Action cancelled by user.") {
      const fixPrompt = `System Error Notification: I tried to execute the tool '${actionRef.tool_name}'. The system rejected it with this error: "${message}". Please analyze the error, apologize, and propose a corrected action.`;
      const assistantMsgId = (Date.now() + 1).toString();
      setMessages((prev) => [...prev, { id: assistantMsgId, role: "assistant", content: "", isStreaming: true }]);
      setIsProcessing(true);
      await executeStreamCall(fixPrompt, assistantMsgId);
    }
  };

  const markUiRequestResolved = async (messageId: string) => {
    setMessages(prev => prev.map(msg => {
      if (msg.id === messageId && msg.uiRequest) {
        return { ...msg, uiRequest: { ...msg.uiRequest, is_resolved: true } };
      }
      return msg;
    }));

    if (activeConversationId && activeConversationId !== "new") {
      try {
        await fetch(`${API_URL}/ops/chat/messages/${messageId}/resolve`, {
          method: "PUT",
          headers: { "X-Tenant-Id": localStorage.getItem("ops_active_workspace_id") || "" },
          credentials: "include"
        });
      } catch (e) {
        console.error("Failed to mark UI request as resolved in DB", e);
      }
    }
  };

  const handleUiSubmit = async (messageId: string, toolName: string, formData: Record<string, any>) => {
    await markUiRequestResolved(messageId);
    const systemFeedback = `[System: User submitted data for ${toolName}: ${JSON.stringify(formData)}]`;
    
    const userMsgId = Date.now().toString();
    const assistantMsgId = (Date.now() + 1).toString();

    setMessages(prev => [
      ...prev,
      { id: userMsgId, role: "user", content: systemFeedback },
      { id: assistantMsgId, role: "assistant", content: "", isStreaming: true }
    ]);

    setIsProcessing(true);
    await executeStreamCall(systemFeedback, assistantMsgId);
  };

  const handleUiCancel = async (messageId: string) => {
    await markUiRequestResolved(messageId);
    const systemFeedback = `[System: User cancelled the form submission.]`;
    
    setMessages(prev => [
      ...prev,
      { id: Date.now().toString(), role: "system", content: systemFeedback }
    ]);
  };

  return { handleSend, handleActionResolved, handleUiSubmit, handleUiCancel, isProcessing };
}
