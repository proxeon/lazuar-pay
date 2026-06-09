import { useState, useEffect, useRef } from "react";
import { cn } from "../lib/utils";
import { 
  Plus, 
  Trash2, 
  Pencil, 
  MessageSquare, 
  Settings, 
  LogOut, 
  PanelLeftClose, 
  PanelLeftOpen,
  SlidersHorizontal
} from "lucide-react";
import { motion, AnimatePresence } from "motion/react";

interface Conversation {
  id: string;
  title: string;
}

interface SidebarProps {
  isOpen: boolean;
  setIsOpen: () => void;
  isMobile?: boolean;
  conversations: Conversation[];
  activeConversationId: string | null;
  onSelect: (id: string | null) => void;
  onNewChat: () => void;
  onDelete: (id: string, e: React.MouseEvent) => void;
  onRename: (id: string, title: string) => void;
  onLogout: () => void;
}

export default function Sidebar({
  isOpen,
  setIsOpen,
  isMobile,
  conversations,
  activeConversationId,
  onSelect,
  onNewChat,
  onDelete,
  onRename,
  onLogout
}: SidebarProps) {
  const [isUserMenuOpen, setIsUserMenuOpen] = useState(false);
  const [renamingId, setRenamingId] = useState<string | null>(null);
  const [renameValue, setRenameValue] = useState("");
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

  const handleStartRename = (id: string, title: string, e: React.MouseEvent) => {
    e.stopPropagation();
    setRenamingId(id);
    setRenameValue(title);
  };

  const handleCommitRename = (id: string) => {
    if (renameValue.trim()) {
      onRename(id, renameValue.trim());
    }
    setRenamingId(null);
  };

  return (
    <motion.aside
      initial={false}
      animate={{ 
        width: isMobile ? 240 : (isOpen ? 240 : 64),
        x: isMobile ? (isOpen ? 0 : -240) : 0,
      }}
      transition={{ duration: 0.3, ease: [0.2, 0, 0, 1] }}
      className={cn(
        "z-30 flex h-full shrink-0 flex-col border-r border-[#e5e5e5] bg-white absolute md:relative",
        isMobile ? "shadow-2xl" : ""
      )}
    >
      {/* Header Panel with Branding and Collapse Toggle */}
      <div className={cn(
        "flex h-14 w-full shrink-0 items-center overflow-hidden border-b border-[#e5e5e5] px-4",
        expanded ? "justify-between" : "justify-center"
      )}>
        {expanded && (
          <div className="flex items-center gap-2.5 min-w-0">
            <div className="flex h-5 w-5 shrink-0 items-center justify-center bg-[#09090b]">
              <div className="h-1 w-1 bg-white" />
            </div>
            <span className="whitespace-nowrap text-sm font-semibold tracking-tight text-[#09090b] truncate">
              Lazuar Ops
            </span>
          </div>
        )}
        
        {!isMobile && (
          <button
            onClick={setIsOpen}
            className="text-[#71717a] hover:text-[#09090b] transition-colors focus:outline-none"
            title={expanded ? "Collapse sidebar" : "Expand sidebar"}
          >
            {expanded ? <PanelLeftClose size={16} /> : <PanelLeftOpen size={16} />}
          </button>
        )}
      </div>

      {/* Primary Action Button List */}
      <div className="p-2 space-y-1 border-b border-[#e5e5e5]">
        {/* New Chat Button */}
        <button
          onClick={onNewChat}
          className={cn(
            "flex h-9 w-full items-center gap-3 transition-colors text-left focus:outline-none",
            expanded 
              ? "px-3 border border-[#e5e5e5] bg-white text-[#09090b] hover:bg-[#fafafa]" 
              : "justify-center text-[#71717a] hover:text-[#09090b]"
          )}
          title={!expanded ? "New chat" : undefined}
        >
          <Plus size={16} className="shrink-0" />
          {expanded && <span className="text-[13px] font-medium">New chat</span>}
        </button>

        {/* Global Chats Directory Switcher */}
        <button
          onClick={() => onSelect("directory")}
          className={cn(
            "flex h-9 w-full items-center gap-3 transition-colors text-left focus:outline-none",
            activeConversationId === "directory"
              ? "bg-[#f4f4f5] text-[#09090b] font-semibold"
              : "text-[#71717a] hover:bg-[#fafafa] hover:text-[#09090b]",
            expanded ? "px-3" : "justify-center"
          )}
          title={!expanded ? "Chats" : undefined}
        >
          <MessageSquare size={16} className="shrink-0" />
          {expanded && <span className="text-[13px]">Chats</span>}
        </button>
      </div>

      {/* Recents Subsection Header */}
      {expanded && (
        <div className="flex items-center justify-between px-4 pt-4 pb-1 text-[#71717a] shrink-0">
          <span className="text-[11px] font-bold uppercase tracking-wider">Recents</span>
          <SlidersHorizontal size={11} className="cursor-pointer hover:text-[#09090b] transition-colors" />
        </div>
      )}

      {/* Recent Conversation Items (Max 20) */}
      <div className="flex-1 overflow-y-auto p-2 space-y-[2px]">
        {expanded && conversations.map((conv) => (
          <div
            key={conv.id}
            onClick={() => renamingId !== conv.id && onSelect(conv.id)}
            className={cn(
              "group flex h-9 w-full items-center justify-between px-3 cursor-pointer transition-colors relative",
              conv.id === activeConversationId
                ? "bg-[#f4f4f5] text-[#09090b] font-medium"
                : "text-[#71717a] hover:bg-[#fafafa] hover:text-[#09090b]"
            )}
          >
            <div className="flex items-center min-w-0 flex-1">
              <div className="flex-1 min-w-0 pr-2">
                {renamingId === conv.id ? (
                  <input
                    type="text"
                    value={renameValue}
                    onChange={(e) => setRenameValue(e.target.value)}
                    onBlur={() => handleCommitRename(conv.id)}
                    onKeyDown={(e) => {
                      if (e.key === "Enter") handleCommitRename(conv.id);
                      if (e.key === "Escape") setRenamingId(null);
                    }}
                    className="w-full bg-white border border-[#e5e5e5] px-1.5 py-0.5 text-xs text-[#09090b] outline-none"
                    autoFocus
                    onClick={(e) => e.stopPropagation()}
                  />
                ) : (
                  <span className="text-[13px] truncate block">{conv.title}</span>
                )}
              </div>
            </div>

            {renamingId !== conv.id && (
              <div className="opacity-0 group-hover:opacity-100 flex items-center gap-1 shrink-0 bg-transparent">
                <button
                  onClick={(e) => handleStartRename(conv.id, conv.title, e)}
                  className="p-1 text-[#71717a] hover:text-[#09090b] transition-colors"
                  title="Rename"
                >
                  <Pencil size={11} />
                </button>
                <button
                  onClick={(e) => onDelete(conv.id, e)}
                  className="p-1 text-[#71717a] hover:text-rose-600 transition-colors"
                  title="Delete"
                >
                  <Trash2 size={11} />
                </button>
              </div>
            )}
          </div>
        ))}
      </div>

      {/* Bottom Profile Section */}
      <div className="shrink-0 border-t border-[#e5e5e5] p-3 relative">
        <div className="relative" ref={userMenuRef}>
          <button 
            onClick={() => setIsUserMenuOpen(!isUserMenuOpen)}
            className="group flex h-10 w-full items-center rounded-none transition-colors hover:bg-[#fafafa] overflow-hidden text-left focus:outline-none"
            title={!expanded ? "User profile" : undefined}
          >
            <div className="flex w-[40px] shrink-0 items-center justify-center">
              <div className="flex h-7 w-7 shrink-0 items-center justify-center rounded-none bg-[#f4f4f5] border border-[#e5e5e5] text-[10px] font-semibold text-[#52525b]">
                AU
              </div>
            </div>
            <motion.div
              initial={false}
              animate={{ opacity: expanded ? 1 : 0 }}
              className="flex flex-col gap-[2px] min-w-0 overflow-hidden"
            >
              <span className="whitespace-nowrap text-[13px] font-medium leading-none text-[#09090b] truncate">Admin User</span>
              <span className="whitespace-nowrap text-[11px] font-medium leading-none text-[#71717a] truncate">admin@lazuar.io</span>
            </motion.div>
          </button>
          
          <AnimatePresence>
            {isUserMenuOpen && (
              <motion.div
                initial={{ opacity: 0, y: 5 }}
                animate={{ opacity: 1, y: 0 }}
                exit={{ opacity: 0, y: 5 }}
                transition={{ duration: 0.15 }}
                className={cn(
                  "absolute z-50 rounded-none border border-[#e5e5e5] bg-white p-1 shadow-brutal",
                  expanded ? "bottom-[calc(100%+8px)] left-0 w-full min-w-[200px]" : "bottom-0 left-[calc(100%+8px)] min-w-[200px]"
                )}
              >
                <div className="px-2 py-1.5 border-b border-[#f4f4f5] mb-1">
                  <span className="block text-[13px] font-medium text-[#09090b]">Admin User</span>
                  <span className="block text-[11px] text-[#71717a]">admin@lazuar.io</span>
                </div>
                <button className="flex w-full items-center gap-2 px-2 py-1.5 text-[13px] text-[#71717a] hover:bg-[#f4f4f5] hover:text-[#09090b] transition-colors focus:outline-none">
                  <Settings size={14} />
                  Settings
                </button>
                <button 
                  onClick={onLogout}
                  className="flex w-full items-center gap-2 px-2 py-1.5 text-[13px] text-red-600 hover:bg-rose-50 hover:text-red-700 transition-colors focus:outline-none"
                >
                  <LogOut size={14} />
                  Log out
                </button>
              </motion.div>
            )}
          </AnimatePresence>
        </div>
      </div>
    </motion.aside>
  );
}
