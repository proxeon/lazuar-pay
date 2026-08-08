import { ReactNode, useEffect } from "react";
import { X } from "lucide-react";

interface SidePanelProps {
  isOpen: boolean;
  onClose: () => void;
  title: ReactNode;
  subtitle?: ReactNode;
  children: ReactNode;
  footer?: ReactNode;
  disableOutsideClick?: boolean;
}

export default function SidePanel({
  isOpen,
  onClose,
  title,
  subtitle,
  children,
  footer,
  disableOutsideClick = false
}: SidePanelProps) {
  useEffect(() => {
    const handleKeyDown = (e: KeyboardEvent) => {
      if (e.key === "Escape" && !disableOutsideClick) {
        onClose();
      }
    };
    if (isOpen) window.addEventListener("keydown", handleKeyDown);
    return () => window.removeEventListener("keydown", handleKeyDown);
  }, [isOpen, onClose, disableOutsideClick]);

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex justify-end">
      <div 
        className="absolute inset-0 bg-black/20 backdrop-blur-sm transition-opacity" 
        onClick={() => !disableOutsideClick && onClose()} 
      />
      <div className="relative w-full sm:max-w-md bg-white border-l border-[#e5e5e5] h-full shadow-2xl flex flex-col animate-in slide-in-from-right duration-300">
        <div className="flex items-center justify-between p-5 border-b border-[#e5e5e5] bg-[#fafafa] shrink-0">
          <div>
            <h2 className="text-[15px] font-bold text-[#09090b]">{title}</h2>
            {subtitle && <p className="text-[11px] font-mono text-[#71717a] mt-0.5">{subtitle}</p>}
          </div>
          <button 
            onClick={onClose} 
            disabled={disableOutsideClick}
            className="p-1.5 text-[#a1a1aa] hover:bg-[#e5e5e5] hover:text-[#09090b] transition-colors rounded-sm disabled:opacity-50"
          >
            <X size={16} />
          </button>
        </div>
        
        <div className="flex-1 overflow-y-auto p-6">
          {children}
        </div>

        {footer && (
          <div className="p-5 border-t border-[#e5e5e5] bg-[#fafafa]/50 flex items-center justify-end gap-2 shrink-0">
            {footer}
          </div>
        )}
      </div>
    </div>
  );
}
