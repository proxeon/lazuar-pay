import { useState } from "react";
import { ExternalLink, Layers, PlayCircle, Users } from "lucide-react";
import { toast } from "sonner";
import { cn } from "../lib/utils";

// --- MOCK DATA ---
type AppType = "COMMUNITY" | "VAULT" | "CONSULT";
type EntitlementStatus = "ACTIVE" | "PAUSED" | "EXPIRED";

interface Entitlement {
  id: string;
  tenantName: string;
  tenantLogoInitials: string;
  productName: string;
  appType: AppType;
  status: EntitlementStatus;
  url: string;
}

const mockEntitlements: Entitlement[] = [
  { id: "sub_1", tenantName: "Design Masters HQ", tenantLogoInitials: "DM", productName: "Founders Mastermind", appType: "COMMUNITY", status: "ACTIVE", url: "https://design.lazuar.com/portal" },
  { id: "sub_2", tenantName: "CodeCrafters Academy", tenantLogoInitials: "CA", productName: "Fullstack Engineering Bootcamp", appType: "VAULT", status: "ACTIVE", url: "https://codecrafters.lazuar.com/vault" },
  { id: "sub_3", tenantName: "Growth Hackers", tenantLogoInitials: "GH", productName: "SEO Deep Dive (2025 Edition)", appType: "VAULT", status: "PAUSED", url: "https://growth.lazuar.com/vault" },
  { id: "sub_4", tenantName: "Design Masters HQ", tenantLogoInitials: "DM", productName: "1-on-1 Portfolio Review", appType: "CONSULT", status: "EXPIRED", url: "https://design.lazuar.com/consult" },
  // Added a 5th one to show how it wraps into the second row beautifully with 4 columns
  { id: "sub_5", tenantName: "Growth Hackers", tenantLogoInitials: "GH", productName: "Ads Scaling Workshop", appType: "VAULT", status: "ACTIVE", url: "https://growth.lazuar.com/vault" }
];

export default function Launchpad() {
  const [filter, setFilter] = useState<"ALL" | AppType>("ALL");

  const filteredData = mockEntitlements.filter(item => filter === "ALL" || item.appType === filter);

  const handleAccessClick = (entitlement: Entitlement) => {
    if (entitlement.status === "EXPIRED") {
      toast.error(`Access to ${entitlement.productName} has expired. Please renew your subscription.`);
      return;
    }
    toast.success(`Authenticating session...`, {
      description: `Redirecting seamlessly to ${new URL(entitlement.url).hostname}`,
      duration: 3000,
    });
  };

  return (
    // Widened the max-w slightly to comfortably fit 4 square cards
    <div className="flex-1 w-full p-4 md:p-8 mx-auto max-w-[1400px] flex flex-col gap-6 animate-in fade-in duration-300">
      
      <header className="flex flex-col md:flex-row md:items-end justify-between pb-2 gap-4 border-b border-[#e5e5e5]">
        <div>
          <h1 className="text-[20px] font-semibold tracking-tight text-[#09090b]">My Ecosystem</h1>
          <p className="text-[13px] text-[#71717a] mt-1 font-mono uppercase tracking-wider">A unified view of everything you own.</p>
        </div>

        <div className="flex bg-white border border-[#e5e5e5] p-1 rounded-none shadow-sm self-start">
          <button onClick={() => setFilter("ALL")} className={cn("px-3 py-1.5 text-[11px] font-bold uppercase tracking-widest rounded-none transition-colors", filter === "ALL" ? "bg-[#09090b] text-white" : "text-[#71717a] hover:text-[#09090b]")}>All</button>
          <button onClick={() => setFilter("COMMUNITY")} className={cn("px-3 py-1.5 text-[11px] font-bold uppercase tracking-widest rounded-none transition-colors", filter === "COMMUNITY" ? "bg-[#09090b] text-white" : "text-[#71717a] hover:text-[#09090b]")}>Communities</button>
          <button onClick={() => setFilter("VAULT")} className={cn("px-3 py-1.5 text-[11px] font-bold uppercase tracking-widest rounded-none transition-colors", filter === "VAULT" ? "bg-[#09090b] text-white" : "text-[#71717a] hover:text-[#09090b]")}>Vaults</button>
        </div>
      </header>

      {filteredData.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-24 text-center border border-dashed border-[#e5e5e5] rounded-none bg-white/50">
          <Layers className="h-10 w-10 text-[#a1a1aa] mb-4" />
          <h3 className="text-[15px] font-bold uppercase tracking-widest text-[#09090b]">No access found</h3>
          <p className="text-[13px] text-[#71717a] mt-1 max-w-sm">You don't have any active entitlements matching this category.</p>
        </div>
      ) : (
        // Changed to xl:grid-cols-4 for 4 items per row
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6">
          {filteredData.map((item) => (
            <AccessCard key={item.id} entitlement={item} onClick={() => handleAccessClick(item)} />
          ))}
        </div>
      )}
    </div>
  );
}

