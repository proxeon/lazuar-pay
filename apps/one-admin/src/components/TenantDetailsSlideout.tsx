import { X, CheckCircle2, Server, Users, Loader2, Database } from "lucide-react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { client } from "../lib/api-client";
import type { MockTenant } from "./Tenants";
import { cn } from "../lib/utils";

interface TenantDetailsSlideoutProps {
  tenant: MockTenant | null;
  onClose: () => void;
}

const AVAILABLE_APPS = [
  { id: "COMMUNITY", title: "Community", desc: "Subscription engine & CRM linked.", icon: Users },
  { id: "VAULT", title: "Storage Vault", desc: "S3 buckets & policies provisioned.", icon: Server },
  { id: "FUNNEL", title: "Funnel Builder", desc: "Landing pages & lead generation.", icon: Database }
];

export default function TenantDetailsSlideout({ tenant, onClose }: TenantDetailsSlideoutProps) {
  const queryClient = useQueryClient();

  // Fetch Active Apps for this Tenant
  const { data: activeApps, isLoading } = useQuery({
    queryKey: ["workspace-apps", tenant?.id],
    queryFn: async () => {
      const { data, error } = await client.GET("/one/workspaces/{id}/apps", {
        params: { path: { id: tenant!.id } }
      });
      if (error) throw new Error(error.detail || "Failed to load apps");
      return data ?? [];
    },
    enabled: !!tenant
  });

  // Toggle App Entitlement Mutation
  const toggleMutation = useMutation({
    mutationFn: async ({ appId, isActive }: { appId: string; isActive: boolean }) => {
      const { data, error } = await client.POST("/one/workspaces/{id}/apps/{appId}", {
        params: { path: { id: tenant!.id, appId } },
        body: { is_active: isActive }
      });
      if (error) throw new Error(error.detail || "Failed to update entitlement");
      return data;
    },
    onSuccess: (_, variables) => {
      toast.success(`${variables.appId} access ${variables.isActive ? "granted" : "revoked"}.`);
      queryClient.invalidateQueries({ queryKey: ["workspace-apps", tenant?.id] });
    },
    onError: (err: any) => toast.error(err.message)
  });

  if (!tenant) return null;

  return (
    <div className="fixed inset-0 z-50 overflow-hidden flex justify-end">
      <div className="absolute inset-0 bg-black/10 backdrop-blur-[2px] transition-opacity animate-in fade-in duration-200" onClick={onClose} />
      
      <div className="relative w-full max-w-sm bg-white h-full border-l border-[#e5e5e5] shadow-2xl flex flex-col animate-in slide-in-from-right duration-300">
        
        <div className="flex items-start justify-between p-6 border-b border-[#f4f4f5] shrink-0 bg-[#fafafa]/50">
          <div>
            <h2 className="text-[18px] font-semibold text-[#09090b] leading-tight">{tenant.name}</h2>
            <div className="flex items-center gap-2 mt-2">
              <span className="text-[11px] font-mono text-[#71717a] bg-[#f4f4f5] px-1.5 py-0.5 border border-[#e5e5e5]">{tenant.slug}</span>
              {tenant.is_active && (
                <span className="inline-flex items-center gap-1 text-[10px] font-bold uppercase tracking-widest text-emerald-600">
                  <span className="w-1.5 h-1.5 rounded-full bg-emerald-500" /> Active
                </span>
              )}
            </div>
          </div>
          <button onClick={onClose} className="text-[#a1a1aa] hover:bg-[#e5e5e5] hover:text-[#09090b] transition-colors p-1.5 rounded-none"><X size={16} /></button>
        </div>

        <div className="flex-1 overflow-y-auto p-6 space-y-8">
          <section className="space-y-3">
            <h3 className="text-[10px] font-bold uppercase tracking-widest text-[#a1a1aa] mb-3 border-b border-[#f4f4f5] pb-1">Metadata</h3>
            <div className="flex justify-between items-center text-[13px]">
              <span className="text-[#71717a]">Tenant ID</span>
              <span className="font-mono text-[#09090b] truncate max-w-[200px]" title={tenant.id}>{tenant.id}</span>
            </div>
            <div className="flex justify-between items-center text-[13px]">
              <span className="text-[#71717a]">Created At</span>
              <span className="font-mono text-[#09090b]">{new Date(tenant.created_at).toLocaleDateString("en-MY")}</span>
            </div>
          </section>

          <section>
            <h3 className="text-[10px] font-bold uppercase tracking-widest text-[#a1a1aa] mb-4 border-b border-[#f4f4f5] pb-1">App Entitlements</h3>
            
            <div className="space-y-3">
              {isLoading ? (
                <div className="flex flex-col items-center justify-center py-6 text-[#71717a]">
                  <Loader2 size={16} className="animate-spin mb-2" />
                  <span className="text-[10px] uppercase tracking-widest font-bold">Loading Entitlements...</span>
                </div>
              ) : (
                AVAILABLE_APPS.map(app => {
                  const isActive = activeApps?.some(a => a.app_id === app.id);
                  const isUpdating = toggleMutation.isPending && toggleMutation.variables?.appId === app.id;

                  return (
                    <div key={app.id} className={cn("flex items-start gap-3 p-3 border transition-colors", isActive ? "border-emerald-200 bg-emerald-50/30" : "border-[#e5e5e5] bg-white")}>
                      <div className={cn("p-1.5 border", isActive ? "bg-emerald-50 text-emerald-600 border-emerald-100" : "bg-[#f4f4f5] text-[#a1a1aa] border-[#e5e5e5]")}>
                        <app.icon size={14} />
                      </div>
                      <div className="flex-1">
                        <div className="flex items-center justify-between">
                          <h4 className={cn("text-[12px] font-semibold", isActive ? "text-[#09090b]" : "text-[#71717a]")}>{app.title}</h4>
                          <button
                            disabled={isUpdating}
                            onClick={() => toggleMutation.mutate({ appId: app.id, isActive: !isActive })}
                            className={cn("relative inline-flex h-5 w-9 shrink-0 cursor-pointer items-center rounded-none border-2 border-transparent transition-colors focus:outline-none focus:ring-2 focus:ring-[#09090b] disabled:opacity-50", isActive ? "bg-[#09090b]" : "bg-[#e5e5e5]")}
                          >
                            <span className={cn("pointer-events-none inline-block h-4 w-4 transform rounded-none bg-white shadow-sm transition duration-200", isActive ? "translate-x-4" : "translate-x-0")} />
                          </button>
                        </div>
                        <p className="text-[11px] text-[#71717a] mt-0.5 leading-snug">{app.desc}</p>
                      </div>
                    </div>
                  );
                })
              )}
            </div>
          </section>
        </div>
      </div>
    </div>
  );
}
