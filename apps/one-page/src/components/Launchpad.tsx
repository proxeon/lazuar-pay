import { useState } from "react";
import { ExternalLink, Layers, PlayCircle, Users } from "lucide-react";
import { toast } from "sonner";
import { cn } from "../lib/utils";

// --- 1. MOCK DATA (The State Matrix) ---
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
  {
    id: "sub_1",
    tenantName: "Design Masters HQ",
    tenantLogoInitials: "DM",
    productName: "Founders Mastermind",
    appType: "COMMUNITY",
    status: "ACTIVE",
    url: "https://design.lazuar.com/portal",
  },
  {
    id: "sub_2",
    tenantName: "CodeCrafters Academy",
    tenantLogoInitials: "CA",
    productName: "Fullstack Engineering Bootcamp",
    appType: "VAULT",
    status: "ACTIVE",
    url: "https://codecrafters.lazuar.com/vault",
  },
  {
    id: "sub_3",
    tenantName: "Growth Hackers",
    tenantLogoInitials: "GH",
    productName: "SEO Deep Dive (2025 Edition)",
    appType: "VAULT",
    status: "PAUSED",
    url: "https://growth.lazuar.com/vault",
  },
  {
    id: "sub_4",
    tenantName: "Design Masters HQ",
    tenantLogoInitials: "DM",
    productName: "1-on-1 Portfolio Review",
    appType: "CONSULT",
    status: "EXPIRED",
    url: "https://design.lazuar.com/consult",
  }
];

export default function Launchpad() {
  const [filter, setFilter] = useState<"ALL" | AppType>("ALL");

  const filteredData = mockEntitlements.filter(item => filter === "ALL" || item.appType === filter);

  // --- 5. Mock SSO Redirection ---
  const handleAccessClick = (entitlement: Entitlement) => {
    if (entitlement.status === "EXPIRED") {
      toast.error(`Access to ${entitlement.productName} has expired. Please renew your subscription.`);
      return;
    }
    
    // Simulating cross-domain cookie SSO
    toast.success(`Authenticating session...`, {
      description: `Redirecting seamlessly to ${new URL(entitlement.url).hostname}`,
      duration: 3000,
    });
  };

  return (
    <div className="flex-1 w-full p-4 md:p-8 mx-auto max-w-[1240px] flex flex-col gap-6 animate-in fade-in duration-300">
      
      {/* Header */}
      <header className="flex flex-col md:flex-row md:items-end justify-between pb-2 gap-4 border-b border-[#e5e5e5]">
        <div>
          <h1 className="text-[20px] font-semibold tracking-tight text-[#09090b]">My Ecosystem</h1>
          <p className="text-[13px] text-[#71717a] mt-1">A unified view of everything you own across the Lazuar platform.</p>
        </div>

        {/* --- 4. Categorization Tabs --- */}
        <div className="flex bg-white border border-[#e5e5e5] p-1 rounded-md shadow-sm self-start">
          <button 
            onClick={() => setFilter("ALL")}
            className={cn(
              "px-3 py-1.5 text-[12px] font-semibold tracking-wide rounded-sm transition-colors",
              filter === "ALL" ? "bg-[#09090b] text-white" : "text-[#71717a] hover:text-[#09090b]"
            )}
          >
            All
          </button>
          <button 
            onClick={() => setFilter("COMMUNITY")}
            className={cn(
              "px-3 py-1.5 text-[12px] font-semibold tracking-wide rounded-sm transition-colors",
              filter === "COMMUNITY" ? "bg-[#09090b] text-white" : "text-[#71717a] hover:text-[#09090b]"
            )}
          >
            Communities
          </button>
          <button 
            onClick={() => setFilter("VAULT")}
            className={cn(
              "px-3 py-1.5 text-[12px] font-semibold tracking-wide rounded-sm transition-colors",
              filter === "VAULT" ? "bg-[#09090b] text-white" : "text-[#71717a] hover:text-[#09090b]"
            )}
          >
            Vaults
          </button>
        </div>
      </header>

      {/* --- 3. The Launchpad Grid --- */}
      {filteredData.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-24 text-center border border-dashed border-[#e5e5e5] rounded-xl bg-white/50">
          <Layers className="h-10 w-10 text-[#a1a1aa] mb-4" />
          <h3 className="text-[15px] font-semibold text-[#09090b]">No access found</h3>
          <p className="text-[13px] text-[#71717a] mt-1 max-w-sm">You don't have any active entitlements matching this category.</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-5">
          {filteredData.map((item) => (
            <AccessCard key={item.id} entitlement={item} onClick={() => handleAccessClick(item)} />
          ))}
        </div>
      )}
    </div>
  );
}

// --- 2. Design the "Access Card" Component ---
function AccessCard({ entitlement, onClick }: { entitlement: Entitlement; onClick: () => void }) {
  
  // Visual mapping for App Types
  const TypeIcon = entitlement.appType === "COMMUNITY" ? Users : entitlement.appType === "VAULT" ? PlayCircle : Layers;
  
  // Visual mapping for Statuses
  const statusColor = 
    entitlement.status === "ACTIVE" ? "bg-emerald-50 text-emerald-700 border-emerald-200" :
    entitlement.status === "PAUSED" ? "bg-amber-50 text-amber-700 border-amber-200" :
    "bg-rose-50 text-rose-700 border-rose-200";

  return (
    <div 
      onClick={onClick}
      className={cn(
        "group flex flex-col bg-white border border-[#e5e5e5] rounded-xl overflow-hidden shadow-sm transition-all duration-200",
        entitlement.status !== "EXPIRED" ? "cursor-pointer hover:shadow-md hover:border-[#a1a1aa]" : "opacity-75 cursor-not-allowed grayscale-[30%]"
      )}
    >
      {/* Top Half: Tenant Context */}
      <div className="px-5 py-4 border-b border-[#f4f4f5] bg-[#fafafa]/50 flex items-center justify-between">
        <div className="flex items-center gap-2.5">
          <div className="flex h-7 w-7 items-center justify-center rounded-[6px] bg-white border border-[#e5e5e5] text-[10px] font-bold text-[#09090b] shadow-sm">
            {entitlement.tenantLogoInitials}
          </div>
          <span className="text-[12px] font-semibold tracking-tight text-[#52525b] uppercase">{entitlement.tenantName}</span>
        </div>
        <ExternalLink size={14} className="text-[#a1a1aa] group-hover:text-[#09090b] transition-colors" />
      </div>

      {/* Bottom Half: Product Context */}
      <div className="p-5 flex-1 flex flex-col justify-between">
        <div>
          <div className="flex items-center gap-2 mb-3">
            <span className="inline-flex items-center gap-1 px-2 py-0.5 rounded-[4px] bg-[#f4f4f5] border border-[#e5e5e5] text-[10px] font-bold uppercase tracking-widest text-[#52525b]">
              <TypeIcon size={10} /> {entitlement.appType}
            </span>
            <span className={cn("inline-flex items-center px-2 py-0.5 rounded-[4px] border text-[10px] font-bold uppercase tracking-widest", statusColor)}>
              {entitlement.status}
            </span>
          </div>
          
          <h2 className="text-[16px] font-semibold text-[#09090b] leading-tight mb-2 group-hover:text-blue-600 transition-colors">
            {entitlement.productName}
          </h2>
        </div>
        
        <div className="mt-6">
          <button 
            className={cn(
              "w-full h-9 rounded-md text-[13px] font-semibold transition-colors",
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
