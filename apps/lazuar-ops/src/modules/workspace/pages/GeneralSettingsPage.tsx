import { useState, useEffect } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Loader2, AlertTriangle, Save } from "lucide-react";
import { toast } from "sonner";
import { client, type EntitlementDto } from "../../../lib/api-client";
import { useOutletContext } from "react-router-dom";
import PageLayout from "../../core/components/PageLayout";

export default function GeneralSettingsPage() {
  const { activeWorkspaceId } = useOutletContext<{ activeWorkspaceId: string }>();
  const queryClient = useQueryClient();
  
  const [name, setName] = useState("");
  const [slug, setSlug] = useState("");
  const [originalSlug, setOriginalSlug] = useState("");

  const { data: entitlements, isLoading } = useQuery({
    queryKey: ["entitlements"],
    queryFn: async () => {
      const { data } = await client.GET("/one/me/entitlements");
      return data as EntitlementDto[];
    }
  });

  useEffect(() => {
    if (entitlements && activeWorkspaceId) {
      const activeWorkspace = entitlements.find(e => e.workspace_id === activeWorkspaceId);
      if (activeWorkspace) {
        setName(activeWorkspace.workspace_name);
        setSlug(activeWorkspace.workspace_slug);
        setOriginalSlug(activeWorkspace.workspace_slug);
      }
    }
  }, [entitlements, activeWorkspaceId]);

  const updateMutation = useMutation({
    mutationFn: async () => {
      const { error } = await client.PUT("/one/workspaces/{id}", {
        params: { path: { id: activeWorkspaceId } },
        body: { name: name.trim(), slug: slug.trim() }
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("Workspace settings updated successfully.");
      setOriginalSlug(slug);
      queryClient.invalidateQueries({ queryKey: ["entitlements"] });
    },
    onError: (err: any) => toast.error(err.message || "Failed to update workspace.")
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (slug !== originalSlug) {
      if (!window.confirm("WARNING: Changing your slug will break all existing public links. Are you absolutely sure you want to proceed?")) {
        return;
      }
    }
    updateMutation.mutate();
  };

  const hasChanges = name !== entitlements?.find(e => e.workspace_id === activeWorkspaceId)?.workspace_name || slug !== originalSlug;

  return (
    <PageLayout
      title="General Settings"
      description="Manage your workspace identity and public URL mapping."
      breadcrumbs={[{ label: "Workspace" }, { label: "General Settings" }]}
    >
      <div className="max-w-2xl bg-white border border-[#e5e5e5] rounded-none flex flex-col">
        {isLoading ? (
          <div className="p-12 flex justify-center"><Loader2 className="animate-spin text-[#a1a1aa]" /></div>
        ) : (
          <form onSubmit={handleSubmit} className="flex flex-col">
            <div className="p-6 md:p-8 space-y-8">
              
              <div className="space-y-4">
                <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">Workspace Identity</label>
                <div className="space-y-1.5">
                  <label className="text-[11px] font-semibold text-[#09090b]">Workspace Name</label>
                  <input type="text" required value={name} onChange={(e) => setName(e.target.value)} disabled={updateMutation.isPending} className="w-full h-10 border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50" />
                </div>
              </div>

              <div className="space-y-4">
                <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5 text-rose-600">Danger Zone</label>
                
                <div className="bg-rose-50 border border-rose-200 p-4 space-y-3">
                  <div className="flex items-start gap-2 text-rose-800">
                    <AlertTriangle size={16} className="mt-0.5 shrink-0 text-rose-600" />
                    <div>
                      <h4 className="text-[12px] font-bold uppercase tracking-widest">Public URL Slug</h4>
                      <p className="text-[12px] mt-1 text-rose-700 leading-relaxed">
                        Changing your workspace slug will instantly break all existing public checkout links, magic links, and portal URLs. Only change this if you are rebranding.
                      </p>
                    </div>
                  </div>
                  <div className="pt-2">
                    <input type="text" required value={slug} onChange={(e) => setSlug(e.target.value.toLowerCase())} disabled={updateMutation.isPending} className="w-full h-10 border border-rose-200 bg-white px-3 font-mono text-[13px] focus:outline-none focus:border-rose-400 disabled:opacity-50 text-rose-900" />
                  </div>
                </div>
              </div>

            </div>

            <div className="flex items-center justify-end p-5 border-t border-[#f4f4f5] bg-[#fafafa]/50">
              <button type="submit" disabled={!hasChanges || updateMutation.isPending} className="h-10 px-8 bg-[#09090b] text-white text-[11px] font-bold tracking-widest uppercase rounded-none hover:bg-[#27272a] disabled:opacity-50 transition-colors flex items-center gap-2">
                {updateMutation.isPending ? <Loader2 size={13} className="animate-spin" /> : <Save size={13} />} Save Changes
              </button>
            </div>
          </form>
        )}
      </div>
    </PageLayout>
  );
}
