import { useState, useRef, useEffect, useCallback } from "react";
import { 
  Plus, 
  Mic, 
  Volume2, 
  Sparkles, 
  Copy, 
  ThumbsUp, 
  ThumbsDown, 
  RotateCcw, 
  Check,
  Activity,
  Terminal,
  Database,
  Globe,
  ArrowUp,
  Loader2,
  ChevronDown,
  Pencil
} from "lucide-react";
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
  const [copiedId, setCopiedId] = useState<string | null>(null);
  
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const emptyTextareaRef = useRef<HTMLTextAreaElement>(null);
  const activeTextareaRef = useRef<HTMLTextAreaElement>(null);

  const [greeting, setGreeting] = useState("Hello");

  useEffect(() => {
    const hours = new Date().getHours();
    if (hours >= 22 || hours < 5) setGreeting("Hello, night owl");
    else if (hours >= 5 && hours < 12) setGreeting("Good morning");
    else if (hours >= 12 && hours < 17) setGreeting("Good afternoon");
    else setGreeting("Good evening");
  }, []);

  const scrollToBottom = () => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  };

  useEffect(() => {
    scrollToBottom();
  }, [messages]);

  const adjustHeight = useCallback((el: HTMLTextAreaElement | null) => {
    if (!el) return;
    el.style.height = "auto";
    const computed = window.getComputedStyle(el);
    const lineHeight = parseInt(computed.lineHeight) || 20;
    const maxHeight = lineHeight * 15;
    el.style.height = `${Math.min(el.scrollHeight, maxHeight)}px`;
  }, []);

  useEffect(() => {
    adjustHeight(emptyTextareaRef.current);
    adjustHeight(activeTextareaRef.current);
  }, [input, adjustHeight]);

  useEffect(() => {
    setInput("");
    if (emptyTextareaRef.current) emptyTextareaRef.current.style.height = "auto";
    if (activeTextareaRef.current) activeTextareaRef.current.style.height = "auto";
  }, [activeConversationId]);

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

  const handleSend = async (textToSend: string) => {
    if (!textToSend.trim() || isProcessing || !activeConversationId) return;

    const userText = textToSend.trim();
    setInput("");

    const userMsgId = Date.now().toString();
    setMessages((prev) => [...prev, { id: userMsgId, role: "user", content: userText }]);

    const assistantMsgId = (Date.now() + 1).toString();
    setMessages((prev) => [...prev, { id: assistantMsgId, role: "assistant", content: "", isStreaming: true }]);
    setIsProcessing(true);

    await executeStreamCall(userText, assistantMsgId);
  };

  const handleActionResolved = async (success: boolean, message?: string, actionRef?: ProposedActionDto) => {
    const systemFeedback = success
      ? `[System: The action was executed successfully. Waiting for next instruction.]`
      : `[System: The action failed or was cancelled. Reason: ${message}]`;

    setMessages((prev) => [
      ...prev,
      { id: Date.now().toString(), role: "system", content: systemFeedback }
    ]);

    // MAGIC LOOP: If it failed due to an API error, automatically ping the AI to fix its mistake
    if (!success && actionRef && message !== "Action cancelled by user.") {
      const fixPrompt = `System Error Notification: I tried to execute the tool '${actionRef.tool_name}' with the payload ${JSON.stringify(actionRef.command_payload)}. The system rejected it with this error: "${message}". Please analyze the error, apologize, and propose a corrected action.`;
      
      const assistantMsgId = (Date.now() + 1).toString();
      setMessages((prev) => [...prev, { id: assistantMsgId, role: "assistant", content: "", isStreaming: true }]);
      setIsProcessing(true);
      
      // Send the hidden context prompt to the backend
      await executeStreamCall(fixPrompt, assistantMsgId);
    }
  };

  const handleCopyToClipboard = (text: string, msgId: string) => {
    navigator.clipboard.writeText(text);
    setCopiedId(msgId);
    setTimeout(() => setCopiedId(null), 1500);
  };

  const visibleMessages = messages.filter((m) => m.role !== "system" || m.content.includes("successfully") || m.content.includes("failed"));
  const isEmpty = visibleMessages.length === 0;

  const quickActions = [
    { label: "Verify Metrics", icon: Activity, query: "Check the active container performance metrics and system load." },
    { label: "Analyze Logs", icon: Terminal, query: "Scan the latest reverse proxy server block redirects for errors." },
    { label: "DB Health", icon: Database, query: "Verify database transaction lag, locked queries, and cluster health." },
    { label: "DNS Status", icon: Globe, query: "Perform a routing health check on external proxy and edge nodes." }
  ];

  if (!activeConversationId) {
    return (
      <div className="flex-1 flex flex-col items-center justify-center text-sm text-[#71717a]">
        <span>No operations thread selected. Create a new chat to begin.</span>
      </div>
    );
  }

  return (
    <div className="flex-1 flex flex-col h-full bg-white overflow-hidden relative">
      {isEmpty ? (
        <div className="flex-1 flex flex-col items-center justify-center px-6 bg-white overflow-y-auto">
          <div className="w-full max-w-[720px] text-center flex flex-col items-center">
            
            <div className="flex items-center gap-3.5 mb-8 select-none">
              <Sparkles className="h-9 w-9 text-orange-500 fill-orange-100" />
              <h1 className="text-3xl sm:text-4xl font-semibold tracking-tight text-[#09090b] font-serif">
                {greeting}
              </h1>
            </div>

            <div className="w-full bg-white border border-[#e5e5e5] rounded-2xl p-4 transition-colors focus-within:border-[#09090b] focus-within:ring-0 mb-6">
              <textarea
                ref={emptyTextareaRef}
                value={input}
                onChange={(e) => setInput(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === "Enter" && !e.shiftKey) {
                    e.preventDefault();
                    handleSend(input);
                  }
                }}
                disabled={isProcessing}
                placeholder="How can I help you today?"
                rows={1}
                className="w-full resize-none text-[15px] text-[#09090b] placeholder-[#71717a] focus:outline-none bg-transparent leading-relaxed overflow-y-auto"
                style={{ minHeight: "44px" }}
              />
              <div className="flex items-center justify-between mt-3">
                <button className="p-1.5 text-[#71717a] hover:text-[#09090b] transition-colors">
                  <Plus size={18} />
                </button>
                <div className="flex items-center gap-3">
                  <span className="text-xs text-[#71717a] font-medium border border-[#e5e5e5] px-2 py-0.5 select-none bg-white">
                    Lazuar Ops Core
                  </span>
                  <button className="p-1.5 text-[#71717a] hover:text-[#09090b] transition-colors">
                    <Mic size={17} />
                  </button>
                  <button className="p-1.5 text-[#71717a] hover:text-[#09090b] transition-colors">
                    <Volume2 size={17} />
                  </button>
                  <button
                    onClick={() => handleSend(input)}
                    disabled={!input.trim() || isProcessing}
                    className="h-7 w-7 rounded-full bg-[#09090b] text-white flex items-center justify-center hover:bg-[#27272a] disabled:opacity-40 transition-colors"
                  >
                    <ArrowUp size={14} />
                  </button>
                </div>
              </div>
            </div>

            <div className="flex flex-wrap items-center justify-center gap-2">
              {quickActions.map((action) => (
                <button
                  key={action.label}
                  onClick={() => handleSend(action.query)}
                  className="flex items-center gap-2 border border-[#e5e5e5] bg-white px-3 py-1.5 hover:bg-[#fafafa] hover:border-[#cbcbcb] text-[13px] text-[#52525b] transition-all font-medium rounded-lg"
                >
                  <action.icon size={13} className="text-[#71717a]" />
                  <span>{action.label}</span>
                </button>
              ))}
            </div>

          </div>
        </div>
      ) : (
        <div className="flex-1 flex flex-col h-full overflow-hidden relative">
          
          <div className="absolute top-0 left-0 right-0 h-11 bg-white px-6 flex items-center justify-between z-20 select-none">
            <div className="flex items-center gap-1.5 cursor-pointer group">
              <span className="text-[13px] font-semibold text-[#09090b]">Active Query Control</span>
              <ChevronDown size={12} className="text-[#71717a] group-hover:text-[#09090b] transition-colors" />
            </div>
            <button className="h-7 border border-[#e5e5e5] bg-white text-[10px] font-bold uppercase tracking-widest text-[#09090b] px-3 hover:bg-[#fafafa] transition-colors">
              Share
            </button>
            <div className="absolute top-full left-0 right-0 h-8 bg-gradient-to-b from-white to-transparent pointer-events-none" />
          </div>

          <div className="flex-1 overflow-y-auto px-6 pt-16 pb-48">
            <div className="max-w-[680px] mx-auto space-y-8">
              {visibleMessages.map((msg) => (
                <div
                  key={msg.id}
                  className={cn(
                    "flex flex-col w-full",
                    msg.role === "user" ? "items-end" : "items-start"
                  )}
                >
                  {msg.role === "system" ? (
                    <div className="w-full flex justify-center py-2">
                      <span className={cn("text-[11px] font-mono px-3 py-1.5 rounded-sm border", msg.content.includes("successfully") ? "bg-emerald-50 text-emerald-700 border-emerald-200" : "bg-rose-50 text-rose-700 border-rose-200")}>
                        {msg.content}
                      </span>
                    </div>
                  ) : msg.role === "user" ? (
                    <div className="group flex flex-col items-end w-full relative">
                      <div className="bg-[#f4f4f5] px-4 py-2.5 text-[14px] leading-relaxed text-[#09090b] max-w-[80%] rounded-2xl border border-[#e5e5e5] break-words shrink-0">
                        {msg.content}
                      </div>
                      <div className="opacity-0 group-hover:opacity-100 transition-opacity flex items-center gap-2 mt-2 select-none text-[#71717a] text-[11px] font-sans">
                        <button 
                          onClick={() => handleSend(msg.content)}
                          className="p-1 hover:text-[#09090b] transition-colors" 
                          title="Retry"
                        >
                          <RotateCcw size={12} />
                        </button>
                        <button className="p-1 hover:text-[#09090b] transition-colors" title="Edit">
                          <Pencil size={12} />
                        </button>
                        <button 
                          onClick={() => handleCopyToClipboard(msg.content, msg.id)}
                          className="p-1 hover:text-[#09090b] transition-colors" 
                          title="Copy"
                        >
                          {copiedId === msg.id ? <Check size={12} className="text-emerald-600" /> : <Copy size={12} />}
                        </button>
                      </div>
                    </div>
                  ) : (
                    <div className="w-full flex flex-col items-start min-w-0">
                      {msg.content && (
                        <div className="text-[15px] leading-relaxed text-[#09090b] prose prose-sm max-w-none break-words whitespace-pre-wrap font-sans antialiased w-full">
                          <ReactMarkdown remarkPlugins={[remarkGfm]}>
                            {msg.content}
                          </ReactMarkdown>
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

                      {!msg.isStreaming && (
                        <div className="flex items-center gap-1.5 mt-4 text-[#71717a]">
                          <button
                            onClick={() => handleCopyToClipboard(msg.content, msg.id)}
                            className="p-1.5 hover:bg-[#f4f4f5] hover:text-[#09090b] transition-all"
                            title="Copy reply"
                          >
                            {copiedId === msg.id ? <Check size={14} className="text-emerald-600" /> : <Copy size={14} />}
                          </button>
                          <button className="p-1.5 hover:bg-[#f4f4f5] hover:text-[#09090b] transition-all" title="Good response">
                            <ThumbsUp size={14} />
                          </button>
                          <button className="p-1.5 hover:bg-[#f4f4f5] hover:text-[#09090b] transition-all" title="Bad response">
                            <ThumbsDown size={14} />
                          </button>
                          <button 
                            onClick={() => handleSend(visibleMessages[visibleMessages.indexOf(msg) - 1]?.content || "")}
                            className="p-1.5 hover:bg-[#f4f4f5] hover:text-[#09090b] transition-all" 
                            title="Regenerate reply"
                          >
                            <RotateCcw size={14} />
                          </button>
                        </div>
                      )}
                    </div>
                  )}
                </div>
              ))}
              <div ref={messagesEndRef} />
            </div>
          </div>

          <div className="absolute bottom-0 left-0 right-0 bg-white pt-4 pb-4 px-4 z-10 pointer-events-none">
            
            <div className="absolute bottom-full left-0 right-0 h-12 bg-gradient-to-t from-white via-white/90 to-transparent pointer-events-none" />
            
            <div className="max-w-[760px] mx-auto pointer-events-auto">
              <div className="w-full bg-white border border-[#e5e5e5] rounded-2xl p-3.5 transition-colors focus-within:border-[#09090b] focus-within:ring-0">
                <textarea
                  ref={activeTextareaRef}
                  value={input}
                  onChange={(e) => setInput(e.target.value)}
                  onKeyDown={(e) => {
                    if (e.key === "Enter" && !e.shiftKey) {
                      e.preventDefault();
                      handleSend(input);
                    }
                  }}
                  disabled={isProcessing}
                  placeholder="Write a message..."
                  rows={1}
                  className="w-full resize-none text-[14px] text-[#09090b] placeholder-[#71717a] focus:outline-none bg-transparent leading-relaxed overflow-y-auto"
                  style={{ minHeight: "24px" }}
                />
                <div className="flex items-center justify-between mt-2">
                  <button className="p-1 text-[#71717a] hover:text-[#09090b] transition-colors" title="Attach file">
                    <Plus size={16} />
                  </button>
                  <div className="flex items-center gap-2.5">
                    <span className="text-[10px] font-bold text-[#71717a] border border-[#e5e5e5] px-1.5 py-0.5 select-none bg-white uppercase tracking-wider">
                      Ops Core Max
                    </span>
                    <button className="p-1 text-[#71717a] hover:text-[#09090b] transition-colors">
                      <Mic size={15} />
                    </button>
                    <button className="p-1 text-[#71717a] hover:text-[#09090b] transition-colors">
                      <Volume2 size={15} />
                    </button>
                    <button
                      onClick={() => handleSend(input)}
                      disabled={!input.trim() || isProcessing}
                      className="h-6 w-6 rounded-full bg-[#09090b] text-white flex items-center justify-center hover:bg-[#27272a] disabled:opacity-40 transition-colors"
                    >
                      {isProcessing ? <Loader2 size={11} className="animate-spin" /> : <ArrowUp size={12} />}
                    </button>
                  </div>
                </div>
              </div>
              <p className="text-[11px] text-[#71717a] text-center mt-2.5">
                Lazuar Ops is powered by cognitive core. Verify system commands before executing.
              </p>
            </div>
          </div>

        </div>
      )}

    </div>
  );
}
