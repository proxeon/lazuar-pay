import { ExternalLink, Layers } from "lucide-react";
import { toast } from "sonner";
import { cn } from "../lib/utils";

// --- TYPES ---
type AppCategory = "ACQUISITION" | "FULFILLMENT" | "RETENTION";
type AppSubModule = "FUNNEL" | "EVENT" | "CONSULT" | "VAULT" | "ACADEMY" | "COMMUNITY" | "BROADCAST" | "AFFILIATE";
type EntitlementStatus = "ACTIVE" | "PAUSED" | "EXPIRED";

interface EcosystemApp {
  id: string;
  name: string;
  subModule: AppSubModule;
  category: AppCategory;
  status: EntitlementStatus;
  devPort: number;
  productionSubdomain: string;
  description: string;
  path?: string;
}

// --- MODULE DEFINITIONS ---
const ecosystemApps: EcosystemApp[] = [
  {
    id: "app_funnel",
    name: "Funnel Engine",
    subModule: "FUNNEL",
    category: "ACQUISITION",
    status: "ACTIVE",
    devPort: 3015,
    productionSubdomain: "funnel",
    description: "Build conversion engines and zero-friction customer pathways."
  },
  {
    id: "app_event",
    name: "Event Portal",
    subModule: "EVENT",
    category: "ACQUISITION",
    status: "ACTIVE",
    devPort: 3008,
    productionSubdomain: "event",
    description: "Organize live masterminds, physical summits, and digital webinars."
  },
  {
    id: "app_consult",
    name: "Consult System",
    subModule: "CONSULT",
    category: "ACQUISITION",
    status: "ACTIVE",
    devPort: 3022,
    productionSubdomain: "consult",
    description: "Direct scheduler and booking workflows for customized consulting sessions."
  },
  {
    id: "app_vault",
    name: "Resource Vault",
    subModule: "VAULT",
    category: "FULFILLMENT",
    status: "ACTIVE",
    devPort: 3012,
    productionSubdomain: "vault",
    description: "Sovereign infrastructure for organizing, securing, and downloading digital assets."
  },
  {
    id: "app_academy",
    name: "Academy Platform",
    subModule: "ACADEMY",
    category: "FULFILLMENT",
    status: "ACTIVE",
    devPort: 3012,
    productionSubdomain: "vault",
    path: "/academy",
    description: "Structured educational modules and training resources for full-scale learning."
  },
  {
    id: "app_community",
    name: "Member Community",
    subModule: "COMMUNITY",
    category: "RETENTION",
    status: "ACTIVE",
    devPort: 3020,
    productionSubdomain: "community",
    description: "Interactive forums, social learning structures, and group management."
  },
  {
    id: "app_broadcast",
    name: "Broadcast Center",
    subModule: "BROADCAST",
    category: "RETENTION",
    status: "PAUSED",
    devPort: 3010,
    productionSubdomain: "broadcast",
    description: "Distribute news, updates, and targeted newsletters to active databases."
  },
  {
    id: "app_affiliate",
    name: "Affiliate Engine",
    subModule: "AFFILIATE",
    category: "RETENTION",
    status: "EXPIRED",
    devPort: 3010,
    productionSubdomain: "affiliate",
    description: "Enable viral distribution loops and track referral statistics."
  }
];

