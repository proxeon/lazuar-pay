import { useState, useEffect } from "react";
import { Sparkles, Activity, Terminal, Database, Globe } from "lucide-react";
import ChatInputArea from "./ChatInputArea";

interface ChatEmptyStateProps {
  onSend: (text: string) => void;
  isProcessing: boolean;
  activeConversationId: string | null;
}

export default function ChatEmptyState({ onSend, isProcessing, activeConversationId }: ChatEmptyStateProps) {
  const [greeting, setGreeting] = useState("Hello");

  useEffect(() => {
    const hours = new Date().getHours();
    if (hours >= 22 || hours < 5) setGreeting("Hello, night owl");
    else if (hours >= 5 && hours < 12) setGreeting("Good morning");
    else if (hours >= 12 && hours < 17) setGreeting("Good afternoon");
    else setGreeting("Good evening");
  }, []);

  const quickActions = [
    { label: "Verify Metrics", icon: Activity, query: "Check the active container performance metrics and system load." },
    { label: "Analyze Logs", icon: Terminal, query: "Scan the latest reverse proxy server block redirects for errors." },
    { label: "DB Health", icon: Database, query: "Verify database transaction lag, locked queries, and cluster health." },
    { label: "DNS Status", icon: Globe, query: "Perform a routing health check on external proxy and edge nodes." }
  ];

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
          />
        </div>

        <div className="flex flex-wrap items-center justify-center gap-2">
          {quickActions.map((action) => (
            <button
              key={action.label}
              onClick={() => onSend(action.query)}
              className="flex items-center gap-2 border border-[#e5e5e5] bg-white px-3 py-1.5 hover:bg-[#fafafa] hover:border-[#cbcbcb] text-[13px] text-[#52525b] transition-all font-medium rounded-lg"
            >
              <action.icon size={13} className="text-[#71717a]" />
              <span>{action.label}</span>
            </button>
          ))}
        </div>

      </div>
    </div>
  );
}
