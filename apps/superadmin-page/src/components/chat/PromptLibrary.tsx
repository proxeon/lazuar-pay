import { useState, useMemo } from "react";
import { X, Search, ChevronRight } from "lucide-react";
import { PROMPT_LIBRARY } from "../../lib/prompt-library";
import { cn } from "../../lib/utils";

interface PromptLibraryProps {
  isOpen: boolean;
  onClose: () => void;
  onSelect: (query: string) => void;
}

export default function PromptLibrary({ isOpen, onClose, onSelect }: PromptLibraryProps) {
  const [search, setSearch] = useState("");
  const [expandedId, setExpandedId] = useState<string | null>(null);

  const filteredCategories = useMemo(() => {
    if (!search.trim()) return PROMPT_LIBRARY;
    const lower = search.toLowerCase();
    return PROMPT_LIBRARY.map(cat => ({
      ...cat,
      prompts: cat.prompts.filter(p => 
        p.label.toLowerCase().includes(lower) || 
        p.query.toLowerCase().includes(lower) ||
        cat.title.toLowerCase().includes(lower)
      )
    })).filter(cat => cat.prompts.length > 0);
  }, [search]);

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/20 backdrop-blur-sm animate-in fade-in duration-200" onClick={onClose}>
      <div 
        className="relative w-full max-w-2xl max-h-[80vh] bg-white border border-[#e5e5e5] flex flex-col overflow-hidden animate-in zoom-in-95 duration-200"
        onClick={e => e.stopPropagation()}
      >
        <div className="flex items-center justify-between p-4 border-b border-[#e5e5e5] bg-[#fafafa] shrink-0">
          <h2 className="text-[14px] font-bold uppercase tracking-widest text-[#09090b]">Operations Playbook</h2>
          <button onClick={onClose} className="p-1 hover:bg-[#e5e5e5] transition-colors">
            <X size={16} className="text-[#71717a]" />
          </button>
        </div>

        <div className="p-4 border-b border-[#e5e5e5] shrink-0">
          <div className="relative">
            <Search size={14} className="absolute left-3 top-2.5 text-[#a1a1aa]" />
            <input
              type="text"
              placeholder="Search capabilities..."
              value={search}
              onChange={e => setSearch(e.target.value)}
              className="w-full h-9 pl-9 pr-3 bg-white border border-[#e5e5e5] text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] font-mono"
              autoFocus
            />
          </div>
        </div>

        <div className="flex-1 overflow-y-auto p-4 space-y-4">
          {filteredCategories.length === 0 ? (
            <div className="text-center py-12 text-[#71717a] text-[13px]">No matching operations found.</div>
          ) : (
            filteredCategories.map(cat => (
              <div key={cat.id} className="border border-[#e5e5e5] bg-white">
                <button
                  onClick={() => setExpandedId(expandedId === cat.id ? null : cat.id)}
                  className="w-full flex items-center justify-between p-3 hover:bg-[#fafafa] transition-colors text-left"
                >
                  <div className="flex items-center gap-3">
                    <div className="p-1.5 bg-[#f4f4f5] border border-[#e5e5e5] text-[#52525b]">
                      <cat.icon size={14} />
                    </div>
                    <div>
                      <h3 className="text-[13px] font-bold text-[#09090b]">{cat.title}</h3>
                      <p className="text-[11px] text-[#71717a] font-mono uppercase tracking-wider">{cat.description}</p>
                    </div>
                  </div>
                  <ChevronRight size={14} className={cn("text-[#a1a1aa] transition-transform", expandedId === cat.id && "rotate-90")} />
                </button>
                
                {expandedId === cat.id && (
                  <div className="border-t border-[#e5e5e5] bg-[#fafafa]/50 p-2 grid grid-cols-1 md:grid-cols-2 gap-2 animate-in slide-in-from-top-2 duration-200">
                    {cat.prompts.map((p, i) => (
                      <button
                        key={i}
                        onClick={() => onSelect(p.query)}
                        className="text-left p-3 bg-white border border-[#e5e5e5] hover:border-[#09090b] transition-all group"
                      >
                        <span className="text-[12px] font-bold text-[#09090b] block mb-1 group-hover:text-blue-600">{p.label}</span>
                        <span className="text-[11px] text-[#71717a] leading-relaxed line-clamp-2">{p.query}</span>
                      </button>
                    ))}
                  </div>
                )}
              </div>
            ))
          )}
        </div>
      </div>
    </div>
  );
}
