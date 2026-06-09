import { ExternalLink, Layers, Loader2 } from "lucide-react";
import { toast } from "sonner";
import { useQuery } from "@tanstack/react-query";
import { cn } from "../lib/utils";
import { client, type Entitlement } from "../lib/api-client";

export default function Launchpad() {
  
  const { data: entitlements, isLoading } = useQuery({
    queryKey: ["entitlements"],
    queryFn: async () => {
      const { data, error } = await client.GET("/one/me/entitlements");
      if (error) throw new Error(error.detail || "Failed to fetch entitlements");
      return data ?? [];
    }
  });

  const getAppUrl = (workspaceSlug: string) => {
    const isLocalhost = 
      window.location.hostname === "localhost" || 
      window.location.hostname === "127.0.0.1";
    
    if (isLocalhost) {
      // Directs to Next.js community-page locally mapping the slug parameter
      return `http://localhost:3020/${workspaceSlug}`;
    }
    // Production wildcard routing
    return `https://${workspaceSlug}.lazuar.com`;
  };

  const handleAccessClick = (entitlement: Entitlement) => {
    const resolvedUrl = getAppUrl(entitlement.workspace_slug);

    toast.success(`Authenticating session...`, {
      description: `Connecting securely to ${entitlement.workspace_name}...`,
      duration: 2000,
    });

    setTimeout(() => {
      window.open(resolvedUrl, "_blank", "noopener,noreferrer");
    }, 800);
  };

  return (
    <div className="flex-1 w-full p-4 md:p-8 mx-auto max-w-[1400px] flex flex-col gap-6 animate-in fade-in duration-300">
      
      <header className="flex flex-col md:flex-row md:items-end justify-between pb-2 gap-4 border-b border-[#e5e5e5]">
        <div>
          <h1 className="text-[20px] font-semibold tracking-tight text-[#09090b]">My Ecosystem</h1>
          <p className="text-[13px] text-[#71717a] mt-1 font-mono uppercase tracking-wider">A unified view of everything you own.</p>
        </div>
      </header>

      {isLoading ? (
        <div className="flex flex-col items-center justify-center py-24 text-center">
          <Loader2 className="h-8 w-8 text-[#a1a1aa] animate-spin mb-4" />
          <h3 className="text-[12px] font-bold uppercase tracking-widest text-[#71717a]">Resolving Entitlements...</h3>
        </div>
      ) : !entitlements || entitlements.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-24 text-center border border-dashed border-[#e5e5e5] rounded-none bg-white/50">
          <Layers className="h-10 w-10 text-[#a1a1aa] mb-4" />
          <h3 className="text-[15px] font-bold uppercase tracking-widest text-[#09090b]">No ecosystems found</h3>
          <p className="text-[13px] text-[#71717a] mt-1 max-w-sm">You do not currently have access to any workspaces.</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6">
          {entitlements.map((entitlement) => (
            <AccessCard 
              key={entitlement.workspace_id} 
              entitlement={entitlement} 
              onClick={() => handleAccessClick(entitlement)} 
            />
          ))}
        </div>
      )}
    </div>
  );
}

function AccessCard({ entitlement, onClick }: { entitlement: Entitlement; onClick: () => void }) {
  return (
    <div 
      onClick={onClick}
      className="group flex flex-col h-[280px] w-full bg-white border border-[#e5e5e5] rounded-none shadow-sm hover:shadow-[4px_4px_0px_0px_rgba(0,0,0,0.05)] cursor-pointer hover:border-[#a1a1aa] hover:-translate-y-1 transition-all duration-200"
    >
      <div className="px-5 py-4 border-b border-[#f4f4f5] bg-[#fafafa]/50 flex items-center justify-between shrink-0">
        <span className="text-[11px] font-bold tracking-widest text-[#52525b] uppercase truncate">
          {entitlement.role} ACCESS
        </span>
        <ExternalLink size={14} className="text-[#a1a1aa] group-hover:text-[#09090b] transition-colors shrink-0 ml-2" />
      </div>

      <div className="p-5 flex-1 flex flex-col justify-between">
        <div>
          <h2 className="text-[18px] font-semibold text-[#09090b] leading-snug group-hover:text-blue-600 transition-colors line-clamp-2">
            {entitlement.workspace_name}
          </h2>
          <p className="text-[11px] font-mono text-[#71717a] mt-2">
            {entitlement.workspace_slug}.lazuar.com
          </p>
        </div>
        
        <div className="mt-auto pt-4 shrink-0">
          <button 
            type="button"
            className="w-full h-10 rounded-none text-[11px] uppercase tracking-widest font-bold transition-colors shadow-sm pointer-events-none bg-[#09090b] text-white group-hover:bg-[#27272a]"
          >
            Launch Platform
          </button>
        </div>
      </div>
    </div>
  );
}
