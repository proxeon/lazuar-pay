import { useState, useEffect } from "react";
import { X } from "lucide-react";

interface CreateTenantModalProps {
  onClose: () => void;
  onSuccess: (name: string, slug: string) => void;
}

export default function CreateTenantModal({ onClose, onSuccess }: CreateTenantModalProps) {
  const [name, setName] = useState("");
  const [slug, setSlug] = useState("");

  // Auto-slugify effect
  useEffect(() => {
    const generatedSlug = name
      .trim()
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '-') // Replace non-alphanumeric chars with hyphens
      .replace(/(^-|-$)+/g, '');   // Remove leading or trailing hyphens
    
    setSlug(generatedSlug);
  }, [name]);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim() || !slug.trim()) return;
    onSuccess(name.trim(), slug);
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      {/* Backdrop */}
      <div 
        className="absolute inset-0 bg-black/20 backdrop-blur-sm transition-opacity" 
        onClick={onClose} 
      />
      
      {/* Modal Container */}
      <div className="relative bg-white border border-[#e5e5e5] rounded-none shadow-[8px_8px_0px_0px_rgba(0,0,0,0.1)] w-full max-w-md overflow-hidden flex flex-col animate-in fade-in zoom-in-95 duration-200">
        
        {/* Header */}
        <div className="flex items-center justify-between p-5 border-b border-[#e5e5e5] shrink-0">
          <div>
            <h3 className="text-[14px] font-semibold tracking-tight text-[#09090b]">Create Workspace</h3>
            <p className="text-[11px] text-[#71717a] mt-0.5">Provision a new ecosystem tenant.</p>
          </div>
          <button 
            onClick={onClose} 
            className="text-[#a1a1aa] hover:bg-[#f4f4f5] hover:text-[#09090b] rounded-none transition-colors p-1"
          >
            <X size={16} />
          </button>
        </div>

        {/* Form */}
        <form onSubmit={handleSubmit} className="p-5 space-y-5">
          <div className="space-y-1.5">
            <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">
              Workspace Name *
            </label>
            <input 
              type="text" 
              required 
              autoFocus
              value={name}
              onChange={(e) => setName(e.target.value)}
              placeholder="e.g. Acme Corporation"
              className="flex h-10 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]" 
            />
          </div>

          <div className="space-y-1.5">
            <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">
              Generated Slug
            </label>
            <input 
              type="text" 
              readOnly 
              disabled
              value={slug}
              className="flex h-10 w-full rounded-none border border-[#e5e5e5] bg-[#fafafa] px-3 py-1 text-sm text-[#71717a] font-mono cursor-not-allowed outline-none shadow-inner" 
            />
            <p className="text-[10px] text-[#a1a1aa] mt-1">
              Used as the unique public identifier (e.g. {slug ? `${slug}.lazuar.com` : "tenant.lazuar.com"}).
            </p>
          </div>

          {/* Footer Actions */}
          <div className="flex items-center justify-end gap-3 pt-4 border-t border-[#f4f4f5] mt-2">
            <button 
              type="button" 
              onClick={onClose} 
              className="text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:text-[#09090b] transition-colors px-2 py-1"
            >
              Cancel
            </button>
            <button 
              type="submit" 
              disabled={!name.trim() || !slug.trim()}
              className="h-9 px-5 bg-[#09090b] text-white text-[11px] font-bold tracking-widest uppercase rounded-none hover:bg-[#27272a] disabled:opacity-50 transition-colors shadow-[4px_4px_0px_0px_rgba(0,0,0,0.1)] hover:shadow-none hover:translate-y-[2px] hover:translate-x-[2px] active:scale-95"
            >
              Create & Provision
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
