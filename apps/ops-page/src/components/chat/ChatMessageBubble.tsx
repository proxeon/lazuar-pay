import { useState } from "react";
import { Copy, ThumbsUp, ThumbsDown, RotateCcw, Check, Activity, Pencil } from "lucide-react";
import { cn } from "../../lib/utils";
import ActionApprovalCard from "../ActionApprovalCard";
import type { Message } from "../../types/chat";
import type { ProposedActionDto } from "../../lib/api-client";
import { MarkdownContent } from "./MarkdownContent";

interface ChatMessageBubbleProps {
  msg: Message;
  onSend: (text: string) => void;
  onActionResolved: (success: boolean, message?: string, actionRef?: ProposedActionDto) => void;
  previousUserMsgContent?: string;
}

export default function ChatMessageBubble({ 
  msg, 
  onSend, 
  onActionResolved, 
  previousUserMsgContent 
}: ChatMessageBubbleProps) {
  const [copiedId, setCopiedId] = useState<string | null>(null);

  const handleCopyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text);
    setCopiedId(msg.id);
    setTimeout(() => setCopiedId(null), 1500);
  };

  if (msg.role === "system") {
    return (
      <div className="w-full flex justify-center py-2">
        <span className={cn("text-[11px] font-mono px-3 py-1.5 rounded-sm border", 
          msg.content.includes("successfully") ? "bg-emerald-50 text-emerald-700 border-emerald-200" : "bg-rose-50 text-rose-700 border-rose-200"
        )}>
          {msg.content}
        </span>
      </div>
    );
  }

  if (msg.role === "user") {
    return (
      <div className="group flex flex-col items-end w-full relative">
        <div className="bg-[#f4f4f5] px-4 py-2.5 max-w-[80%] rounded-2xl border border-[#e5e5e5] break-words shrink-0 [&_.prose>*:first-child]:mt-0 [&_.prose>*:last-child]:mb-0">
          <MarkdownContent content={msg.content} />
        </div>
        <div className="opacity-0 group-hover:opacity-100 transition-opacity flex items-center gap-2 mt-2 select-none text-[#71717a] text-[11px] font-sans">
          <button onClick={() => onSend(msg.content)} className="p-1 hover:text-[#09090b] transition-colors" title="Retry">
            <RotateCcw size={12} />
          </button>
          <button className="p-1 hover:text-[#09090b] transition-colors" title="Edit">
            <Pencil size={12} />
          </button>
          <button onClick={() => handleCopyToClipboard(msg.content)} className="p-1 hover:text-[#09090b] transition-colors" title="Copy">
            {copiedId === msg.id ? <Check size={12} className="text-emerald-600" /> : <Copy size={12} />}
          </button>
        </div>
      </div>
    );
  }

  // Assistant Role
  return (
    <div className="w-full flex flex-col items-start min-w-0">
      {msg.content && (
        <div className="w-full">
          <MarkdownContent content={msg.content} />
        </div>
      )}

      {msg.toolStatus && (
        <div className="flex items-center gap-2 px-3 py-1.5 mt-2 bg-[#fafafa] border border-[#e5e5e5] text-[11px] font-mono text-[#71717a] rounded-sm shadow-sm">
          <Activity size={12} className="animate-pulse text-[#09090b]" />
          {msg.toolStatus}
        </div>
      )}

      {msg.proposedAction && (
        <div className="mt-3 w-full">
          <ActionApprovalCard
            action={msg.proposedAction}
            onResolved={onActionResolved}
          />
        </div>
      )}

      {!msg.isStreaming && (
        <div className="flex items-center gap-1.5 mt-4 text-[#71717a]">
          <button onClick={() => handleCopyToClipboard(msg.content)} className="p-1.5 hover:bg-[#f4f4f5] hover:text-[#09090b] transition-all rounded-sm" title="Copy reply">
            {copiedId === msg.id ? <Check size={14} className="text-emerald-600" /> : <Copy size={14} />}
          </button>
          <button className="p-1.5 hover:bg-[#f4f4f5] hover:text-[#09090b] transition-all rounded-sm" title="Good response">
            <ThumbsUp size={14} />
          </button>
          <button className="p-1.5 hover:bg-[#f4f4f5] hover:text-[#09090b] transition-all rounded-sm" title="Bad response">
            <ThumbsDown size={14} />
          </button>
          {previousUserMsgContent && (
            <button onClick={() => onSend(previousUserMsgContent)} className="p-1.5 hover:bg-[#f4f4f5] hover:text-[#09090b] transition-all rounded-sm" title="Regenerate reply">
              <RotateCcw size={14} />
            </button>
          )}
        </div>
      )}
    </div>
  );
}
