import { useState } from "react";
import { Menu, Plus, Loader2, Settings, Building2, SearchX } from "lucide-react";
import { toast } from "sonner";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { client } from "../lib/api-client";
import { cn } from "../lib/utils";

import CreateTenantModal from "./CreateTenantModal";
import TenantDetailsSlideout from "./TenantDetailsSlideout";

interface TenantsProps {
  isMobile?: boolean;
  toggleSidebar?: () => void;
}

export default function Tenants({ isMobile, toggleSidebar }: TenantsProps) {
  const queryClient = useQueryClient();
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [selectedTenant, setSelectedTenant] = useState<any | null>(null);

  const { data: workspaces, isLoading } = useQuery({
    queryKey: ["workspaces"],
    queryFn: async () => {
      const { data, error } = await client.GET("/one/workspaces");
      if (error) throw new Error(error.detail || "Failed to fetch workspaces");
      return data ?? [];
    }
  });

  const createMutation = useMutation({
    mutationFn: async (payload: { name: string; slug: string }) => {
      const { data, error } = await client.POST("/one/workspaces", { body: payload });
      if (error) throw new Error(error.detail || "Provisioning failed");
      return data;
    },
    onSuccess: () => {
      toast.success("Workspace provisioned and seeded successfully!");
      queryClient.invalidateQueries({ queryKey: ["workspaces"] });
      setShowCreateModal(false);
    },
    onError: (err: any) => {
      toast.error(err.message);
    }
  });

  const formatDate = (isoString: string) => {
    return new Date(isoString).toLocaleDateString("en-MY", { year: 'numeric', month: 'short', day: 'numeric' });
  };

  return (
    <div className="flex-1 w-full p-4 md:p-8 mx-auto max-w-[1240px] flex flex-col gap-6 animate-in fade-in duration-300">
      
      <header className="flex flex-col md:flex-row md:items-center justify-between pb-2 gap-4">
        <div className="flex items-center gap-3">
          {isMobile && (
            <button onClick={toggleSidebar} className="p-1.5 -ml-1.5 rounded-md text-[#71717a] hover:bg-[#e5e5e5] hover:text-[#09090b] focus:outline-none transition-colors">
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
            <Plus size={16} /> New Workspace
          </button>
        </div>
      </header>

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
              {isLoading ? (
                <tr><td colSpan={4} className="py-12 text-center text-[#71717a] text-[11px] font-bold uppercase tracking-widest"><Loader2 size={16} className="animate-spin mx-auto mb-2"/> Loading...</td></tr>
              ) : !workspaces || workspaces.length === 0 ? (
                <tr><td colSpan={4} className="py-12 text-center"><SearchX className="h-8 w-8 text-[#a1a1aa] mx-auto mb-3" /><p className="text-[13px] font-bold uppercase tracking-widest text-[#09090b]">No workspaces found</p></td></tr>
              ) : (
                workspaces.map((tenant) => (
                  <tr key={tenant.id} className="hover:bg-[#fafafa]/50 transition-colors group">
                    <td className="p-5 whitespace-nowrap">
                      <p className="font-semibold text-[#09090b] text-[14px]">{tenant.name}</p>
                      <p className="text-[11px] font-mono text-[#71717a] mt-0.5">{tenant.slug}.lazuar.com</p>
                    </td>
                    <td className="p-5 whitespace-nowrap">
                      {tenant.is_active ? (
                        <span className="inline-flex items-center px-2 py-0.5 rounded-none border border-emerald-200 bg-emerald-50 text-[10px] font-bold uppercase tracking-widest text-emerald-700">Active</span>
                      ) : (
                        <span className="inline-flex items-center px-2 py-0.5 rounded-none border border-[#e5e5e5] bg-[#f4f4f5] text-[10px] font-bold uppercase tracking-widest text-[#71717a]">Inactive</span>
                      )}
                    </td>
                    <td className="p-5 whitespace-nowrap text-[#52525b] font-mono text-[12px]">{formatDate(tenant.created_at)}</td>
                    <td className="p-5 whitespace-nowrap text-right">
                      <button onClick={() => setSelectedTenant(tenant)} className="inline-flex items-center gap-1.5 h-8 px-3 rounded-none border border-[#e5e5e5] bg-white text-[#09090b] text-[11px] font-bold uppercase tracking-widest hover:bg-[#f4f4f5] hover:border-[#a1a1aa] transition-colors focus:outline-none">
                        <Settings size={13} /> Manage
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {showCreateModal && (
        <CreateTenantModal 
          onClose={() => setShowCreateModal(false)}
          onSuccess={(name, slug) => createMutation.mutate({ name, slug })}
        />
      )}

      {selectedTenant && (
        <TenantDetailsSlideout tenant={selectedTenant} onClose={() => setSelectedTenant(null)} />
      )}
    </div>
  );
}
