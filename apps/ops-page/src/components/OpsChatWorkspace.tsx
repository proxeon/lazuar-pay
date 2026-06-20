// apps/ops-page/src/components/OpsChatWorkspace.tsx
import { useRef, useEffect, useState } from "react";
import { useParams, useNavigate, useOutletContext } from "react-router-dom";
import { ChevronDown } from "lucide-react";
import { useQueryClient, useQuery } from "@tanstack/react-query";
import { cn } from "../lib/utils";
import type { Message } from "../types/chat";
import { useChatStream } from "../hooks/use-chat-stream";
import ChatEmptyState from "./chat/ChatEmptyState";
import ChatMessageBubble from "./chat/ChatMessageBubble";
import ChatInputArea from "./chat/ChatInputArea";
import PromptLibrary from "./chat/PromptLibrary";
import { client, type OpsConversationDto } from "../lib/api-client";

export default function OpsChatWorkspace() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { activeWorkspaceId } = useOutletContext<{ activeWorkspaceId: string | null }>();

  const messagesEndRef = useRef<HTMLDivElement>(null);
  const [messages, setMessages] = useState<Message[]>([]);
  const [isLibraryOpen, setIsLibraryOpen] = useState(false);

  useEffect(() => {
    async function loadMessages() {
      if (!id) {
        setMessages([]);
        return;
      }

      const { data, error } = await client.GET("/ops/chat/conversations/{id}/messages", {
        params: { path: { id } }
      });

      if (!error && data) {
        setMessages(data.map(m => ({
          id: m.id,
          role: m.role as "user" | "assistant" | "system",
          content: m.content,
          executedTools: m.executed_tools,
          proposedAction: m.proposed_action,
          uiRequest: m.ui_request
            ? { ...m.ui_request, is_resolved: m.is_resolved ?? false }
            : undefined
        })));
      }
    }
    loadMessages();
  }, [id]);

  const { handleSend, handleActionResolved, handleUiSubmit, handleUiCancel, isProcessing } = useChatStream(
    id || "new",
    setMessages,
    (newId) => {
      queryClient.invalidateQueries({ queryKey: ["conversations", activeWorkspaceId] });
      if (!id && newId) {
        navigate(`/chat/${newId}`, { replace: true });
      }
    }
  );

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages]);

  const { data: conversationData } = useQuery<OpsConversationDto[]>({
    queryKey: ["conversations", activeWorkspaceId],
    enabled: false
  });

  const activeConversationTitle = !id 
    ? "New Chat" 
    : conversationData?.find(c => c.id === id)?.title || "Active Query Control";

  const visibleMessages = messages.filter((m) => m.role !== "system" || m.content.includes("successfully") || m.content.includes("failed") || m.content.includes("submitted data") || m.content.includes("cancelled"));
  const isEmpty = visibleMessages.length === 0;

  return (
    <div className="flex-1 flex flex-col h-full bg-white overflow-hidden relative">
      {isEmpty ? (
        <ChatEmptyState 
          onSend={handleSend} 
          isProcessing={isProcessing} 
          activeConversationId={id || null} 
          onOpenLibrary={() => setIsLibraryOpen(true)}
        />
      ) : (
        <div className="flex-1 flex flex-col h-full overflow-hidden relative">
          <div className="absolute top-0 left-0 right-0 h-11 bg-white px-6 flex items-center justify-between z-20 select-none border-b border-[#f4f4f5]">
            <div className="flex items-center gap-1.5 cursor-pointer group">
              <span className="text-[13px] font-semibold text-[#09090b] truncate max-w-[300px]">{activeConversationTitle}</span>
              <ChevronDown size={12} className="text-[#71717a] group-hover:text-[#09090b] transition-colors shrink-0" />
            </div>
            <div className="absolute top-full left-0 right-0 h-8 bg-gradient-to-b from-white to-transparent pointer-events-none" />
          </div>
          
          <div className="flex-1 overflow-y-auto px-6 pt-16 pb-48">
            <div className="max-w-[680px] mx-auto space-y-8">
              {visibleMessages.map((msg, index) => (
                <div key={msg.id} className={cn("flex flex-col w-full", msg.role === "user" ? "items-end" : "items-start")}>
                  <ChatMessageBubble
                    msg={msg}
                    onSend={handleSend}
                    onActionResolved={handleActionResolved}
                    onUiSubmit={handleUiSubmit}
                    onUiCancel={handleUiCancel}
                    previousUserMsgContent={visibleMessages[index - 1]?.content}
                  />
                </div>
              ))}
              <div ref={messagesEndRef} />
            </div>
          </div>
          
          <div className="absolute bottom-0 left-0 right-0 bg-white pt-4 pb-4 px-4 z-10 pointer-events-none">
            <div className="absolute bottom-full left-0 right-0 h-12 bg-gradient-to-t from-white via-white/90 to-transparent pointer-events-none" />
            <div className="max-w-[760px] mx-auto pointer-events-auto">
              <ChatInputArea 
                onSend={handleSend} 
                isProcessing={isProcessing} 
                activeConversationId={id || null} 
                onOpenLibrary={() => setIsLibraryOpen(true)}
              />
              <p className="text-[11px] text-[#71717a] text-center mt-2.5">
                Lazuar Ops applies actions directly to the active workspace. Verify proposed commands carefully.
              </p>
            </div>
          </div>
        </div>
      )}

      <PromptLibrary 
        isOpen={isLibraryOpen} 
        onClose={() => setIsLibraryOpen(false)} 
        onSelect={(query) => {
          setIsLibraryOpen(false);
          handleSend(query);
        }} 
      />
    </div>
  );
}
