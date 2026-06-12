import { useState, useEffect } from "react";
import { Routes, Route, Navigate } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import Sidebar from "./components/Sidebar";
import OpsChatWorkspace from "./components/OpsChatWorkspace";
import LoginPage from "./components/LoginPage";
import PaymentSettingsModal from "./components/PaymentSettingsModal";
import CommunityInsights from "./components/CommunityInsights";
import { MessageSquare, ArrowRight, MoreVertical } from "lucide-react";
import { toast } from "sonner";
import { client, type AuthUser, type EntitlementDto } from "./lib/api-client";
import type { Message } from "./types/chat";

export default function App() {
  const queryClient = useQueryClient();
  const [user, setUser] = useState<AuthUser | null>(null);
  const [activeWorkspaceId, setActiveWorkspaceId] = useState<string | null>(() => localStorage.getItem("ops_active_workspace_id"));
  const [isAuthLoading, setIsAuthLoading] = useState(true);
  const [isMobile, setIsMobile] = useState(false);
  const [isSidebarOpen, setIsSidebarOpen] = useState(() => localStorage.getItem("lazuar-ops-sidebar-collapsed") !== "true");
  
  const [activeConversationId, setActiveConversationId] = useState<string | null>("directory");
  const [activeMessages, setActiveMessages] = useState<Message[]>([]);
  const [openMenuId, setOpenMenuId] = useState<string | null>(null);
  
  const [showPaymentSettings, setShowPaymentSettings] = useState(false);

  useEffect(() => {
    const checkMobile = () => {
      const mobileStatus = window.innerWidth < 768;
      setIsMobile(mobileStatus);
      if (mobileStatus) setIsSidebarOpen(false);
    };
    checkMobile();
    window.addEventListener("resize", checkMobile);
    return () => window.removeEventListener("resize", checkMobile);
  }, []);

  useEffect(() => {
    const closeMenu = () => setOpenMenuId(null);
    document.addEventListener("click", closeMenu);
    return () => document.removeEventListener("click", closeMenu);
  }, []);

  useEffect(() => {
    async function verifySession() {
      try {
        const { data, error } = await client.GET("/one/auth/me");
        if (error || !data) {
          if (window.location.pathname !== "/login") {
            window.location.href = `/login?returnUrl=${encodeURIComponent(window.location.pathname)}`;
          }
          return;
        }

        if (data.role === "SUPER_ADMIN") {
          window.location.href = "http://localhost:3000/dashboard";
          return;
        }

        setUser(data);

        if (window.location.pathname === "/login") {
          window.location.href = "/chat";
        }
      } catch {
        if (window.location.pathname !== "/login") {
          window.location.href = "/login";
        }
      } finally {
        setIsAuthLoading(false);
      }
    }
    verifySession();
  }, []);

  const { data: entitlements, isLoading: isEntitlementsLoading } = useQuery({
    queryKey: ["entitlements"],
    queryFn: async () => {
      const { data, error } = await client.GET("/one/me/entitlements");
      if (error) throw new Error(error.detail);
      return data as EntitlementDto[];
    },
    enabled: !!user
  });

  // Automatically sync local storage and state to prevent stale access drops
  useEffect(() => {
    if (entitlements) {
      if (entitlements.length > 0) {
        const isValid = entitlements.some(e => e.workspace_id === activeWorkspaceId);
        if (!isValid) {
          setActiveWorkspaceId(entitlements[0].workspace_id);
          localStorage.setItem("ops_active_workspace_id", entitlements[0].workspace_id);
        }
      } else {
        setActiveWorkspaceId(null);
        localStorage.removeItem("ops_active_workspace_id");
      }
    }
  }, [entitlements, activeWorkspaceId]);

  const { data: conversationData, refetch: refetchConversations } = useQuery({
    queryKey: ["conversations", activeWorkspaceId],
    queryFn: async () => {
      const { data, error } = await client.GET("/ops/chat/conversations", { params: { query: { limit: 20, offset: 0 } } });
      if (error) throw new Error(error.detail);
      return data.data;
    },
    enabled: !!activeWorkspaceId
  });

  useEffect(() => {
    async function loadMessages() {
      if (!activeConversationId || activeConversationId === "directory" || activeConversationId === "new" || activeConversationId === "insights") {
        setActiveMessages([]);
        return;
      }

      const { data, error } = await client.GET("/ops/chat/conversations/{id}/messages", {
        params: { path: { id: activeConversationId } }
      });

      if (!error && data) {
        setActiveMessages(data.map(m => ({
          id: m.id,
          role: m.role as "user" | "assistant" | "system",
          content: m.content,
          toolStatus: m.tool_status,
          proposedAction: m.proposed_action
        })));
      }
    }
    loadMessages();
  }, [activeConversationId]);

  const handleToggleSidebar = () => {
    setIsSidebarOpen((prev) => {
      localStorage.setItem("lazuar-ops-sidebar-collapsed", String(prev));
      return !prev;
    });
  };

  const handleWorkspaceChange = (id: string) => {
    setActiveWorkspaceId(id);
    localStorage.setItem("ops_active_workspace_id", id);
    setActiveConversationId("directory");
  };

  const handleLogout = async () => {
    await client.POST("/one/auth/logout");
    localStorage.removeItem("ops_active_workspace_id");
    window.location.href = "/login";
  };

  const handleRenameConversation = async (id: string, currentTitle: string) => {
    const newTitle = window.prompt("Enter new title:", currentTitle);
    if (!newTitle || newTitle.trim() === "" || newTitle === currentTitle) return;
    
    try {
      const { error } = await client.PUT("/ops/chat/conversations/{id}/title", {
        params: { path: { id } },
        body: { title: newTitle.trim() }
      });
      if (error) throw new Error(error.detail);
      
      toast.success("Conversation renamed");
      queryClient.invalidateQueries({ queryKey: ["conversations", activeWorkspaceId] });
    } catch (err: any) {
      toast.error("Failed to rename conversation", { description: err.message });
    }
  };

  const handleDeleteConversation = async (id: string) => {
    if (!window.confirm("Are you sure you want to delete this conversation?")) return;
    
    try {
      const { error } = await client.DELETE("/ops/chat/conversations/{id}", {
        params: { path: { id } }
      });
      if (error) throw new Error(error.detail);
      
      toast.success("Conversation deleted");
      queryClient.invalidateQueries({ queryKey: ["conversations", activeWorkspaceId] });
      
      if (activeConversationId === id) {
        setActiveConversationId("directory");
      }
    } catch (err: any) {
      toast.error("Failed to delete conversation", { description: err.message });
    }
  };

  if (isAuthLoading || (user && isEntitlementsLoading)) {
    return <div className="flex h-screen w-full items-center justify-center bg-[#f5f5f5] text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Loading Environment...</div>;
  }

  if (user && entitlements?.length === 0) {
    return (
      <div className="flex h-screen w-full flex-col items-center justify-center bg-[#f5f5f5] gap-4">
        <span className="text-[11px] font-bold uppercase tracking-widest text-rose-600">
          Access Denied: No active workspace entitlements found.
        </span>
        <p className="text-[12px] text-[#71717a] max-w-sm text-center">
          Your application is currently pending review by a system administrator. Check back later.
        </p>
        <button 
          onClick={handleLogout} 
          className="h-9 px-6 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest rounded-none hover:bg-[#27272a] transition-colors"
        >
          Log Out
        </button>
      </div>
    );
  }

  const activeConversationTitle = activeConversationId === "new" 
    ? "New Chat" 
    : conversationData?.find(c => c.id === activeConversationId)?.title || "Active Query Control";

  return (
    <>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        {user && (
          <Route
            path="/chat"
            element={
              <div className="flex h-screen w-full overflow-hidden bg-[#f5f5f5] font-sans text-[#1a1a1a]">
                <Sidebar
                  isOpen={isSidebarOpen}
                  setIsOpen={handleToggleSidebar}
                  isMobile={isMobile}
                  user={user}
                  entitlements={entitlements || []}
                  activeWorkspaceId={activeWorkspaceId}
                  onWorkspaceSelect={handleWorkspaceChange}
                  conversations={conversationData || []}
                  activeConversationId={activeConversationId}
                  onSelect={setActiveConversationId}
                  onNewChat={() => setActiveConversationId("new")}
                  onRename={handleRenameConversation}
                  onDelete={handleDeleteConversation}
                  onLogout={handleLogout}
                  onOpenPaymentSettings={() => setShowPaymentSettings(true)}
                />
                
                <main className="flex-1 flex flex-col overflow-hidden w-full relative bg-white">
                  
                  {activeConversationId === "insights" ? (
                    <CommunityInsights />
                  ) : activeConversationId === "directory" ? (
                    <div className="flex-1 flex flex-col h-full overflow-y-auto bg-[#fafafa] p-6 md:p-12">
                      <div className="max-w-4xl mx-auto w-full">
                        <div className="mb-8">
                          <h1 className="text-xl font-bold text-[#09090b]">Active Operational Threads</h1>
                          <p className="text-xs text-[#71717a] mt-1">Review historical troubleshooting sessions</p>
                        </div>

                        {!conversationData || conversationData.length === 0 ? (
                          <div className="border border-dashed border-[#e5e5e5] p-12 text-center bg-white">
                            <p className="text-sm text-[#71717a]">No active operations threads found.</p>
                          </div>
                        ) : (
                          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                            {conversationData.map((conv) => (
                              <div 
                                key={conv.id} 
                                onClick={() => setActiveConversationId(conv.id)}
                                className="bg-white border border-[#e5e5e5] p-5 hover:bg-[#fafafa] transition-all cursor-pointer flex flex-col justify-between h-32 relative group"
                              >
                                <div className="flex items-start justify-between min-w-0">
                                  <div className="flex items-start gap-3 min-w-0">
                                    <div className="h-8 w-8 shrink-0 bg-[#09090b] text-white flex items-center justify-center">
                                      <MessageSquare size={14} />
                                    </div>
                                    <div className="min-w-0">
                                      <h3 className="text-[14px] font-bold text-[#09090b] truncate pr-8">{conv.title}</h3>
                                      <p className="text-[11px] text-[#71717a] mt-1">
                                        {new Date(conv.updated_at).toLocaleString()}
                                      </p>
                                    </div>
                                  </div>
                                  <div className="relative shrink-0 ml-2" onClick={(e) => e.stopPropagation()}>
                                    <button 
                                      onClick={() => setOpenMenuId(openMenuId === conv.id ? null : conv.id)}
                                      className="p-1 text-[#a1a1aa] hover:text-[#09090b] transition-colors rounded-sm focus:outline-none"
                                    >
                                      <MoreVertical size={16} />
                                    </button>
                                    {openMenuId === conv.id && (
                                      <div className="absolute right-0 top-full mt-1 w-32 bg-white border border-[#e5e5e5] shadow-lg rounded-sm py-1 z-50">
                                        <button 
                                          onClick={() => { setOpenMenuId(null); handleRenameConversation(conv.id, conv.title); }}
                                          className="w-full text-left px-3 py-1.5 text-xs text-[#09090b] hover:bg-[#f4f4f5] transition-colors"
                                        >
                                          Rename
                                        </button>
                                        <button 
                                          onClick={() => { setOpenMenuId(null); handleDeleteConversation(conv.id); }}
                                          className="w-full text-left px-3 py-1.5 text-xs text-rose-600 hover:bg-rose-50 transition-colors"
                                        >
                                          Delete
                                        </button>
                                      </div>
                                    )}
                                  </div>
                                </div>
                                <div className="flex items-center justify-between mt-4 pt-3 border-t border-[#f4f4f5]">
                                  <span className="text-[10px] font-bold uppercase tracking-wider text-[#71717a]">ID: {conv.id.substring(0,8)}</span>
                                  <span className="text-[11px] font-bold text-[#09090b] flex items-center gap-1">Open <ArrowRight size={12} /></span>
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
                      setActiveConversationId={setActiveConversationId}
                      activeConversationTitle={activeConversationTitle}
                      messages={activeMessages}
                      setMessages={setActiveMessages}
                      onStreamComplete={() => refetchConversations()}
                    />
                  )}
                  
                  {isMobile && isSidebarOpen && (
                    <div className="fixed inset-0 bg-black/10 z-20 backdrop-blur-sm" onClick={handleToggleSidebar} />
                  )}
                </main>
              </div>
            }
          />
        )}
        <Route path="*" element={<Navigate to="/chat" replace />} />
      </Routes>

      {showPaymentSettings && (
        <PaymentSettingsModal onClose={() => setShowPaymentSettings(false)} />
      )}
    </>
  );
}
