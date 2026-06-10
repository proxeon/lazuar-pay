import { useRef, useEffect } from "react";
import { ChevronDown } from "lucide-react";
import { cn } from "../lib/utils";
import type { Message } from "../types/chat";
import { useChatStream } from "../hooks/use-chat-stream";

import ChatEmptyState from "./chat/ChatEmptyState";
import ChatMessageBubble from "./chat/ChatMessageBubble";
import ChatInputArea from "./chat/ChatInputArea";

interface OpsChatWorkspaceProps {
  activeConversationId: string | null;
  setActiveConversationId: (id: string) => void;
  messages: Message[];
  setMessages: (updater: (prev: Message[]) => Message[]) => void;
  onStreamComplete: () => void;
}

export default function OpsChatWorkspace({
  activeConversationId, setActiveConversationId, messages, setMessages, onStreamComplete
}: OpsChatWorkspaceProps) {
  
  const messagesEndRef = useRef<HTMLDivElement>(null);
  
  const { handleSend, handleActionResolved, isProcessing } = useChatStream(
    activeConversationId, 
    setMessages, 
    (newId) => {
      onStreamComplete(); 
      // Safely transition the active state to the actual database GUID
      if (activeConversationId === "new" && newId) {
        setActiveConversationId(newId);
      }
    }
  );

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [messages]);

  const visibleMessages = messages.filter((m) => m.role !== "system" || m.content.includes("successfully") || m.content.includes("failed"));
  const isEmpty = visibleMessages.length === 0;

  return (
    <div className="flex-1 flex flex-col h-full bg-white overflow-hidden relative">
      {isEmpty ? (
        <ChatEmptyState onSend={handleSend} isProcessing={isProcessing} activeConversationId={activeConversationId} />
      ) : (
        <div className="flex-1 flex flex-col h-full overflow-hidden relative">
          
          <div className="absolute top-0 left-0 right-0 h-11 bg-white px-6 flex items-center justify-between z-20 select-none">
            <div className="flex items-center gap-1.5 cursor-pointer group">
              <span className="text-[13px] font-semibold text-[#09090b]">Active Query Control</span>
              <ChevronDown size={12} className="text-[#71717a] group-hover:text-[#09090b] transition-colors" />
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
              <ChatInputArea onSend={handleSend} isProcessing={isProcessing} activeConversationId={activeConversationId} />
              <p className="text-[11px] text-[#71717a] text-center mt-2.5">
                Lazuar Ops applies actions directly to the active workspace. Verify proposed commands carefully.
              </p>
            </div>
          </div>

        </div>
      )}
    </div>
  );
}
