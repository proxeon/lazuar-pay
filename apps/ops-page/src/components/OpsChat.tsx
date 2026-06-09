import { useState, useRef, useEffect } from "react";
import { Send, Loader2, Bot, User, Activity } from "lucide-react";
import { cn } from "../lib/utils";
import ActionApprovalCard from "./ActionApprovalCard";
import type { ChatStreamChunkDto, ProposedActionDto } from "../lib/api-client";

interface Message {
  id: string;
  role: "user" | "assistant" | "system";
  content: string;
  isStreaming?: boolean;
  toolStatus?: string;
  proposedAction?: ProposedActionDto;
}

export default function OpsChat() {
  const [messages, setMessages] = useState<Message[]>([
    { id: "1", role: "assistant", content: "Hi! I am Lazuar Ops. How can I help you manage your ecosystem today?" }
  ]);
  const [input, setInput] = useState("");
  const [isProcessing, setIsProcessing] = useState(false);
  const messagesEndRef = useRef<HTMLDivElement>(null);

  const scrollToBottom = () => messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  useEffect(() => scrollToBottom(), [messages]);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!input.trim() || isProcessing) return;

    const userText = input.trim();
    setInput("");
    
    const userMsgId = Date.now().toString();
    setMessages(prev => [...prev, { id: userMsgId, role: "user", content: userText }]);
    
    const assistantMsgId = (Date.now() + 1).toString();
    setMessages(prev => [...prev, { id: assistantMsgId, role: "assistant", content: "", isStreaming: true }]);
    setIsProcessing(true);

    try {
      const response = await fetch("http://localhost:8080/api/v1/ops/chat/stream", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ message: userText }),
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
                
                setMessages(prev => prev.map(msg => {
                  if (msg.id !== assistantMsgId) return msg;
                  
                  if (chunk.type === "text" && chunk.content) {
                    return { ...msg, content: msg.content + chunk.content, toolStatus: undefined };
                  } else if (chunk.type === "tool_status" && chunk.tool_name) {
                    return { ...msg, toolStatus: `Running ${chunk.tool_name}...` };
                  } else if (chunk.type === "proposed_action" && chunk.proposed_action) {
                    return { ...msg, proposedAction: chunk.proposed_action, toolStatus: undefined };
                  }
                  return msg;
                }));
              } catch (e) {
                console.error("Failed to parse SSE chunk", e);
              }
            }
          }
        }
      }
    } catch (error) {
      setMessages(prev => prev.map(msg => msg.id === assistantMsgId ? { ...msg, content: "Network error occurred." } : msg));
    } finally {
      setMessages(prev => prev.map(msg => msg.id === assistantMsgId ? { ...msg, isStreaming: false, toolStatus: undefined } : msg));
      setIsProcessing(false);
    }
  };

  const handleActionResolved = async (success: boolean, message?: string) => {
    // Inject system feedback into the chat to inform the AI of the result
    const systemFeedback = success 
      ? `[System: The action was executed successfully.]`
      : `[System: The action failed or was cancelled. Reason: ${message}]`;

    setMessages(prev => [...prev, { id: Date.now().toString(), role: "system", content: systemFeedback }]);

    // Only auto-trigger the LLM to follow up if it failed (so it can apologize/re-propose)
    if (!success) {
      const assistantMsgId = (Date.now() + 1).toString();
      setMessages(prev => [...prev, { id: assistantMsgId, role: "assistant", content: "", isStreaming: true }]);
      setIsProcessing(true);

      try {
        const response = await fetch("http://localhost:8080/api/v1/ops/chat/stream", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ message: systemFeedback }),
          credentials: "include"
        });
        
        // Similar reading logic as above for the error feedback loop
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
                  setMessages(prev => prev.map(msg => msg.id === assistantMsgId && chunk.type === "text" && chunk.content ? { ...msg, content: msg.content + chunk.content } : msg));
                } catch (e) {}
              }
            }
          }
        }
      } finally {
        setMessages(prev => prev.map(msg => msg.id === assistantMsgId ? { ...msg, isStreaming: false } : msg));
        setIsProcessing(false);
      }
    }
  };

  return (
    <div className="flex flex-col h-full bg-white border-l border-[#e5e5e5] shadow-[-4px_0_24px_rgba(0,0,0,0.02)]">
      <div className="px-5 py-4 border-b border-[#f4f4f5] bg-[#fafafa]/50 flex items-center gap-2">
        <Bot size={18} className="text-[#09090b]" />
        <h2 className="text-[13px] font-bold uppercase tracking-widest text-[#09090b]">Cognitive Core</h2>
      </div>

      <div className="flex-1 overflow-y-auto p-4 space-y-6">
        {messages.filter(m => m.role !== "system").map((msg) => (
          <div key={msg.id} className={cn("flex gap-3", msg.role === "user" ? "flex-row-reverse" : "flex-row")}>
            <div className={cn("flex h-7 w-7 shrink-0 items-center justify-center rounded-none", msg.role === "user" ? "bg-[#f4f4f5] text-[#52525b]" : "bg-[#09090b] text-white")}>
              {msg.role === "user" ? <User size={14} /> : <Bot size={14} />}
            </div>
            
            <div className={cn("flex flex-col max-w-[85%]", msg.role === "user" ? "items-end" : "items-start")}>
              {msg.content && (
                <div className={cn("px-4 py-2.5 text-[13px] leading-relaxed", msg.role === "user" ? "bg-[#f4f4f5] text-[#09090b]" : "bg-transparent text-[#1a1a1a]")}>
                  {msg.content}
                </div>
              )}
              
              {msg.toolStatus && (
                <div className="flex items-center gap-2 px-3 py-1.5 mt-1 bg-[#fafafa] border border-[#e5e5e5] text-[11px] font-mono text-[#71717a] rounded-sm">
                  <Activity size={12} className="animate-pulse text-[#09090b]" /> {msg.toolStatus}
                </div>
              )}

              {msg.proposedAction && (
                <ActionApprovalCard action={msg.proposedAction} onResolved={handleActionResolved} />
              )}
            </div>
          </div>
        ))}
        <div ref={messagesEndRef} />
      </div>

      <div className="p-4 bg-white border-t border-[#f4f4f5]">
        <form onSubmit={handleSubmit} className="relative flex items-center">
          <input
            type="text"
            value={input}
            onChange={(e) => setInput(e.target.value)}
            disabled={isProcessing}
            placeholder="Ask the agent to read data or perform actions..."
            className="w-full h-11 pl-4 pr-12 text-[13px] bg-[#f4f4f5] border border-transparent rounded-none focus:outline-none focus:bg-white focus:border-[#09090b] focus:ring-1 focus:ring-[#09090b] transition-all disabled:opacity-50"
          />
          <button
            type="submit"
            disabled={!input.trim() || isProcessing}
            className="absolute right-1.5 h-8 w-8 flex items-center justify-center bg-[#09090b] text-white rounded-sm hover:bg-[#27272a] disabled:opacity-50 disabled:bg-[#e5e5e5] disabled:text-[#a1a1aa] transition-colors"
          >
            {isProcessing ? <Loader2 size={14} className="animate-spin" /> : <Send size={14} />}
          </button>
        </form>
      </div>
    </div>
  );
}
