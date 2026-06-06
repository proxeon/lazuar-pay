import { cn } from "../lib/utils";
import { 
  LayoutDashboard, 
  Users, 
  BarChart3, 
  Settings, 
  LogOut, 
  PanelLeftClose, 
  PanelLeftOpen 
} from "lucide-react";
import { motion, AnimatePresence } from "motion/react";
import { useState, useEffect, useRef } from "react";
import { Link, useLocation } from "react-router-dom";

interface SidebarProps {
  isOpen: boolean;
  setIsOpen: (isOpen: boolean) => void;
  isMobile?: boolean;
}

export default function Sidebar({ isOpen, setIsOpen, isMobile }: SidebarProps) {
  const [isUserMenuOpen, setIsUserMenuOpen] = useState(false);
  const userMenuRef = useRef<HTMLDivElement>(null);
  const location = useLocation();

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

  // Updated to list "Users" mapping to "/users"
  const menuItems = [
    { icon: LayoutDashboard, label: "Overview", path: "/dashboard" },
    { icon: Users, label: "Users", path: "/users" },
    { icon: BarChart3, label: "Performance", path: "/performance" },
  ];

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
      <div className="flex h-[56px] w-full shrink-0 items-center overflow-hidden border-b border-[#e5e5e5] px-5">
        <div className="flex items-center gap-3">
          {!isMobile && (
            <button
              onClick={() => setIsOpen(!isOpen)}
              className="group relative flex h-6 w-6 shrink-0 items-center justify-center rounded-[4px] focus:outline-none focus-visible:ring-2 focus-visible:ring-[#09090b] focus-visible:ring-offset-2"
              title={expanded ? "Collapse Sidebar" : "Expand Sidebar"}
            >
              <div className="absolute inset-0 flex h-full w-full items-center justify-center rounded-[4px] bg-[#09090b] transition-opacity duration-200 group-hover:opacity-0">
                <div className="h-1.5 w-1.5 rounded-[1px] bg-white opacity-90" />
              </div>
              <div className="absolute inset-0 flex h-full w-full items-center justify-center rounded-[4px] bg-[#f4f4f5] text-[#09090b] opacity-0 transition-opacity duration-200 group-hover:opacity-100 border border-[#e5e5e5]">
                {expanded ? <PanelLeftClose size={14} strokeWidth={2} /> : <PanelLeftOpen size={14} strokeWidth={2} />}
              </div>
            </button>
          )}
          {isMobile && (
            <div className="flex h-6 w-6 shrink-0 items-center justify-center rounded-[4px] bg-[#09090b]">
              <div className="h-1.5 w-1.5 rounded-[1px] bg-white opacity-90" />
            </div>
          )}
          
          <motion.span
            initial={false}
            animate={{ width: expanded ? "auto" : 0, opacity: expanded ? 1 : 0 }}
            transition={{ duration: 0.2 }}
            className="whitespace-nowrap text-[14px] font-semibold tracking-tight text-[#09090b]"
          >
            Metrics.app
          </motion.span>
        </div>
      </div>

      <nav className="flex-1 space-y-[2px] overflow-x-hidden p-3 py-4 text-sm font-medium">
        {menuItems.map((item, index) => {
          const isActive = location.pathname.startsWith(item.path);

          return (
            <Link
              key={index}
              to={item.path}
              onClick={() => isMobile && setIsOpen(false)}
              title={!expanded ? item.label : undefined}
              className={cn(
                "flex h-8 w-full items-center rounded-md transition-colors overflow-hidden relative focus:outline-none",
                isActive
                  ? "bg-[#f4f4f5] text-[#09090b]"
                  : "text-[#71717a] hover:bg-[#fafafa] hover:text-[#09090b]"
              )}
            >
              <div className="flex h-full w-[40px] shrink-0 items-center justify-center">
                <item.icon className="shrink-0" size={15} strokeWidth={isActive ? 2.5 : 2} />
              </div>
              
              <motion.div
                initial={false}
                animate={{ opacity: expanded ? 1 : 0, filter: expanded ? "blur(0px)" : "blur(2px)" }}
                transition={{ duration: 0.2, ease: "easeOut" }}
                className="whitespace-nowrap text-[13px] font-medium leading-none"
              >
                {item.label}
              </motion.div>
            </Link>
          );
        })}
      </nav>

      <div className="shrink-0 border-t border-[#e5e5e5] p-3 overflow-visible relative">
        <div className="relative" ref={userMenuRef}>
          <button 
            onClick={() => setIsUserMenuOpen(!isUserMenuOpen)}
            className="group flex h-10 w-full items-center rounded-md transition-colors hover:bg-[#fafafa] overflow-hidden text-left focus:outline-none focus:ring-1 focus:ring-[#e5e5e5]"
            title={!expanded ? "User Profile" : undefined}
          >
            <div className="flex w-[40px] shrink-0 items-center justify-center">
              <div className="flex h-7 w-7 shrink-0 items-center justify-center rounded-[4px] bg-[#f4f4f5] border border-[#e5e5e5] text-[10px] font-semibold text-[#52525b] group-hover:bg-white transition-colors">
                AU
              </div>
            </div>
            <motion.div
              initial={false}
              animate={{ opacity: expanded ? 1 : 0, filter: expanded ? "blur(0px)" : "blur(2px)" }}
              transition={{ duration: 0.2, ease: "easeOut" }}
              className="flex flex-col gap-[2px]"
            >
              <span className="whitespace-nowrap text-[13px] font-medium leading-none text-[#09090b]">Admin User</span>
              <span className="whitespace-nowrap text-[11px] font-medium leading-none text-[#71717a]">admin@metrics.io</span>
            </motion.div>
          </button>
          
          <AnimatePresence>
            {isUserMenuOpen && (
              <motion.div
                initial={{ opacity: 0, y: 5, scale: 0.98 }}
                animate={{ opacity: 1, y: 0, scale: 1 }}
                exit={{ opacity: 0, y: 5, scale: 0.98 }}
                transition={{ duration: 0.15, ease: "easeOut" }}
                className={cn(
                  "absolute z-50 rounded-lg border border-[#e5e5e5] bg-white p-1 shadow-[0_4px_12px_rgba(0,0,0,0.05)] overflow-hidden",
                  expanded ? "bottom-[calc(100%+8px)] left-0 w-full min-w-[200px]" : "bottom-0 left-[calc(100%+8px)] min-w-[200px]"
                )}
              >
                  <div className="px-2 py-1.5 border-b border-[#f4f4f5] mb-1">
                    <span className="block text-[13px] font-medium text-[#09090b]">Admin User</span>
                    <span className="block text-[11px] text-[#71717a]">admin@metrics.io</span>
                  </div>
                  <button className="flex w-full items-center gap-2 rounded-sm px-2 py-1.5 text-[13px] text-[#71717a] hover:bg-[#f4f4f5] hover:text-[#09090b] transition-colors focus:outline-none focus:bg-[#f4f4f5]">
                    <Settings size={14} />
                    Settings
                  </button>
                  <button className="flex w-full items-center gap-2 rounded-sm px-2 py-1.5 text-[13px] text-red-600 hover:bg-red-50 hover:text-red-700 transition-colors focus:outline-none focus:bg-red-50">
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
