import { ReactNode, useState, useEffect, useRef } from "react";
import { Link, useOutletContext } from "react-router-dom";
import { ChevronRight, ChevronDown, Check, Plus, Building2 } from "lucide-react";
import { motion, AnimatePresence } from "motion/react";
import type { OpsOutletContext } from "../../../App";
import CreateWorkspaceModal from "../../workspace/components/CreateWorkspaceModal";

interface Breadcrumb {
  label: string;
  href?: string;
}

interface PageLayoutProps {
  title: string;
  description?: string;
  breadcrumbs?: Breadcrumb[];
  actionButton?: ReactNode;
  children: ReactNode;
}

export default function PageLayout({ title, description, breadcrumbs, actionButton, children }: PageLayoutProps) {
  const { activeWorkspaceId, entitlements, onWorkspaceSelect } = useOutletContext<OpsOutletContext>();

  const [isWorkspaceMenuOpen, setIsWorkspaceMenuOpen] = useState(false);
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const workspaceMenuRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (workspaceMenuRef.current && !workspaceMenuRef.current.contains(event.target as Node)) {
        setIsWorkspaceMenuOpen(false);
      }
    }
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  const activeWorkspaceName = entitlements.find(e => e.workspace_id === activeWorkspaceId)?.workspace_name || "Select Workspace";

  return (
    <div className="flex-1 flex flex-col h-full overflow-hidden bg-[#fafafa]">
      
      {/* Slim Anchored Top Bar */}
      <div className="px-6 py-2.5 md:px-8 border-b border-[#e5e5e5] bg-white shrink-0 z-20">
        <div className="max-w-6xl mx-auto w-full flex items-center justify-between min-h-[28px]">
          
          <nav className="flex items-center gap-1.5 shrink-0 pr-4">
            {breadcrumbs && breadcrumbs.map((bc, idx) => (
              <div key={idx} className="flex items-center gap-1.5">
                {bc.href ? (
                  <Link 
                    to={bc.href} 
                    className="text-[10px] font-bold uppercase tracking-widest text-[#a1a1aa] hover:text-[#09090b] transition-colors"
                  >
                    {bc.label}
                  </Link>
                ) : (
                  <span className="text-[10px] font-bold uppercase tracking-widest text-[#09090b]">
                    {bc.label}
                  </span>
                )}
                {idx < breadcrumbs.length - 1 && (
                  <ChevronRight size={12} className="text-[#d4d4d8] shrink-0" />
                )}
              </div>
            ))}
          </nav>

          <div className="relative shrink-0" ref={workspaceMenuRef}>
            <button 
              onClick={() => setIsWorkspaceMenuOpen(!isWorkspaceMenuOpen)} 
              className="flex items-center gap-2 h-7 px-2 hover:bg-[#fafafa] transition-colors focus:outline-none rounded-sm"
            >
              <Building2 size={12} className="text-[#a1a1aa] shrink-0" />
              <span className="text-[11px] font-semibold text-[#09090b] truncate max-w-[120px] sm:max-w-[200px]">
                {activeWorkspaceName}
              </span>
              <ChevronDown size={12} className="text-[#a1a1aa] shrink-0" />
            </button>

            <AnimatePresence>
              {isWorkspaceMenuOpen && (
                <motion.div 
                  initial={{ opacity: 0, y: 5 }} 
                  animate={{ opacity: 1, y: 0 }} 
                  exit={{ opacity: 0, y: 5 }} 
                  transition={{ duration: 0.15 }} 
                  className="absolute z-50 top-full right-0 mt-1 w-64 bg-white border border-[#e5e5e5] shadow-lg py-1 max-h-[300px] overflow-y-auto"
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

        </div>
      </div>

      {/* Scrollable Main Content Container */}
      <div className="flex-1 overflow-y-auto p-6 md:p-8">
        <div className="max-w-6xl mx-auto w-full">
          
          <div className="flex flex-col sm:flex-row sm:items-start justify-between gap-4 mb-8 pb-6 border-b border-[#e5e5e5]">
            <div className="space-y-1.5 max-w-2xl">
              <h1 className="text-xl font-bold text-[#09090b] tracking-tight">
                {title}
              </h1>
              {description && (
                <p className="text-[13px] text-[#71717a] leading-normal">
                  {description}
                </p>
              )}
            </div>
            {actionButton && (
              <div className="shrink-0 pt-0.5">
                {actionButton}
              </div>
            )}
          </div>

          <div className="w-full">
            {children}
          </div>

        </div>
      </div>

      {isCreateModalOpen && (
        <CreateWorkspaceModal 
          onClose={() => setIsCreateModalOpen(false)} 
          onSuccess={(id) => {
            setIsCreateModalOpen(false);
            onWorkspaceSelect(id);
          }} 
        />
      )}
    </div>
  );
}
