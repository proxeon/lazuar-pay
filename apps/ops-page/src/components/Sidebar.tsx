import { useState, useEffect, useRef } from "react";
import { Link, useLocation } from "react-router-dom";
import { cn } from "../lib/utils";
import { 
  LogOut, 
  PanelLeftClose, 
  PanelLeftOpen, 
  Building2, 
  Settings, 
  Users, 
  Activity, 
  Package, 
  Tag, 
  Zap,
  Mail
} from "lucide-react";
import { motion, AnimatePresence } from "motion/react";
import type { AuthUser, EntitlementDto } from "../lib/api-client";

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
  const userMenuRef = useRef<HTMLDivElement>(null);
  const expanded = isMobile ? true : isOpen;
  const location = useLocation();

  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (userMenuRef.current && !userMenuRef.current.contains(event.target as Node)) {
        setIsUserMenuOpen(false);
      }
    }
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  const NavItem = ({ to, icon: Icon, label }: { to: string, icon: any, label: string }) => {
    const isActive = location.pathname.startsWith(to);
    return (
      <Link 
        to={to} 
        onClick={() => isMobile && setIsOpen()} 
        className={cn(
          "group flex h-9 w-full items-center text-left focus:outline-none transition-colors relative", 
          isActive ? "bg-[#f4f4f5] text-[#09090b] font-semibold" : "text-[#71717a] hover:bg-[#fafafa] hover:text-[#09090b]"
        )}
      >
        <div className="w-12 h-full shrink-0 flex items-center justify-center">
          <Icon size={16} />
        </div>
        {expanded ? (
          <span className="text-[13px] truncate pr-4">{label}</span>
        ) : (
          <div className="absolute left-full ml-1 px-2 py-1.5 bg-[#09090b] text-white text-[10px] font-bold uppercase tracking-widest rounded-sm opacity-0 pointer-events-none group-hover:opacity-100 group-hover:pointer-events-auto z-50 whitespace-nowrap transition-opacity shadow-md">
            {label}
          </div>
        )}
      </Link>
    );
  };

  return (
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

      <div className="flex-1 overflow-y-auto py-4 flex flex-col gap-6">
        
        {/* Module Section: Community */}
        <div>
          {expanded && (
            <div className="px-4 mb-2">
              <span className="text-[10px] font-bold uppercase tracking-widest text-[#a1a1aa]">Community</span>
            </div>
          )}
          <nav className="space-y-0.5">
            <NavItem to="/community/dashboard" icon={Activity} label="Dashboard" />
            <NavItem to="/community/subscribers" icon={Users} label="Subscribers" />
            <NavItem to="/community/plans" icon={Package} label="Plans & Products" />
            <NavItem to="/community/coupons" icon={Tag} label="Promotions" />
            <NavItem to="/community/automations" icon={Zap} label="Automations" />
            <NavItem to="/community/settings/payment" icon={Settings} label="Payment Settings" />
            <NavItem to="/community/settings/templates" icon={Mail} label="Message Templates" />
          </nav>
        </div>

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
                
                {/* Clean global settings menu stripped of module-specific features */}
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
