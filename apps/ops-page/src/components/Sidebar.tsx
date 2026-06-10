import { useState, useEffect, useRef } from "react";
import { cn } from "../lib/utils";
import { Plus, MessageSquare, Settings, LogOut, PanelLeftClose, PanelLeftOpen, Building2, MoreVertical } from "lucide-react";
import { motion, AnimatePresence } from "motion/react";
import type { OpsConversationDto, AuthUser, EntitlementDto } from "../lib/api-client";

interface SidebarProps {
  isOpen: boolean;
  setIsOpen: () => void;
  isMobile?: boolean;
  user: AuthUser;
  entitlements: EntitlementDto[];
  activeWorkspaceId: string | null;
  onWorkspaceSelect: (id: string) => void;
  conversations: OpsConversationDto[];
  activeConversationId: string | null;
  onSelect: (id: string | null) => void;
  onNewChat: () => void;
  onRename: (id: string, currentTitle: string) => void;
  onDelete: (id: string) => void;
  onLogout: () => void;
}

export default function Sidebar({
  isOpen, setIsOpen, isMobile, user, entitlements, activeWorkspaceId, onWorkspaceSelect,
  conversations, activeConversationId, onSelect, onNewChat, onRename, onDelete, onLogout
}: SidebarProps) {
  const [isUserMenuOpen, setIsUserMenuOpen] = useState(false);
  const [openMenuId, setOpenMenuId] = useState<string | null>(null);
  const userMenuRef = useRef<HTMLDivElement>(null);
  const expanded = isMobile ? true : isOpen;

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

      <div className="py-2 space-y-1">
        <button onClick={onNewChat} className={cn("group flex h-9 w-full items-center text-left focus:outline-none transition-colors", activeConversationId === "new" ? "bg-[#f4f4f5] text-[#09090b]" : "text-[#71717a] hover:bg-[#fafafa] hover:text-[#09090b]")}>
          <div className="w-12 h-full shrink-0 flex items-center justify-center"><Plus size={16} /></div>
          {expanded && <span className="text-[13px] font-medium truncate">New chat</span>}
        </button>

        <button onClick={() => onSelect("directory")} className={cn("group flex h-9 w-full items-center text-left focus:outline-none transition-colors", activeConversationId === "directory" ? "bg-[#f4f4f5] text-[#09090b] font-semibold" : "text-[#71717a] hover:bg-[#fafafa] hover:text-[#09090b]")}>
          <div className="w-12 h-full shrink-0 flex items-center justify-center"><MessageSquare size={16} /></div>
          {expanded && <span className="text-[13px] truncate">Conversations Directory</span>}
        </button>
      </div>

      {expanded && (
        <div className="flex items-center justify-between px-4 pt-4 pb-1 text-[#71717a] shrink-0 select-none border-t border-[#f4f4f5]">
          <span className="text-[11px] font-bold uppercase tracking-wider">Recent</span>
        </div>
      )}

      <div className="flex-1 overflow-y-auto py-2 space-y-[2px]">
        {expanded && conversations.map((conv) => (
          <div
            key={conv.id}
            onClick={() => onSelect(conv.id)}
            className={cn("group relative flex h-9 w-full items-center justify-between px-4 cursor-pointer transition-colors", conv.id === activeConversationId ? "bg-[#f4f4f5] text-[#09090b] font-medium" : "text-[#71717a] hover:bg-[#fafafa] hover:text-[#09090b]")}
          >
            <span className="text-[13px] truncate block pr-2">{conv.title}</span>
            
            <div className={cn("relative shrink-0", openMenuId === conv.id ? "block" : "hidden group-hover:block")} onClick={(e) => e.stopPropagation()}>
              <button 
                onClick={() => setOpenMenuId(openMenuId === conv.id ? null : conv.id)}
                className="p-1 text-[#a1a1aa] hover:text-[#09090b] transition-colors rounded-sm focus:outline-none"
              >
                <MoreVertical size={14} />
              </button>
              {openMenuId === conv.id && (
                <div className="absolute right-0 top-full mt-1 w-28 bg-white border border-[#e5e5e5] shadow-lg rounded-sm py-1 z-50">
                  <button 
                    onClick={() => { setOpenMenuId(null); onRename(conv.id, conv.title); }}
                    className="w-full text-left px-3 py-1.5 text-xs text-[#09090b] hover:bg-[#f4f4f5] transition-colors"
                  >
                    Rename
                  </button>
                  <button 
                    onClick={() => { setOpenMenuId(null); onDelete(conv.id); }}
                    className="w-full text-left px-3 py-1.5 text-xs text-rose-600 hover:bg-rose-50 transition-colors"
                  >
                    Delete
                  </button>
                </div>
              )}
            </div>
          </div>
        ))}
        {expanded && conversations.length === 20 && (
          <button onClick={() => onSelect("directory")} className="w-full py-2 text-[11px] font-bold uppercase tracking-widest text-[#a1a1aa] hover:text-[#09090b] transition-colors">See All</button>
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
                <button onClick={() => { window.location.href = "http://localhost:3001/profile"; }} className="flex w-full items-center gap-2 px-2 py-1.5 text-xs text-[#71717a] hover:bg-[#f4f4f5] hover:text-[#09090b] transition-colors focus:outline-none">
                  <Settings size={14} /> View Identity Hub
                </button>
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
