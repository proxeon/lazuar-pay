import { useState, useRef, useEffect, useCallback } from "react";
import { Plus, Mic, Volume2, ArrowUp, Loader2 } from "lucide-react";
import { cn } from "../../lib/utils";

interface ChatInputAreaProps {
  onSend: (text: string) => void;
  isProcessing: boolean;
  activeConversationId: string | null;
  placeholder?: string;
  variant?: "empty" | "active";
}

export default function ChatInputArea({ 
  onSend, 
  isProcessing, 
  activeConversationId, 
  placeholder = "Write a message...", 
  variant = "active" 
}: ChatInputAreaProps) {
  const [input, setInput] = useState("");
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  const adjustHeight = useCallback(() => {
    const el = textareaRef.current;
    if (!el) return;
    el.style.height = "auto";
    const computed = window.getComputedStyle(el);
    const lineHeight = parseInt(computed.lineHeight) || 20;
    const maxHeight = lineHeight * 15;
    el.style.height = `${Math.min(el.scrollHeight, maxHeight)}px`;
  }, []);

  useEffect(() => {
    adjustHeight();
  }, [input, adjustHeight]);

  // Clear input when switching conversations
  useEffect(() => {
    setInput("");
    if (textareaRef.current) textareaRef.current.style.height = "auto";
  }, [activeConversationId]);

  const handleSubmit = () => {
    if (!input.trim() || isProcessing) return;
    onSend(input);
    setInput("");
  };

  return (
    <div className="w-full bg-white border border-[#e5e5e5] rounded-2xl p-3.5 transition-colors focus-within:border-[#09090b] focus-within:ring-0">
      <textarea
        ref={textareaRef}
        value={input}
        onChange={(e) => setInput(e.target.value)}
        onKeyDown={(e) => {
          if (e.key === "Enter" && !e.shiftKey) {
            e.preventDefault();
            handleSubmit();
          }
        }}
        disabled={isProcessing}
        placeholder={placeholder}
        rows={1}
        className="w-full resize-none text-[14px] text-[#09090b] placeholder-[#71717a] focus:outline-none bg-transparent leading-relaxed overflow-y-auto"
        style={{ minHeight: variant === "empty" ? "44px" : "24px" }}
      />
      
      <div className={cn("flex items-center justify-between", variant === "empty" ? "mt-3" : "mt-2")}>
        <button className="p-1.5 text-[#71717a] hover:text-[#09090b] transition-colors" title="Attach file">
          <Plus size={16} />
        </button>
        <div className="flex items-center gap-2.5">
          <span className="text-[10px] font-bold text-[#71717a] border border-[#e5e5e5] px-1.5 py-0.5 select-none bg-white uppercase tracking-wider">
            {variant === "empty" ? "Lazuar Ops Core" : "Ops Core Max"}
          </span>
          <button className="p-1 text-[#71717a] hover:text-[#09090b] transition-colors">
            <Mic size={15} />
          </button>
          <button className="p-1 text-[#71717a] hover:text-[#09090b] transition-colors">
            <Volume2 size={15} />
          </button>
          <button
            onClick={handleSubmit}
            disabled={!input.trim() || isProcessing}
            className={cn(
              "rounded-full bg-[#09090b] text-white flex items-center justify-center hover:bg-[#27272a] disabled:opacity-40 transition-colors",
              variant === "empty" ? "h-7 w-7" : "h-6 w-6"
            )}
          >
            {isProcessing && variant !== "empty" ? (
              <Loader2 size={11} className="animate-spin" />
            ) : (
              <ArrowUp size={variant === "empty" ? 14 : 12} />
            )}
          </button>
        </div>
      </div>
    </div>
  );
}