function AccessCard({ entitlement, onClick }: { entitlement: Entitlement; onClick: () => void }) {
  const TypeIcon = entitlement.appType === "COMMUNITY" ? Users : entitlement.appType === "VAULT" ? PlayCircle : Layers;
  
  const statusColor = 
    entitlement.status === "ACTIVE" ? "bg-emerald-50 text-emerald-700 border-emerald-200" :
    entitlement.status === "PAUSED" ? "bg-amber-50 text-amber-700 border-amber-200" :
    "bg-rose-50 text-rose-700 border-rose-200";

  return (
    <div 
      onClick={onClick}
      // Added aspect-square to force a perfect 1:1 ratio
      className={cn(
        "group flex flex-col aspect-square bg-white border border-[#e5e5e5] rounded-none shadow-sm hover:shadow-[4px_4px_0px_0px_rgba(0,0,0,0.05)] transition-all duration-200",
        entitlement.status !== "EXPIRED" ? "cursor-pointer hover:border-[#a1a1aa] hover:-translate-y-1" : "opacity-75 cursor-not-allowed grayscale-[30%]"
      )}
    >
      <div className="px-5 py-4 border-b border-[#f4f4f5] bg-[#fafafa]/50 flex items-center justify-between shrink-0">
        <div className="flex items-center gap-2.5">
          <div className="flex h-7 w-7 items-center justify-center rounded-none bg-white border border-[#e5e5e5] text-[10px] font-bold text-[#09090b] shadow-sm shrink-0">
            {entitlement.tenantLogoInitials}
          </div>
          <span className="text-[11px] font-bold tracking-widest text-[#52525b] uppercase truncate">{entitlement.tenantName}</span>
        </div>
        <ExternalLink size={14} className="text-[#a1a1aa] group-hover:text-[#09090b] transition-colors shrink-0 ml-2" />
      </div>

      <div className="p-5 flex-1 flex flex-col justify-between">
        <div>
          <div className="flex flex-wrap items-center gap-2 mb-4">
            <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-none bg-[#f4f4f5] border border-[#e5e5e5] text-[9px] font-bold uppercase tracking-widest text-[#52525b]">
              <TypeIcon size={10} /> {entitlement.appType}
            </span>
            <span className={cn("inline-flex items-center px-2 py-0.5 rounded-none border text-[9px] font-bold uppercase tracking-widest", statusColor)}>
              {entitlement.status}
            </span>
          </div>
          
          {/* Added line-clamp-3 so extremely long titles don't overflow the square */}
          <h2 className="text-[18px] font-semibold text-[#09090b] leading-snug group-hover:text-blue-600 transition-colors line-clamp-3">
            {entitlement.productName}
          </h2>
        </div>
        
        <div className="mt-auto pt-4 shrink-0">
          <button 
            className={cn(
              "w-full h-10 rounded-none text-[11px] uppercase tracking-widest font-bold transition-colors shadow-sm",
              entitlement.status !== "EXPIRED" 
                ? "bg-[#09090b] text-white group-hover:bg-[#27272a]" 
                : "bg-[#f4f4f5] text-[#a1a1aa] border border-[#e5e5e5]"
            )}
          >
            {entitlement.status === "EXPIRED" ? "Renew Access" : "Open App"}
          </button>
        </div>
      </div>
    </div>
  );
}
