import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Loader2, UserMinus } from "lucide-react";
import { toast } from "sonner";
import { client, type components } from "../../../lib/api-client";
import { useOutletContext } from "react-router-dom";
import PageLayout from "../../core/components/PageLayout";
import type { OpsOutletContext } from "../../../App";

type WorkspaceMemberDto = components["schemas"]["One.WorkspaceMemberDto"];
type WorkspaceInvitationDto = components["schemas"]["One.WorkspaceInvitationDto"];

export default function TeamPage() {
  const { activeWorkspaceId, role: workspaceRole } = useOutletContext<OpsOutletContext>();
  const canInvite = workspaceRole === "ADMIN" || workspaceRole === "SUPER_ADMIN";
  const queryClient = useQueryClient();
  const [email, setEmail] = useState("");
  const [role, setRole] = useState("MEMBER");

  const { data: members, isLoading } = useQuery({
    queryKey: ["workspace-members", activeWorkspaceId],
    queryFn: async () => {
      const { data, error } = await client.GET("/one/workspaces/{id}/members", {
        params: { path: { id: activeWorkspaceId } },
      });
      if (error) throw new Error(error.detail);
      return data as WorkspaceMemberDto[];
    },
    enabled: !!activeWorkspaceId,
  });

  const { data: invites, isLoading: invitesLoading } = useQuery({
    queryKey: ["workspace-invites", activeWorkspaceId],
    queryFn: async () => {
      const { data, error } = await client.GET("/one/workspaces/{id}/invites", {
        params: { path: { id: activeWorkspaceId } },
      });
      if (error) throw new Error(error.detail);
      return data as WorkspaceInvitationDto[];
    },
    enabled: !!activeWorkspaceId,
  });

  const inviteMutation = useMutation({
    mutationFn: async () => {
      const { error } = await client.POST("/one/workspaces/{id}/invites", {
        params: { path: { id: activeWorkspaceId } },
        body: { email: email.trim().toLowerCase(), role },
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("Invitation sent");
      setEmail("");
      queryClient.invalidateQueries({ queryKey: ["workspace-members", activeWorkspaceId] });
      queryClient.invalidateQueries({ queryKey: ["workspace-invites", activeWorkspaceId] });
    },
    onError: (err: Error) => toast.error(err.message || "Failed to invite"),
  });

  const revokeMutation = useMutation({
    mutationFn: async (inviteId: string) => {
      const { error } = await client.DELETE("/one/workspaces/{id}/invites/{inviteId}", {
        params: { path: { id: activeWorkspaceId, inviteId } },
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("Invitation revoked");
      queryClient.invalidateQueries({ queryKey: ["workspace-invites", activeWorkspaceId] });
    },
    onError: (err: Error) => toast.error(err.message || "Failed to revoke"),
  });

  const removeMutation = useMutation({
    mutationFn: async (userId: string) => {
      const { error } = await client.DELETE("/one/workspaces/{id}/members/{userId}", {
        params: { path: { id: activeWorkspaceId, userId } },
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("Member removed");
      queryClient.invalidateQueries({ queryKey: ["workspace-members", activeWorkspaceId] });
    },
    onError: (err: Error) => toast.error(err.message || "Failed to remove"),
  });

  return (
    <PageLayout
      title="Team"
      description="Invite staff as Admin, Member, or Viewer. Members operate commerce; Viewers can only read."
      breadcrumbs={[{ label: "Workspace", href: "/workspace/general" }, { label: "Team" }]}
    >
      <div className="bg-white border border-[#e5e5e5] flex flex-col">
        {canInvite && (
        <form
          className="px-5 py-4 border-b border-[#f4f4f5] flex flex-col sm:flex-row gap-3 bg-[#fafafa]/50"
          onSubmit={(e) => {
            e.preventDefault();
            if (!email.trim()) return;
            inviteMutation.mutate();
          }}
        >
          <input
            required
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            placeholder="staff@example.com"
            className="flex h-9 flex-1 rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b]"
          />
          <select
            value={role}
            onChange={(e) => setRole(e.target.value)}
            className="h-9 rounded-sm border border-[#e5e5e5] bg-white px-2 text-[12px] uppercase tracking-wider"
          >
            <option value="ADMIN">Admin</option>
            <option value="MEMBER">Member</option>
            <option value="VIEWER">Viewer</option>
          </select>
          <button
            type="submit"
            disabled={inviteMutation.isPending}
            className="h-9 px-4 rounded-sm bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest disabled:opacity-50"
          >
            {inviteMutation.isPending ? <Loader2 size={13} className="animate-spin" /> : "Invite"}
          </button>
        </form>
        )}

        <div className="divide-y divide-[#f4f4f5]">
          {isLoading && (
            <div className="p-8 flex justify-center text-[#71717a]">
              <Loader2 size={16} className="animate-spin" />
            </div>
          )}
          {members?.map((m) => {
            const managerCount = (members ?? []).filter((row) =>
              row.role === "ADMIN" || row.role === "SUPER_ADMIN").length;
            const isLastManager = managerCount <= 1 && (m.role === "ADMIN" || m.role === "SUPER_ADMIN");
            return (
            <div key={m.id} className="px-5 py-3 flex items-center justify-between gap-3">
              <div className="min-w-0">
                <div className="text-[13px] font-medium text-[#09090b] truncate">{m.name || m.email}</div>
                <div className="text-[11px] text-[#71717a] truncate">{m.email}</div>
              </div>
              <div className="flex items-center gap-3 shrink-0">
                <span className="text-[10px] font-bold uppercase tracking-widest text-[#71717a]">{m.role}</span>
                {!isLastManager && (
                <button
                  type="button"
                  onClick={() => removeMutation.mutate(m.global_user_id)}
                  disabled={removeMutation.isPending}
                  className="p-1.5 text-[#a1a1aa] hover:text-rose-600"
                  title="Remove"
                >
                  <UserMinus size={14} />
                </button>
                )}
              </div>
            </div>
            );
          })}
          {!isLoading && (members?.length ?? 0) === 0 && (
            <div className="p-8 text-center text-[12px] text-[#71717a]">No members yet.</div>
          )}
        </div>
      </div>

      <div className="bg-white border border-[#e5e5e5] flex flex-col mt-6">
        <div className="px-5 py-3 border-b border-[#f4f4f5] text-[10px] font-bold uppercase tracking-widest text-[#71717a]">
          Pending invitations
        </div>
        <div className="divide-y divide-[#f4f4f5]">
          {invitesLoading && (
            <div className="p-8 flex justify-center text-[#71717a]">
              <Loader2 size={16} className="animate-spin" />
            </div>
          )}
          {invites?.map((invite) => (
            <div key={invite.id} className="px-5 py-3 flex items-center justify-between gap-3">
              <div className="min-w-0">
                <div className="text-[13px] font-medium text-[#09090b] truncate">{invite.email}</div>
                <div className="text-[11px] text-[#71717a] truncate">
                  {invite.role} · {invite.status} · expires {new Date(invite.expires_at).toLocaleString("en-GB")}
                </div>
              </div>
              {canInvite && invite.status === "PENDING" && (
                <button
                  type="button"
                  onClick={() => revokeMutation.mutate(invite.id)}
                  disabled={revokeMutation.isPending}
                  className="text-[11px] font-bold uppercase tracking-widest text-rose-700 hover:underline disabled:opacity-50"
                >
                  Revoke
                </button>
              )}
            </div>
          ))}
          {!invitesLoading && (invites?.length ?? 0) === 0 && (
            <div className="p-8 text-center text-[12px] text-[#71717a]">No invitations.</div>
          )}
        </div>
      </div>
    </PageLayout>
  );
}