export default function Launchpad() {
  const getAppUrl = (app: EcosystemApp) => {
    const isLocalhost = 
      window.location.hostname === "localhost" || 
      window.location.hostname === "127.0.0.1";
    
    const pathSuffix = app.path || "";
    
    if (isLocalhost) {
      return `http://localhost:${app.devPort}${pathSuffix}`;
    }
    return `https://${app.productionSubdomain}.lazuar.com${pathSuffix}`;
  };

  const handleAccessClick = (app: EcosystemApp) => {
    if (app.status === "EXPIRED") {
      toast.error(`Access to ${app.name} has expired. Please renew your subscription.`);
      return;
    }
    if (app.status === "PAUSED") {
      toast.warning(`${app.name} is currently suspended or under maintenance.`);
      return;
    }

    const resolvedUrl = getAppUrl(app);

    toast.success(`Authenticating session...`, {
      description: `Connecting securely to ${new URL(resolvedUrl).host}...`,
      duration: 3000,
    });

    setTimeout(() => {
      window.open(resolvedUrl, "_blank", "noopener,noreferrer");
    }, 1200);
  };

  return (
    <div className="flex-1 w-full p-4 md:p-8 mx-auto max-w-[1400px] flex flex-col gap-6 animate-in fade-in duration-300">
      
      <header className="flex flex-col md:flex-row md:items-end justify-between pb-2 gap-4 border-b border-[#e5e5e5]">
        <div>
          <h1 className="text-[20px] font-semibold tracking-tight text-[#09090b]">My Ecosystem</h1>
          <p className="text-[13px] text-[#71717a] mt-1 font-mono uppercase tracking-wider">A unified view of everything you own.</p>
        </div>
      </header>

      {ecosystemApps.length === 0 ? (
        <div className="flex flex-col items-center justify-center py-24 text-center border border-dashed border-[#e5e5e5] rounded-none bg-white/50">
          <Layers className="h-10 w-10 text-[#a1a1aa] mb-4" />
          <h3 className="text-[15px] font-bold uppercase tracking-widest text-[#09090b]">No modules found</h3>
          <p className="text-[13px] text-[#71717a] mt-1 max-w-sm">No operational modules configured for this view block.</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6">
          {ecosystemApps.map((app) => (
            <AccessCard 
              key={app.id} 
              app={app} 
              onClick={() => handleAccessClick(app)} 
            />
          ))}
        </div>
      )}
    </div>
  );
}

function AccessCard({ app, onClick }: { app: EcosystemApp; onClick: () => void }) {
  return (
    <div 
      onClick={onClick}
      className={cn(
        "group flex flex-col aspect-square bg-white border border-[#e5e5e5] rounded-none shadow-sm hover:shadow-[4px_4px_0px_0px_rgba(0,0,0,0.05)] transition-all duration-200",
        app.status === "ACTIVE" 
          ? "cursor-pointer hover:border-[#a1a1aa] hover:-translate-y-1" 
          : "opacity-70 cursor-not-allowed grayscale-[20%]"
      )}
    >
      <div className="px-5 py-4 border-b border-[#f4f4f5] bg-[#fafafa]/50 flex items-center justify-between shrink-0">
        <span className="text-[11px] font-bold tracking-widest text-[#52525b] uppercase truncate">
          {app.category}
        </span>
        <ExternalLink size={14} className="text-[#a1a1aa] group-hover:text-[#09090b] transition-colors shrink-0 ml-2" />
      </div>

      <div className="p-5 flex-1 flex flex-col justify-between">
        <div>
          <h2 className="text-[18px] font-semibold text-[#09090b] leading-snug group-hover:text-blue-600 transition-colors line-clamp-2">
            {app.name}
          </h2>
          <p className="text-[12px] text-[#71717a] mt-2 line-clamp-3 leading-relaxed">
            {app.description}
          </p>
        </div>
        
        <div className="mt-auto pt-4 shrink-0">
          <button 
            type="button"
            className={cn(
              "w-full h-10 rounded-none text-[11px] uppercase tracking-widest font-bold transition-colors shadow-sm pointer-events-none",
              app.status === "ACTIVE" 
                ? "bg-[#09090b] text-white group-hover:bg-[#27272a]" 
                : "bg-[#f4f4f5] text-[#a1a1aa] border border-[#e5e5e5]"
            )}
          >
            {app.status === "ACTIVE" ? "Launch Platform" : app.status === "PAUSED" ? "Under Maintenance" : "Renew Access"}
          </button>
        </div>
      </div>
    </div>
  );
}
