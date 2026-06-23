import { useState, useEffect, useRef } from "react";
import { Link, useLocation } from "react-router-dom";
import { cn } from "../lib/utils";
import { 
  LogOut, 
  PanelLeftClose, 
  PanelLeftOpen, 
  Building2, 
  Settings,
  ChevronDown,
  Users,
  Check,
  Plus
} from "lucide-react";
import { motion, AnimatePresence } from "motion/react";
import type { AuthUser, EntitlementDto } from "../lib/api-client";
import CreateWorkspaceModal from "../modules/workspace/components/CreateWorkspaceModal";

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
  const [isWorkspaceMenuOpen, setIsWorkspaceMenuOpen] = useState(false);
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);

  const userMenuRef = useRef<HTMLDivElement>(null);
  const workspaceMenuRef = useRef<HTMLDivElement>(null);
  const expanded = isMobile ? true : isOpen;
  const location = useLocation();

  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (userMenuRef.current && !userMenuRef.current.contains(event.target as Node)) setIsUserMenuOpen(false);
      if (workspaceMenuRef.current && !workspaceMenuRef.current.contains(event.target as Node)) setIsWorkspaceMenuOpen(false);
    }
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  const ModuleNav = ({ title, basePath, icon: Icon, links }: { title: string, basePath: string, icon: any, links: { label: string, href: string }[] }) => {
    const isActiveModule = location.pathname.startsWith(basePath);
    const [isAccordionOpen, setIsAccordionOpen] = useState(isActiveModule);
    const [isFlyoutOpen, setIsFlyoutOpen] = useState(false);
    const navRef = useRef<HTMLDivElement>(null);

    useEffect(() => {
      if (isActiveModule && expanded) {
        setIsAccordionOpen(true);
      }
    }, [isActiveModule, expanded]);

    useEffect(() => {
      function handleClickOutside(event: MouseEvent) {
        if (navRef.current && !navRef.current.contains(event.target as Node)) {
          setIsFlyoutOpen(false);
        }
      }
      document.addEventListener("mousedown", handleClickOutside);
      return () => document.removeEventListener("mousedown", handleClickOutside);
    }, []);

    const handleToggle = () => {
      if (expanded) {
        setIsAccordionOpen(!isAccordionOpen);
      } else {
        setIsFlyoutOpen(!isFlyoutOpen);
      }
    };

    return (
      <div className="flex flex-col w-full relative mb-1" ref={navRef}>
        <button 
          onClick={handleToggle} 
          className={cn(
            "group flex h-9 w-full items-center text-left focus:outline-none transition-colors", 
            isActiveModule ? "text-[#09090b]" : "text-[#71717a] hover:bg-[#fafafa] hover:text-[#09090b]"
          )}
        >
          <div className="w-12 h-full shrink-0 flex items-center justify-center">
            <Icon size={16} />
          </div>
          
          <motion.div 
            initial={false} 
            animate={{ opacity: expanded ? 1 : 0 }} 
            className="flex flex-1 items-center justify-between min-w-0 overflow-hidden pr-4"
          >
            <span className="text-[11px] font-bold uppercase tracking-widest whitespace-nowrap truncate">{title}</span>
            <ChevronDown size={14} className={cn("transition-transform duration-200 shrink-0", isAccordionOpen && "rotate-180")} />
          </motion.div>
        </button>
        
        <AnimatePresence initial={false}>
          {expanded && isAccordionOpen && (
            <motion.div
              initial={{ height: 0, opacity: 0 }}
              animate={{ height: "auto", opacity: 1 }}
              exit={{ height: 0, opacity: 0 }}
              transition={{ duration: 0.2, ease: "easeInOut" }}
              className="overflow-hidden"
            >
              <div className="flex flex-col py-1 space-y-0.5">
                {links.map((link) => {
                  const isExactActive = location.pathname.startsWith(link.href);
                  return (
                    <Link 
                      key={link.href} 
                      to={link.href} 
                      onClick={() => isMobile && setIsOpen()}
                      className={cn(
                        "flex h-8 w-full items-center pl-[48px] pr-4 text-[13px] transition-colors focus:outline-none", 
                        isExactActive ? "text-[#09090b] font-medium bg-[#f4f4f5]" : "text-[#71717a] hover:text-[#09090b] hover:bg-[#fafafa]"
                      )}
                    >
                      {link.label}
                    </Link>
                  );
                })}
              </div>
            </motion.div>
          )}
        </AnimatePresence>

        <AnimatePresence>
          {!expanded && isFlyoutOpen && (
            <motion.div
              initial={{ opacity: 0, y: 5 }}
              animate={{ opacity: 1, y: 0 }}
              exit={{ opacity: 0, y: 5 }}
              transition={{ duration: 0.15 }}
              className="absolute left-[calc(100%+8px)] top-0 z-[100] min-w-[200px] rounded-none border border-[#e5e5e5] bg-white p-1 shadow-sm"
            >
              <div className="px-2 py-1.5 mb-1 border-b border-[#f4f4f5]">
                <span className="text-[10px] font-bold uppercase tracking-widest text-[#09090b]">{title}</span>
              </div>
              {links.map((link) => {
                const isExactActive = location.pathname.startsWith(link.href);
                return (
                  <Link 
                    key={link.href} 
                    to={link.href}
                    onClick={() => setIsFlyoutOpen(false)}
                    className={cn(
                      "flex items-center px-2 py-1.5 text-xs transition-colors focus:outline-none",
                      isExactActive ? "text-[#09090b] font-medium bg-[#fafafa]" : "text-[#71717a] hover:bg-[#f4f4f5] hover:text-[#09090b]"
                    )}
                  >
                    {link.label}
                  </Link>
                );
              })}
            </motion.div>
          )}
        </AnimatePresence>
      </div>
    );
  };

  const activeWorkspaceName = entitlements.find(e => e.workspace_id === activeWorkspaceId)?.workspace_name || "Select Workspace";

  return (
    <>
      <motion.aside
        initial={false}
        animate={{ width: isMobile ? 240 : (isOpen ? 240 : 48), x: isMobile ? (isOpen ? 0 : -240) : 0 }}
        transition={{ duration: 0.3, ease: [0.2, 0, 0, 1] }}
        className={cn("z-30 flex h-full shrink-0 flex-col border-r border-[#e5e5e5] bg-white absolute md:relative")}
      >
        <div className="flex h-14 w-full shrink-0 items-center overflow-hidden relative border-b border-[#e5e5e5]">
          {expanded ? (
            <div className="w-full flex items-center justify-between px-4">
              <span className="text-[14px] font-bold tracking-tight text-[#09090b] select-none font-sans">Lazuar Console</span>
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

        {expanded && (
          <div className="p-3 border-b border-[#e5e5e5] bg-[#fafafa] relative" ref={workspaceMenuRef}>
            <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block mb-1.5 px-1">Target Workspace</label>
            <button 
              onClick={() => setIsWorkspaceMenuOpen(!isWorkspaceMenuOpen)} 
              className="w-full h-8 px-2.5 bg-white border border-[#e5e5e5] text-[12px] font-medium text-[#09090b] flex items-center justify-between hover:bg-[#fafafa] transition-colors focus:outline-none"
            >
              <div className="flex items-center gap-2 min-w-0">
                <Building2 size={13} className="text-[#a1a1aa] shrink-0" />
                <span className="truncate">{activeWorkspaceName}</span>
              </div>
              <ChevronDown size={14} className="text-[#a1a1aa] shrink-0" />
            </button>

            <AnimatePresence>
              {isWorkspaceMenuOpen && (
                <motion.div 
                  initial={{ opacity: 0, y: 5 }} 
                  animate={{ opacity: 1, y: 0 }} 
                  exit={{ opacity: 0, y: 5 }} 
                  transition={{ duration: 0.15 }} 
                  className="absolute z-50 top-full left-3 right-3 mt-1 bg-white border border-[#e5e5e5] shadow-lg py-1 max-h-[300px] overflow-y-auto"
                >
                  {entitlements.map(e => (
                    <button 
                      key={e.workspace_id} 
                      onClick={() => { onWorkspaceSelect(e.workspace_id); setIsWorkspaceMenuOpen(false); }} 
                      className="w-full flex items-center justify-between px-3 py-2 text-left text-[12px] text-[#09090b] hover:bg-[#f4f4f5] transition-colors focus:outline-none"
                    >
                      <span className="truncate pr-4">{e.workspace_name}</span>
                      {e.workspace_id === activeWorkspaceId && <Check size={14} className="text-[#09090b] shrink-0" />}
                    </button>
                  ))}
                  <div className="h-px bg-[#f4f4f5] my-1" />
                  <button 
                    onClick={() => { setIsCreateModalOpen(true); setIsWorkspaceMenuOpen(false); }} 
                    className="w-full flex items-center gap-2 px-3 py-2 text-left text-[12px] font-medium text-[#09090b] hover:bg-[#f4f4f5] transition-colors focus:outline-none"
                  >
                    <Plus size={14} className="text-[#a1a1aa]" /> Create New Workspace
                  </button>
                </motion.div>
              )}
            </AnimatePresence>
          </div>
        )}

        {/* REMOVED overflow-y-auto FROM THIS WRAPPER TO FIX THE FLYOUT CLIPPING BUG */}
        <div className="flex-1 py-4 flex flex-col gap-6">
          <nav className="space-y-0.5">
            <ModuleNav 
              title="Community" 
              basePath="/community" 
              icon={Users}
              links={[
                { label: "Dashboard", href: "/community/dashboard" },
                { label: "Subscribers", href: "/community/subscribers" },
                { label: "Plans & Products", href: "/community/plans" },
                { label: "Promotions", href: "/community/coupons" },
                { label: "Automations", href: "/community/automations" },
                { label: "Payment Settings", href: "/community/payment" },
                { label: "Email Templates", href: "/community/templates" }
              ]} 
            />
            <ModuleNav 
              title="Workspace" 
              basePath="/workspace" 
              icon={Settings}
              links={[
                { label: "General Settings", href: "/workspace/general" }
              ]} 
            />
          </nav>
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
                  <button onClick={onLogout} className="flex w-full items-center gap-2 px-2 py-1.5 text-xs text-red-600 hover:bg-rose-50 hover:text-red-700 transition-colors focus:outline-none">
                    <LogOut size={14} /> Log out
                  </button>
                </motion.div>
              )}
            </AnimatePresence>
          </div>
        </div>
      </motion.aside>

      {isCreateModalOpen && (
        <CreateWorkspaceModal 
          onClose={() => setIsCreateModalOpen(false)} 
          onSuccess={(id) => {
            setIsCreateModalOpen(false);
            onWorkspaceSelect(id);
          }} 
        />
      )}
    </>
  );
}
