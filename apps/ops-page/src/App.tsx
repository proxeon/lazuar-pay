import { useState, useEffect } from "react";
import { Routes, Route, Navigate } from "react-router-dom";
import Sidebar from "./components/Sidebar";
import OpsChatWorkspace from "./components/OpsChatWorkspace";
import type { ProposedActionDto } from "./lib/api-client";

interface Conversation {
  id: string;
  title: string;
}

interface Message {
  id: string;
  role: "user" | "assistant" | "system";
  content: string;
  isStreaming?: boolean;
  toolStatus?: string;
  proposedAction?: ProposedActionDto;
}

export default function App() {
  const [isSidebarOpen, setIsSidebarOpen] = useState(() => {
    try {
      const saved = localStorage.getItem("lazuar-ops-sidebar-collapsed");
      return saved !== "true";
    } catch {
      return true;
    }
  });

  const [isMobile, setIsMobile] = useState(false);
  const [activeConversationId, setActiveConversationId] = useState<string | null>("1");

  const [conversations, setConversations] = useState<Conversation[]>([
    { id: "1", title: "Kubernetes Pod Memory Leak" },
    { id: "2", title: "PostgreSQL DB Migration Failure" },
    { id: "3", title: "AWS S3 Rate Limit Warnings" },
    { id: "4", title: "Nginx Reverse Proxy Redirect Issues" }
  ]);

  const [messagesMap, setMessagesMap] = useState<Record<string, Message[]>>({
    "1": [
      { id: "init-1", role: "assistant", content: "Active metrics indicate pod memory leak. How would you like me to analyze the trace nodes?" }
    ],
    "2": [
      { id: "init-2", role: "assistant", content: "Ready to inspect migration delta scripts. Point me to the database manifest." }
    ],
    "3": [
      { id: "init-3", role: "assistant", content: "Bucket metadata limits hit. S3 actions are queued. Shall I inspect the queue details?" }
    ],
    "4": [
      { id: "init-4", role: "assistant", content: "Redirect rules loaded. Nginx server blocks verified. Request instructions." }
    ]
  });

  useEffect(() => {
    const checkMobile = () => {
      const mobileStatus = window.innerWidth < 768;
      setIsMobile(mobileStatus);
      if (mobileStatus) {
        setIsSidebarOpen(false);
      } else {
        const saved = localStorage.getItem("lazuar-ops-sidebar-collapsed");
        setIsSidebarOpen(saved !== "true");
      }
    };
    
    checkMobile();
    window.addEventListener("resize", checkMobile);
    return () => window.removeEventListener("resize", checkMobile);
  }, []);

  const handleToggleSidebar = () => {
    setIsSidebarOpen((prev) => {
      const nextState = !prev;
      try {
        localStorage.setItem("lazuar-ops-sidebar-collapsed", String(!nextState));
      } catch (err) {
        console.error("Failed to save sidebar state", err);
      }
      return nextState;
    });
  };

  const handleNewChat = () => {
    const newId = Date.now().toString();
    const newChat: Conversation = {
      id: newId,
      title: `Operations Query ${conversations.length + 1}`
    };
    setConversations((prev) => {
      const updated = [newChat, ...prev];
      return updated.slice(0, 20);
    });
    setMessagesMap((prev) => ({
      ...prev,
      [newId]: [
        { id: `init-${newId}`, role: "assistant", content: "How can I help you manage your ecosystem today?" }
      ]
    }));
    setActiveConversationId(newId);
  };

  const handleDeleteChat = (id: string, e: React.MouseEvent) => {
    e.stopPropagation();
    setConversations((prev) => {
      const updated = prev.filter((c) => c.id !== id);
      if (activeConversationId === id) {
        setActiveConversationId(updated.length > 0 ? updated[0].id : null);
      }
      return updated;
    });
    setMessagesMap((prev) => {
      const updated = { ...prev };
      delete updated[id];
      return updated;
    });
  };

  const handleRenameChat = (id: string, newTitle: string) => {
    setConversations((prev) =>
      prev.map((c) => (c.id === id ? { ...c, title: newTitle } : c))
    );
  };

  const activeMessages = activeConversationId ? messagesMap[activeConversationId] || [] : [];
  const setActiveMessages = (updater: (prev: Message[]) => Message[]) => {
    if (!activeConversationId) return;
    setMessagesMap((prev) => ({
      ...prev,
      [activeConversationId]: updater(prev[activeConversationId] || [])
    }));
  };

  return (
    <div className="flex h-screen w-full overflow-hidden bg-[#f5f5f5] font-sans text-[#1a1a1a]">
      <Sidebar
        isOpen={isSidebarOpen}
        setIsOpen={handleToggleSidebar}
        isMobile={isMobile}
        conversations={conversations}
        activeConversationId={activeConversationId}
        onSelect={setActiveConversationId}
        onNewChat={handleNewChat}
        onDelete={handleDeleteChat}
        onRename={handleRenameChat}
      />
      
      <main className="flex-1 flex flex-col overflow-hidden w-full relative bg-white">
        <Routes>
          <Route path="/" element={<Navigate to="/chat" replace />} />
          <Route 
            path="/chat" 
            element={
              <OpsChatWorkspace
                activeConversationId={activeConversationId}
                messages={activeMessages}
                setMessages={setActiveMessages}
              />
            } 
          />
        </Routes>
        
        {isMobile && isSidebarOpen && (
          <div 
            className="fixed inset-0 bg-black/10 z-20 backdrop-blur-sm" 
            onClick={handleToggleSidebar}
          />
        )}
      </main>
    </div>
  );
}
