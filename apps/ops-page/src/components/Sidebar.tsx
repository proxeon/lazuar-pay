import { useState, useEffect, useRef } from "react";
import { Link, useLocation } from "react-router-dom";
import { cn } from "../lib/utils";
import { 
  LogOut, 
  PanelLeftClose, 
  PanelLeftOpen, 
  Settings,
  ChevronDown,
  Users,
  Box,
  ShoppingCart,
  Zap,
  Mail
} from "lucide-react";
import { motion, AnimatePresence } from "motion/react";
import type { AuthUser } from "../lib/api-client";

interface SidebarProps {
  isOpen: boolean;
  setIsOpen: () => void;
  isMobile?: boolean;
  user: AuthUser;
  onLogout: () => void;
}

export default function Sidebar({
  isOpen, setIsOpen, isMobile, user, onLogout
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

  const ModuleNav = ({ title, basePath, icon: Icon, links }: { title: string, basePath: string | string[], icon: any, links: { label: string, href: string }[] }) => {
    const basePaths = Array.isArray(basePath) ? basePath : [basePath];
    const isActiveModule = basePaths.some(path => location.pathname.startsWith(path));
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

     {/* FIXED: Removed overflow-y-auto here to comply with ADR-012.
     Overflow clips absolute positioned flyout menus in collapsed mode. */}
      {/* ADR-012 COMPLIANCE + SCROLL FIX: Apply overflow-y-auto ONLY when expanded 
      so the user profile stays anchored. When collapsed, allow overflow-visible for flyouts. */}
      <div className={cn("flex-1 py-4 flex flex-col gap-6", expanded ? "overflow-y-auto" : "overflow-visible")}>
        <nav className="space-y-0.5">
          <ModuleNav 
            title="Commerce" 
            basePath={["/commerce", "/community/dunning-schedules"]} 
            icon={ShoppingCart}
            links={[
              { label: "Dashboard", href: "/commerce/dashboard" },
              { label: "Checkout Links", href: "/commerce/products" },
              { label: "Subscribers", href: "/commerce/subscribers" },
              { label: "Transaction Logs", href: "/commerce/transactions" },
              { label: "Promotions", href: "/commerce/coupons" },
              { label: "Dunning Schedules", href: "/community/dunning-schedules" },
              { label: "Gateway Settings", href: "/commerce/payment" }
            ]} 
          />
          <ModuleNav 
            title="Communications" 
            basePath={["/community/broadcasts", "/community/templates"]} 
            icon={Mail}
            links={[
              { label: "Bulk Broadcast", href: "/community/broadcasts" },
              { label: "Message Templates", href: "/community/templates" }
            ]} 
          />
          <ModuleNav 
            title="Community" 
            basePath="/community/spaces" 
            icon={Users}
            links={[
              { label: "Community Spaces", href: "/community/spaces" }
            ]} 
          />
          <ModuleNav 
            title="Vault" 
            basePath="/vault" 
            icon={Box}
            links={[
              { label: "Digital Files", href: "/vault/products" }
            ]} 
          />
          <ModuleNav 
            title="Developer" 
            basePath="/developer" 
            icon={Zap}
            links={[
              { label: "Outbound Webhooks", href: "/developer/webhooks" }
            ]} 
          />
          <ModuleNav 
            title="Workspace" 
            basePath="/workspace" 
            icon={Settings}
            links={[
              { label: "General Settings", href: "/workspace/general" },
              { label: "Platform Billing", href: "/workspace/billing" }
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
  );
}
