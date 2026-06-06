import { useState } from "react";
import { Menu, Plus, Loader2, Settings, Building2, SearchX } from "lucide-react";
import { toast } from "sonner";
import { cn } from "../lib/utils";

import CreateTenantModal from "./CreateTenantModal";
import TenantDetailsSlideout from "./TenantDetailsSlideout";

// --- TYPES ---
export type ProvisioningStatus = 'COMPLETE' | 'PROVISIONING';

export interface MockTenant {
  id: string;
  name: string;
  slug: string;
  isActive: boolean;
  createdAt: string;
  provisioningStatus: ProvisioningStatus;
}

interface TenantsProps {
  isMobile?: boolean;
  toggleSidebar?: () => void;
}

export default function Tenants({ isMobile, toggleSidebar }: TenantsProps) {
  // --- STATE ---
  const [tenants, setTenants] = useState<MockTenant[]>([
    {
      id: "t_1",
      name: "Lazuar HQ",
      slug: "lazuar-hq",
      isActive: true,
      createdAt: "2024-01-15T08:30:00Z",
      provisioningStatus: "COMPLETE",
    },
    {
      id: "t_2",
      name: "Premium Studio",
      slug: "premium-studio",
      isActive: true,
      createdAt: "2024-02-20T14:45:00Z",
      provisioningStatus: "COMPLETE",
    }
  ]);

  // Modal & Drawer State
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [selectedTenant, setSelectedTenant] = useState<MockTenant | null>(null);

  // --- ASYNC SIMULATION LOGIC ---
  const handleTenantCreated = (name: string, slug: string) => {
    const newTenant: MockTenant = {
      id: `t_${Date.now()}`,
      name: name,
      slug: slug,
      isActive: false, // Initially false until provisioned
      createdAt: new Date().toISOString(),
      provisioningStatus: "PROVISIONING",
    };

    // Step 1: Append to state as 'PROVISIONING'
    setTenants((prev) => [newTenant, ...prev]);
    setShowCreateModal(false);

    // Step 2: Fire info toast
    toast.info("Provisioning workspace & seeding cognitive core...");

    // Step 3: Simulate backend event bus delay
    setTimeout(() => {
      // Step 4: Update state to 'COMPLETE' and 'ACTIVE'
      setTenants((prev) =>
        prev.map((t) =>
          t.id === newTenant.id
            ? { ...t, provisioningStatus: "COMPLETE", isActive: true }
            : t
        )
      );

      // Step 5: Fire success toast
      toast.success("Workspace provisioned successfully!");
    }, 3000);
  };

  // --- UTILS ---
  const formatDate = (isoString: string) => {
    return new Date(isoString).toLocaleDateString("en-MY", {
      year: 'numeric',
      month: 'short',
      day: 'numeric'
    });
  };

  return (
    <div className="flex-1 w-full p-4 md:p-8 mx-auto max-w-[1240px] flex flex-col gap-6 animate-in fade-in duration-300">
      
      {/* Header & Action Bar */}
      <header className="flex flex-col md:flex-row md:items-center justify-between pb-2 gap-4">
        <div className="flex items-center gap-3">
          {isMobile && (
            <button 
              onClick={toggleSidebar}
              className="p-1.5 -ml-1.5 rounded-md text-[#71717a] hover:bg-[#e5e5e5] hover:text-[#09090b] focus:outline-none transition-colors"
            >
              <Menu size={20} />
            </button>
          )}
          <div>
            <h1 className="text-[20px] font-semibold tracking-tight text-[#09090b]">Workspaces</h1>
            <p className="text-[13px] text-[#71717a] mt-0.5">Manage ecosystem tenants and provisioning.</p>
          </div>
        </div>

        <div className="flex items-center gap-2">
          <button 
            onClick={() => setShowCreateModal(true)}
            className="flex items-center gap-1.5 bg-[#09090b] text-white text-[13px] font-semibold px-4 h-9 rounded-none shadow-[4px_4px_0px_0px_rgba(0,0,0,0.1)] hover:shadow-none hover:translate-y-[2px] hover:translate-x-[2px] hover:bg-[#27272a] transition-all active:scale-95"
          >
            <Plus size={16} />
            New Workspace
          </button>
        </div>
      </header>

      {/* Data Table */}
      <div className="bg-white border border-[#e5e5e5] rounded-none shadow-[4px_4px_0px_0px_rgba(0,0,0,0.05)] overflow-hidden flex flex-col">
        <div className="px-5 py-4 border-b border-[#f4f4f5] bg-[#fafafa]/50 flex items-center gap-2">
          <Building2 size={16} className="text-[#a1a1aa]" />
          <h2 className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">Tenant Directory</h2>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full text-left text-[13px]">
            <thead>
              <tr className="border-b border-[#e5e5e5] bg-[#fafafa]">
                <th className="h-10 px-5 font-bold uppercase tracking-widest text-[#71717a] text-[10px]">Workspace Name</th>
                <th className="h-10 px-5 font-bold uppercase tracking-widest text-[#71717a] text-[10px]">Status</th>
                <th className="h-10 px-5 font-bold uppercase tracking-widest text-[#71717a] text-[10px]">Created Date</th>
                <th className="h-10 px-5 font-bold uppercase tracking-widest text-[#71717a] text-[10px] text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#f4f4f5]">
              {tenants.length === 0 ? (
                <tr>
                  <td colSpan={4} className="py-12 text-center">
                    <SearchX className="h-8 w-8 text-[#a1a1aa] mx-auto mb-3" />
                    <p className="text-[13px] font-bold uppercase tracking-widest text-[#09090b]">No workspaces found</p>
                  </td>
                </tr>
              ) : (
                tenants.map((tenant) => (
                  <tr key={tenant.id} className="hover:bg-[#fafafa]/50 transition-colors group">
                    
                    {/* Name & Slug */}
                    <td className="p-5 whitespace-nowrap">
                      <p className="font-semibold text-[#09090b] text-[14px]">{tenant.name}</p>
                      <p className="text-[11px] font-mono text-[#71717a] mt-0.5">{tenant.slug}</p>
                    </td>

                    {/* Status Badge */}
                    <td className="p-5 whitespace-nowrap">
                      {tenant.provisioningStatus === 'PROVISIONING' ? (
                        <span className="inline-flex items-center gap-1.5 px-2 py-0.5 rounded-none border border-amber-200 bg-amber-50 text-[10px] font-bold uppercase tracking-widest text-amber-700 animate-pulse">
                          <Loader2 size={12} className="animate-spin" />
                          Provisioning
                        </span>
                      ) : tenant.isActive ? (
                        <span className="inline-flex items-center px-2 py-0.5 rounded-none border border-emerald-200 bg-emerald-50 text-[10px] font-bold uppercase tracking-widest text-emerald-700">
                          Active
                        </span>
                      ) : (
                        <span className="inline-flex items-center px-2 py-0.5 rounded-none border border-[#e5e5e5] bg-[#f4f4f5] text-[10px] font-bold uppercase tracking-widest text-[#71717a]">
                          Inactive
                        </span>
                      )}
                    </td>

                    {/* Created Date */}
                    <td className="p-5 whitespace-nowrap text-[#52525b] font-mono text-[12px]">
                      {formatDate(tenant.createdAt)}
                    </td>

                    {/* Actions */}
                    <td className="p-5 whitespace-nowrap text-right">
                      <button 
                        onClick={() => setSelectedTenant(tenant)}
                        className={cn(
                          "inline-flex items-center gap-1.5 h-8 px-3 rounded-none border text-[11px] font-bold uppercase tracking-widest transition-colors focus:outline-none",
                          tenant.provisioningStatus === 'PROVISIONING' 
                            ? "border-[#e5e5e5] bg-[#fafafa] text-[#a1a1aa] cursor-wait" 
                            : "border-[#e5e5e5] bg-white text-[#09090b] hover:bg-[#f4f4f5] hover:border-[#a1a1aa]"
                        )}
                      >
                        <Settings size={13} />
                        {tenant.provisioningStatus === 'PROVISIONING' ? "Waiting..." : "Manage"}
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* --- MODALS --- */}
      {showCreateModal && (
        <CreateTenantModal 
          onClose={() => setShowCreateModal(false)}
          onSuccess={handleTenantCreated}
        />
      )}

      {selectedTenant && (
        <TenantDetailsSlideout 
          tenant={selectedTenant}
          onClose={() => setSelectedTenant(null)}
        />
      )}

    </div>
  );
}
