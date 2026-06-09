import { useState, useEffect } from "react";
import { Routes, Route, Navigate } from "react-router-dom";
import Sidebar from "./components/Sidebar";
import OpsChatWorkspace from "./components/OpsChatWorkspace";
import LoginPage from "./components/LoginPage";
import { MessageSquare, Trash2, ArrowRight } from "lucide-react";
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
  const [isAuthenticated, setIsAuthenticated] = useState(() => {
    try {
      return localStorage.getItem("lazuar-ops-auth") === "true";
    } catch {
      return false;
    }
  });

  const [isSidebarOpen, setIsSidebarOpen] = useState(() => {
    try {
      const saved = localStorage.getItem("lazuar-ops-sidebar-collapsed");
      return saved !== "true";
    } catch {
      return true;
    }
  });

  const [isMobile, setIsMobile] = useState(false);
  const [activeConversationId, setActiveConversationId] = useState<string | null>("directory");

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
    if (e) e.stopPropagation();
    setConversations((prev) => {
      const updated = prev.filter((c) => c.id !== id);
      if (activeConversationId === id) {
        setActiveConversationId("directory");
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

  const handleLoginSuccess = () => {
    setIsAuthenticated(true);
    try {
      localStorage.setItem("lazuar-ops-auth", "true");
    } catch (err) {
      console.error("Failed to persist session", err);
    }
  };

  const handleLogout = () => {
    setIsAuthenticated(false);
    try {
      localStorage.removeItem("lazuar-ops-auth");
    } catch (err) {
      console.error("Failed to flush session", err);
    }
  };

  const activeMessages = activeConversationId && activeConversationId !== "directory" 
    ? messagesMap[activeConversationId] || [] 
    : [];

  const setActiveMessages = (updater: (prev: Message[]) => Message[]) => {
    if (!activeConversationId || activeConversationId === "directory") return;
    setMessagesMap((prev) => ({
      ...prev,
      [activeConversationId]: updater(prev[activeConversationId] || [])
    }));
  };

  return (
    <Routes>
      <Route 
        path="/login" 
        element={
          isAuthenticated ? (
            <Navigate to="/chat" replace />
          ) : (
            <LoginPage onLoginSuccess={handleLoginSuccess} />
          )
        } 
      />

      <Route
        path="/chat"
        element={
          isAuthenticated ? (
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
                onLogout={handleLogout}
              />
              
              <main className="flex-1 flex flex-col overflow-hidden w-full relative bg-white">
                {activeConversationId === "directory" ? (
                  /* Conversations Grid Directory Dashboard view */
                  <div className="flex-1 flex flex-col h-full overflow-y-auto bg-[#fafafa] p-6 md:p-12">
                    <div className="max-w-4xl mx-auto w-full">
                      <div className="mb-8">
                        <h1 className="text-xl font-bold text-[#09090b]">Active Operational Threads</h1>
                        <p className="text-xs text-[#71717a] mt-1">Review or jump into active troubleshooting sessions</p>
                      </div>

                      {conversations.length === 0 ? (
                        <div className="border border-dashed border-[#e5e5e5] p-12 text-center bg-white">
                          <p className="text-sm text-[#71717a]">No active operations threads found.</p>
                        </div>
                      ) : (
                        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                          {conversations.map((conv) => (
                            <div 
                              key={conv.id} 
                              onClick={() => setActiveConversationId(conv.id)}
                              className="bg-white border border-[#e5e5e5] p-5 shadow-sm hover:shadow-brutal transition-all cursor-pointer flex flex-col justify-between h-36 relative group"
                            >
                              <div className="flex items-start gap-3 min-w-0">
                                <div className="h-8 w-8 shrink-0 bg-[#09090b] text-white flex items-center justify-center">
                                  <MessageSquare size={14} />
                                </div>
                                <div className="min-w-0">
                                  <h3 className="text-[14px] font-bold text-[#09090b] truncate pr-8">{conv.title}</h3>
                                  <p className="text-[12px] text-[#71717a] mt-1 truncate">
                                    {messagesMap[conv.id]?.[messagesMap[conv.id].length - 1]?.content || "Empty chat thread"}
                                  </p>
                                </div>
                              </div>

                              <div className="flex items-center justify-between mt-4 pt-3 border-t border-[#f4f4f5]">
                                <span className="text-[10px] font-bold uppercase tracking-wider text-[#71717a]">ID: {conv.id}</span>
                                <div className="flex items-center gap-3">
                                  <button 
                                    onClick={(e) => handleDeleteChat(conv.id, e)}
                                    className="p-1 text-[#71717a] hover:text-rose-600 transition-colors"
                                    title="Delete thread"
                                  >
                                    <Trash2 size={13} />
                                  </button>
                                  <span className="text-[11px] font-bold text-[#09090b] flex items-center gap-1">
                                    Open <ArrowRight size={12} />
                                  </span>
                                </div>
                              </div>
                            </div>
                          ))}
                        </div>
                      )}
                    </div>
                  </div>
                ) : (
                  <OpsChatWorkspace
                    activeConversationId={activeConversationId}
                    messages={activeMessages}
                    setMessages={setActiveMessages}
                  />
                )}
                
                {isMobile && isSidebarOpen && (
                  <div 
                    className="fixed inset-0 bg-black/10 z-20 backdrop-blur-sm" 
                    onClick={handleToggleSidebar}
                  />
                )}
              </main>
            </div>
          ) : (
            <Navigate to="/login?returnUrl=/chat" replace />
          )
        }
      />

      <Route path="*" element={<Navigate to="/chat" replace />} />
    </Routes>
  );
}
