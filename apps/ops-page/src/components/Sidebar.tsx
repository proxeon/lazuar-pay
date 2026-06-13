import { useState, useEffect, useRef } from "react";
import { Link, useLocation, useNavigate } from "react-router-dom";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { cn } from "../lib/utils";
import { Plus, MessageSquare, Settings, LogOut, PanelLeftClose, PanelLeftOpen, Building2, MoreVertical, CreditCard, Activity } from "lucide-react";
import { motion, AnimatePresence } from "motion/react";
import { client, type AuthUser, type EntitlementDto } from "../lib/api-client";
import { toast } from "sonner";

interface SidebarProps {
  isOpen: boolean;
  setIsOpen: () => void;
  isMobile?: boolean;
  user: AuthUser;
  entitlements: EntitlementDto[];
  activeWorkspaceId: string | null;
  onWorkspaceSelect: (id: string) => void;
  onLogout: () => void;
}

export default function Sidebar({
  isOpen, setIsOpen, isMobile, user, entitlements, activeWorkspaceId, onWorkspaceSelect, onLogout
}: SidebarProps) {
  const [isUserMenuOpen, setIsUserMenuOpen] = useState(false);
  const [openMenuId, setOpenMenuId] = useState<string | null>(null);
  const userMenuRef = useRef<HTMLDivElement>(null);
  const expanded = isMobile ? true : isOpen;
  const location = useLocation();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (userMenuRef.current && !userMenuRef.current.contains(event.target as Node)) {
        setIsUserMenuOpen(false);
      }
    }
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  useEffect(() => {
    const closeMenu = () => setOpenMenuId(null);
    document.addEventListener("click", closeMenu);
    return () => document.removeEventListener("click", closeMenu);
  }, []);

  const { data: conversations } = useQuery({
    queryKey: ["conversations", activeWorkspaceId],
    queryFn: async () => {
      const { data, error } = await client.GET("/ops/chat/conversations", { params: { query: { limit: 20, offset: 0 } } });
      if (error) throw new Error(error.detail);
      return data.data;
    },
    enabled: !!activeWorkspaceId
  });

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
      
      if (location.pathname === `/chat/${id}`) {
        navigate("/chat");
      }
    } catch (err: any) {
      toast.error("Failed to delete conversation", { description: err.message });
    }
  };

  return (
    <motion.aside
      initial={false}
      animate={{ width: isMobile ? 240 : (isOpen ? 240 : 48), x: isMobile ? (isOpen ? 0 : -240) : 0 }}
      transition={{ duration: 0.3, ease: [0.2, 0, 0, 1] }}
      className={cn("z-30 flex h-full shrink-0 flex-col border-r border-[#e5e5e5] bg-white absolute md:relative", isMobile ? "shadow-2xl" : "")}
    >
      <div className="flex h-14 w-full shrink-0 items-center overflow-hidden relative border-b border-[#e5e5e5]">
        {expanded ? (
          <div className="w-full flex items-center justify-between px-4">
            <span className="text-[14px] font-bold tracking-tight text-[#09090b] select-none font-sans">Lazuar Ops</span>
            {!isMobile && (
              <button onClick={setIsOpen} className="text-[#71717a] hover:text-[#09090b] transition-colors focus:outline-none">
                <PanelLeftClose size={16} />
              </button>
            )}
          </div>
        ) : (
          <button onClick={setIsOpen} className="w-full h-full flex items-center justify-center text-[#71717a] hover:text-[#09090b] transition-colors focus:outline-none">
            <PanelLeftOpen size={16} />
          </button>
        )}
      </div>

      {expanded && entitlements.length > 1 && (
        <div className="p-3 border-b border-[#e5e5e5] bg-[#fafafa]">
          <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block mb-1.5 px-1">Target Workspace</label>
          <div className="relative">
            <Building2 size={13} className="absolute left-2.5 top-2 text-[#a1a1aa]" />
            <select
              value={activeWorkspaceId || ""}
              onChange={(e) => onWorkspaceSelect(e.target.value)}
              className="w-full h-8 pl-8 pr-3 bg-white border border-[#e5e5e5] text-[12px] font-medium text-[#09090b] focus:outline-none focus:ring-1 focus:ring-[#09090b] appearance-none"
            >
              {entitlements.map(e => <option key={e.workspace_id} value={e.workspace_id}>{e.workspace_name}</option>)}
            </select>
          </div>
        </div>
      )}

      <nav className="py-2 space-y-1">
        <Link 
          to="/chat" 
          onClick={() => isMobile && setIsOpen()} 
          className={cn("group flex h-9 w-full items-center text-left focus:outline-none transition-colors", location.pathname === "/chat" ? "bg-[#f4f4f5] text-[#09090b]" : "text-[#71717a] hover:bg-[#fafafa] hover:text-[#09090b]")}
        >
          <div className="w-12 h-full shrink-0 flex items-center justify-center"><Plus size={16} /></div>
          {expanded && <span className="text-[13px] font-medium truncate">New chat</span>}
        </Link>

        <Link 
          to="/insights" 
          onClick={() => isMobile && setIsOpen()} 
          className={cn("group flex h-9 w-full items-center text-left focus:outline-none transition-colors", location.pathname === "/insights" ? "bg-[#f4f4f5] text-[#09090b] font-semibold" : "text-[#71717a] hover:bg-[#fafafa] hover:text-[#09090b]")}
        >
          <div className="w-12 h-full shrink-0 flex items-center justify-center"><Activity size={16} /></div>
          {expanded && <span className="text-[13px] truncate">Community Insights</span>}
        </Link>

        <Link 
          to="/history" 
          onClick={() => isMobile && setIsOpen()} 
          className={cn("group flex h-9 w-full items-center text-left focus:outline-none transition-colors", location.pathname === "/history" ? "bg-[#f4f4f5] text-[#09090b] font-semibold" : "text-[#71717a] hover:bg-[#fafafa] hover:text-[#09090b]")}
        >
          <div className="w-12 h-full shrink-0 flex items-center justify-center"><MessageSquare size={16} /></div>
          {expanded && <span className="text-[13px] truncate">Conversations Directory</span>}
        </Link>
      </nav>

      {expanded && (
        <div className="flex items-center justify-between px-4 pt-4 pb-1 text-[#71717a] shrink-0 select-none border-t border-[#f4f4f5]">
          <span className="text-[11px] font-bold uppercase tracking-wider">Recent</span>
        </div>
      )}

      <div className="flex-1 overflow-y-auto py-2 space-y-[2px]">
        {expanded && conversations?.map((conv) => (
          <div
            key={conv.id}
            onClick={() => {
              if (isMobile) setIsOpen();
              navigate(`/chat/${conv.id}`);
            }}
            className={cn("group relative flex h-9 w-full items-center justify-between px-4 cursor-pointer transition-colors", location.pathname === `/chat/${conv.id}` ? "bg-[#f4f4f5] text-[#09090b] font-medium" : "text-[#71717a] hover:bg-[#fafafa] hover:text-[#09090b]")}
          >
            <span className="text-[13px] truncate flex-1 pr-2">{conv.title}</span>
            
            <div className={cn("relative shrink-0", openMenuId === conv.id ? "block" : "hidden group-hover:block")} onClick={(e) => e.stopPropagation()}>
              <button 
                onClick={(e) => { e.stopPropagation(); setOpenMenuId(openMenuId === conv.id ? null : conv.id); }}
                className="p-1 text-[#a1a1aa] hover:text-[#09090b] transition-colors rounded-sm focus:outline-none"
              >
                <MoreVertical size={14} />
              </button>
              {openMenuId === conv.id && (
                <div className="absolute right-0 top-full mt-1 w-28 bg-white border border-[#e5e5e5] shadow-lg rounded-sm py-1 z-50">
                  <button 
                    onClick={(e) => { e.stopPropagation(); setOpenMenuId(null); handleRenameConversation(conv.id, conv.title); }}
                    className="w-full text-left px-3 py-1.5 text-xs text-[#09090b] hover:bg-[#f4f4f5] transition-colors"
                  >
                    Rename
                  </button>
                  <button 
                    onClick={(e) => { e.stopPropagation(); setOpenMenuId(null); handleDeleteConversation(conv.id); }}
                    className="w-full text-left px-3 py-1.5 text-xs text-rose-600 hover:bg-rose-50 transition-colors"
                  >
                    Delete
                  </button>
                </div>
              )}
            </div>
          </div>
        ))}
        {expanded && conversations?.length === 20 && (
          <Link to="/history" onClick={() => isMobile && setIsOpen()} className="w-full block py-2 px-4 text-[11px] font-bold uppercase tracking-widest text-[#a1a1aa] hover:text-[#09090b] transition-colors text-center">See All</Link>
        )}
      </div>

      <div className="shrink-0 relative border-t border-[#e5e5e5]">
        <div className="relative" ref={userMenuRef}>
          <button onClick={() => setIsUserMenuOpen(!isUserMenuOpen)} className="group flex h-12 w-full items-center rounded-none transition-colors hover:bg-[#fafafa] overflow-hidden text-left focus:outline-none">
            <div className="w-12 h-full shrink-0 flex items-center justify-center">
              <div className="flex h-7 w-7 shrink-0 items-center justify-center bg-[#09090b] text-white text-[10px] font-semibold">{user.email.substring(0,2).toUpperCase()}</div>
            </div>
            <motion.div initial={false} animate={{ opacity: expanded ? 1 : 0 }} className="flex flex-col gap-[2px] min-w-0 overflow-hidden pr-3">
              <span className="whitespace-nowrap text-[13px] font-medium leading-none text-[#09090b] truncate">{user.name}</span>
              <span className="whitespace-nowrap text-[11px] font-medium leading-none text-[#71717a] truncate">{user.email}</span>
            </motion.div>
          </button>
          
          <AnimatePresence>
            {isUserMenuOpen && (
              <motion.div initial={{ opacity: 0, y: 5 }} animate={{ opacity: 1, y: 0 }} exit={{ opacity: 0, y: 5 }} transition={{ duration: 0.15 }} className={cn("absolute z-50 rounded-none border border-[#e5e5e5] bg-white p-1", expanded ? "bottom-[calc(100%+8px)] left-2 w-[calc(100%-16px)] min-w-[200px]" : "bottom-1 left-[calc(100%+8px)] min-w-[200px]")}>
                
                <Link 
                  to="/settings/payment"
                  onClick={() => setIsUserMenuOpen(false)} 
                  className="flex w-full items-center gap-2 px-2 py-1.5 text-xs text-[#09090b] hover:bg-[#f4f4f5] transition-colors focus:outline-none"
                >
                  <CreditCard size={14} className="text-[#71717a]" /> Payment Configuration
                </Link>

                <a 
                  href="http://localhost:3001/profile" 
                  target="_blank" 
                  rel="noopener noreferrer" 
                  onClick={() => setIsUserMenuOpen(false)} 
                  className="flex w-full items-center gap-2 px-2 py-1.5 text-xs text-[#71717a] hover:bg-[#f4f4f5] hover:text-[#09090b] transition-colors focus:outline-none"
                >
                  <Settings size={14} /> View Identity Hub
                </a>
                <div className="h-px w-full bg-[#f4f4f5] my-1" />
                <button onClick={onLogout} className="flex w-full items-center gap-2 px-2 py-1.5 text-xs text-red-600 hover:bg-rose-50 hover:text-red-700 transition-colors focus:outline-none">
                  <LogOut size={14} /> Log out
                </button>
              </motion.div>
            )}
          </AnimatePresence>
        </div>
      </div>
    </motion.aside>
  );
}
