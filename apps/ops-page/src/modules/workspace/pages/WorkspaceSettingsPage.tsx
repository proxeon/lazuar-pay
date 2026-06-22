import { useState, useEffect } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2, UserPlus, X, Trash2, ShieldAlert } from "lucide-react";
import { toast } from "sonner";
import { useOutletContext } from "react-router-dom";
import { client } from "../../../lib/api-client";
import { cn } from "../../../lib/utils";
import PageLayout from "../../core/components/PageLayout";

const AVAILABLE_APPS = [
  { id: "COMMUNITY", name: "Community & Subscriptions", description: "Membership plans and billing." },
  { id: "OPS", name: "Ops & AI Agent", description: "Internal orchestration and tools." },
  { id: "BILLING", name: "Billing & Ledgers", description: "Double-entry financial truths." },
  { id: "PAYMENTS", name: "Payments Gateway", description: "Stripe and Billplz adapters." },
  { id: "CRM", name: "CRM & Profiles", description: "Customer identity directories." },
  { id: "LHDN", name: "LHDN e-Invoicing", description: "Malaysian tax compliance." }
];

export default function WorkspaceSettingsPage() {
  const { activeWorkspaceId } = useOutletContext<{ activeWorkspaceId: string | null }>();
  const queryClient = useQueryClient();
  const [activeTab, setActiveTab] = useState<"general" | "members" | "apps">("general");

  const [name, setName] = useState("");
  const [slug, setSlug] = useState("");
  const [inviteEmail, setInviteEmail] = useState("");
  const [inviteRole, setInviteRole] = useState("ADMIN");

  const { data: workspaces, isLoading: isWorkspaceLoading } = useQuery({
    queryKey: ["workspaces"],
    queryFn: async () => {
      const { data, error } = await client.GET("/one/workspaces");
      if (error) throw new Error(error.detail);
      return data;
    },
    enabled: !!activeWorkspaceId
  });

  useEffect(() => {
    if (workspaces && activeWorkspaceId) {
      const current = workspaces.find((w: any) => w.id === activeWorkspaceId);
      if (current) {
        setName(current.name);
        setSlug(current.slug);
      }
    }
  }, [workspaces, activeWorkspaceId]);

  const { data: members, isLoading: isMembersLoading } = useQuery({
    queryKey: ["workspace-members", activeWorkspaceId],
    queryFn: async () => {
      const { data, error } = await client.GET("/one/workspaces/{id}/members", { params: { path: { id: activeWorkspaceId! } } });
      if (error) throw new Error(error.detail);
      return data;
    },
    enabled: activeTab === "members" && !!activeWorkspaceId
  });

  const { data: invites, isLoading: isInvitesLoading } = useQuery({
    queryKey: ["workspace-invites", activeWorkspaceId],
    queryFn: async () => {
      const { data, error } = await client.GET("/one/workspaces/{id}/invites", { params: { path: { id: activeWorkspaceId! } } });
      if (error) throw new Error(error.detail);
      return data;
    },
    enabled: activeTab === "members" && !!activeWorkspaceId
  });

  const { data: apps, isLoading: isAppsLoading } = useQuery({
    queryKey: ["workspace-apps", activeWorkspaceId],
    queryFn: async () => {
      const { data, error } = await client.GET("/one/workspaces/{id}/apps", { params: { path: { id: activeWorkspaceId! } } });
      if (error) throw new Error(error.detail);
      return data.map((a: any) => a.app_id);
    },
    enabled: activeTab === "apps" && !!activeWorkspaceId
  });

  const updateWorkspaceMutation = useMutation({
    mutationFn: async () => {
      const { error } = await client.PUT("/one/workspaces/{id}", {
        params: { path: { id: activeWorkspaceId! } },
        body: { name, slug }
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("Workspace updated successfully.");
      queryClient.invalidateQueries({ queryKey: ["workspaces"] });
      queryClient.invalidateQueries({ queryKey: ["entitlements"] });
    },
    onError: (err: any) => toast.error("Failed to update workspace", { description: err.message })
  });

  const inviteMutation = useMutation({
    mutationFn: async () => {
      const { error } = await client.POST("/one/workspaces/{id}/invites", {
        params: { path: { id: activeWorkspaceId! } },
        body: { email: inviteEmail, role: inviteRole }
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("Invitation sent.");
      setInviteEmail("");
      queryClient.invalidateQueries({ queryKey: ["workspace-invites", activeWorkspaceId] });
    },
    onError: (err: any) => toast.error("Failed to send invitation", { description: err.message })
  });

  const revokeInviteMutation = useMutation({
    mutationFn: async (inviteId: string) => {
      const { error } = await client.DELETE("/one/workspaces/{id}/invites/{inviteId}", {
        params: { path: { id: activeWorkspaceId!, inviteId } }
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("Invitation revoked.");
      queryClient.invalidateQueries({ queryKey: ["workspace-invites", activeWorkspaceId] });
    },
    onError: (err: any) => toast.error("Failed to revoke invitation", { description: err.message })
  });

  const removeMemberMutation = useMutation({
    mutationFn: async (userId: string) => {
      const { error } = await client.DELETE("/one/workspaces/{id}/members/{userId}", {
        params: { path: { id: activeWorkspaceId!, userId } }
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("Member removed.");
      queryClient.invalidateQueries({ queryKey: ["workspace-members", activeWorkspaceId] });
    },
    onError: (err: any) => toast.error("Failed to remove member", { description: err.message })
  });

  const toggleAppMutation = useMutation({
    mutationFn: async ({ appId, isActive }: { appId: string, isActive: boolean }) => {
      const { error } = await client.POST("/one/workspaces/{id}/apps/{appId}", {
        params: { path: { id: activeWorkspaceId!, appId } },
        body: { is_active: isActive }
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("App entitlement updated.");
      queryClient.invalidateQueries({ queryKey: ["workspace-apps", activeWorkspaceId] });
    },
    onError: (err: any) => toast.error("Failed to update entitlement", { description: err.message })
  });

  if (!activeWorkspaceId) return null;

  return (
    <PageLayout 
      title="Workspace Settings" 
      description="Manage your organization's core settings, team members, and active modules."
      breadcrumbs={[{ label: "Workspace" }, { label: "Settings" }]}
    >
      <div className="bg-white border border-[#e5e5e5] rounded-none flex flex-col min-h-[600px]">
        <div className="flex border-b border-[#e5e5e5] bg-[#fafafa]">
          {["general", "members", "apps"].map((tab) => (
            <button 
              key={tab}
              onClick={() => setActiveTab(tab as any)}
              className={cn(
                "px-6 py-4 text-[11px] font-bold uppercase tracking-widest transition-colors border-b-2", 
                activeTab === tab ? "border-[#09090b] text-[#09090b] bg-white" : "border-transparent text-[#71717a] hover:text-[#09090b]"
              )}
            >
              {tab}
            </button>
          ))}
        </div>

        {activeTab === "general" && (
          <div className="p-6 md:p-8 flex-1">
            {isWorkspaceLoading ? (
              <div className="flex justify-center p-8"><Loader2 className="animate-spin text-[#a1a1aa]" /></div>
            ) : (
              <form onSubmit={(e) => { e.preventDefault(); updateWorkspaceMutation.mutate(); }} className="max-w-md space-y-6">
                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Organization Name *</label>
                  <input required value={name} onChange={e => setName(e.target.value)} disabled={updateWorkspaceMutation.isPending} className="flex h-10 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50" />
                </div>
                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Tenant Slug *</label>
                  <input required value={slug} onChange={e => setSlug(e.target.value)} disabled={updateWorkspaceMutation.isPending} className="flex h-10 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 font-mono text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50" />
                  <p className="text-[10px] text-[#a1a1aa] mt-1">Changing the slug will break existing public checkout and portal links.</p>
                </div>
                <div className="pt-2">
                  <button type="submit" disabled={updateWorkspaceMutation.isPending} className="h-9 px-6 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest hover:bg-[#27272a] transition-colors disabled:opacity-50 flex items-center gap-2">
                    {updateWorkspaceMutation.isPending && <Loader2 size={14} className="animate-spin" />} Save Changes
                  </button>
                </div>
              </form>
            )}
          </div>
        )}

        {activeTab === "members" && (
          <div className="flex-1 flex flex-col">
            <div className="p-6 md:p-8 border-b border-[#f4f4f5] bg-[#fafafa]/50">
              <h4 className="text-[11px] font-bold uppercase tracking-widest text-[#09090b] mb-4">Invite New Member</h4>
              <form onSubmit={(e) => { e.preventDefault(); inviteMutation.mutate(); }} className="flex items-end gap-4 max-w-2xl">
                <div className="space-y-1.5 flex-1">
                  <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a]">Email Address</label>
                  <input required type="email" value={inviteEmail} onChange={e => setInviteEmail(e.target.value)} disabled={inviteMutation.isPending} placeholder="colleague@example.com" className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b]" />
                </div>
                <div className="space-y-1.5 w-48">
                  <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a]">Role</label>
                  <select value={inviteRole} onChange={e => setInviteRole(e.target.value)} disabled={inviteMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b]">
                    <option value="ADMIN">Admin</option>
                    <option value="STAFF">Staff</option>
                  </select>
                </div>
                <button type="submit" disabled={inviteMutation.isPending} className="h-9 px-6 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest hover:bg-[#27272a] transition-colors disabled:opacity-50 flex items-center gap-2">
                  {inviteMutation.isPending ? <Loader2 size={14} className="animate-spin" /> : <UserPlus size={14} />} Invite
                </button>
              </form>
            </div>

            <div className="p-6 md:p-8 space-y-8 flex-1">
              <div>
                <h4 className="text-[11px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-2 mb-4">Pending Invitations</h4>
                {isInvitesLoading ? (
                  <div className="flex justify-center py-4"><Loader2 className="animate-spin text-[#a1a1aa]" size={16} /></div>
                ) : invites?.filter((i: any) => i.status === "PENDING" && new Date(i.expires_at).getTime() > Date.now()).length === 0 ? (
                  <p className="text-[12px] text-[#a1a1aa]">No pending invitations.</p>
                ) : (
                  <div className="overflow-x-auto border border-[#e5e5e5]">
                    <table className="w-full text-left text-[13px]">
                      <tbody className="divide-y divide-[#f4f4f5]">
                        {invites?.filter((i: any) => i.status === "PENDING" && new Date(i.expires_at).getTime() > Date.now()).map((invite: any) => (
                          <tr key={invite.id} className="bg-white hover:bg-[#fafafa]">
                            <td className="px-4 py-3 font-medium text-[#09090b]">{invite.email}</td>
                            <td className="px-4 py-3 text-[#71717a] text-[11px] uppercase tracking-wider">{invite.role}</td>
                            <td className="px-4 py-3 text-right">
                              <button onClick={() => { if(window.confirm("Revoke this invitation?")) revokeInviteMutation.mutate(invite.id); }} className="text-[10px] font-bold uppercase tracking-widest text-rose-600 hover:text-rose-700 flex items-center justify-end w-full gap-1">
                                <X size={12} /> Revoke
                              </button>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>

              <div>
                <h4 className="text-[11px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-2 mb-4">Active Members</h4>
                {isMembersLoading ? (
                  <div className="flex justify-center py-4"><Loader2 className="animate-spin text-[#a1a1aa]" size={16} /></div>
                ) : (
                  <div className="overflow-x-auto border border-[#e5e5e5]">
                    <table className="w-full text-left text-[13px]">
                      <thead className="bg-[#fafafa] border-b border-[#e5e5e5] text-[9px] font-bold uppercase tracking-widest text-[#71717a]">
                        <tr>
                          <th className="px-4 py-2">Name</th>
                          <th className="px-4 py-2">Email</th>
                          <th className="px-4 py-2">Role</th>
                          <th className="px-4 py-2 text-right">Action</th>
                        </tr>
                      </thead>
                      <tbody className="divide-y divide-[#f4f4f5]">
                        {members?.map((member: any) => (
                          <tr key={member.id} className="bg-white hover:bg-[#fafafa]">
                            <td className="px-4 py-3 font-medium text-[#09090b]">{member.name}</td>
                            <td className="px-4 py-3 text-[#71717a]">{member.email}</td>
                            <td className="px-4 py-3"><span className="text-[9px] px-1.5 py-0.5 border border-indigo-200 bg-indigo-50 text-indigo-700 font-bold uppercase tracking-widest">{member.role}</span></td>
                            <td className="px-4 py-3 text-right">
                              <button onClick={() => { if(window.confirm("Remove this user from the workspace?")) removeMemberMutation.mutate(member.global_user_id); }} className="text-[10px] font-bold uppercase tracking-widest text-rose-600 hover:text-rose-700 flex items-center justify-end w-full gap-1">
                                <Trash2 size={12} /> Remove
                              </button>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>
            </div>
          </div>
        )}

        {activeTab === "apps" && (
          <div className="p-6 md:p-8 flex-1">
            <div className="mb-6 flex items-start gap-3 bg-blue-50 border border-blue-200 p-4 rounded-sm">
              <ShieldAlert size={16} className="text-blue-600 mt-0.5 shrink-0" />
              <div>
                <h4 className="text-[12px] font-bold text-blue-800 uppercase tracking-widest">Module Entitlements</h4>
                <p className="text-[13px] text-blue-700 mt-1 leading-relaxed">
                  Toggle platform modules on or off for this workspace. Toggling a module initializes its database structures and default templates.
                </p>
              </div>
            </div>

            {isAppsLoading ? (
              <div className="flex justify-center p-8"><Loader2 className="animate-spin text-[#a1a1aa]" /></div>
            ) : (
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                {AVAILABLE_APPS.map((app) => {
                  const isActive = apps?.includes(app.id);
                  return (
                    <div key={app.id} className="border border-[#e5e5e5] p-5 flex flex-col sm:flex-row sm:items-center justify-between gap-4 bg-white">
                      <div>
                        <h4 className="text-[13px] font-bold text-[#09090b] mb-1">{app.name}</h4>
                        <p className="text-[11px] text-[#71717a]">{app.description}</p>
                      </div>
                      <button 
                        onClick={() => toggleAppMutation.mutate({ appId: app.id, isActive: !isActive })}
                        disabled={toggleAppMutation.isPending}
                        className={cn(
                          "relative inline-flex h-5 w-9 items-center rounded-full transition-colors focus:outline-none shrink-0",
                          isActive ? "bg-[#09090b]" : "bg-[#e5e5e5]"
                        )}
                      >
                        <span className={cn("inline-block h-4 w-4 transform rounded-full bg-white transition-transform", isActive ? "translate-x-4" : "translate-x-1")} />
                      </button>
                    </div>
                  );
                })}
              </div>
            )}
          </div>
        )}
      </div>
    </PageLayout>
  );
}
