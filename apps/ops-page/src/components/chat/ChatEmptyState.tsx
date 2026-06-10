import { useState, useEffect } from "react";
import { Sparkles } from "lucide-react";
import ChatInputArea from "./ChatInputArea";

interface ChatEmptyStateProps {
  onSend: (text: string) => void;
  isProcessing: boolean;
  activeConversationId: string | null;
  onOpenLibrary: () => void;
}

export default function ChatEmptyState({ onSend, isProcessing, activeConversationId, onOpenLibrary }: ChatEmptyStateProps) {
  const [greeting, setGreeting] = useState("Hello");

  useEffect(() => {
    const hours = new Date().getHours();
    if (hours >= 22 || hours < 5) setGreeting("Hello, night owl");
    else if (hours >= 5 && hours < 12) setGreeting("Good morning");
    else if (hours >= 12 && hours < 17) setGreeting("Good afternoon");
    else setGreeting("Good evening");
  }, []);

  return (
    <div className="flex-1 flex flex-col items-center justify-center px-6 bg-white overflow-y-auto">
      <div className="w-full max-w-[720px] text-center flex flex-col items-center">
        <div className="flex items-center gap-3.5 mb-8 select-none">
          <Sparkles className="h-9 w-9 text-orange-500 fill-orange-100" />
          <h1 className="text-3xl sm:text-4xl font-semibold tracking-tight text-[#09090b] font-serif">
            {greeting}
          </h1>
        </div>
        
        <div className="w-full mb-6 text-left">
          <ChatInputArea
            onSend={onSend}
            isProcessing={isProcessing}
            activeConversationId={activeConversationId}
            placeholder="How can I help you today?"
            variant="empty"
            onOpenLibrary={onOpenLibrary}
          />
        </div>
        
        <p className="text-[13px] text-[#71717a] max-w-md">
          Not sure where to start? Click the <span className="font-bold text-[#09090b]">Playbook</span> icon in the input bar to browse automated workflows, billing actions, and member management tools.
        </p>
      </div>
    </div>
  );
}
