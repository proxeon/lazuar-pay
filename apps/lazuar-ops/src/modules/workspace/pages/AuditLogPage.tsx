import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Loader2 } from "lucide-react";
import { API_URL } from "../../../lib/api-client";
import { useOutletContext } from "react-router-dom";
import PageLayout from "../../core/components/PageLayout";

type AuditEvent = {
  id: string;
  actor_email?: string | null;
  action: string;
  entity_type: string;
  entity_id: string;
  metadata_json?: string | null;
  created_at: string;
};

export default function AuditLogPage() {
  const { activeWorkspaceId } = useOutletContext<{ activeWorkspaceId: string }>();
  const [page, setPage] = useState(1);

  const { data, isLoading } = useQuery({
    queryKey: ["workspace-audit", activeWorkspaceId, page],
    queryFn: async () => {
      const res = await fetch(
        `${API_URL}/one/workspaces/${activeWorkspaceId}/audit?page=${page}&limit=50`,
        { credentials: "include", headers: { "X-Tenant-Id": activeWorkspaceId } },
      );
      if (res.status === 403) return { data: [] as AuditEvent[], total_count: 0, total_pages: 1 };
      if (!res.ok) throw new Error("Failed to load audit log");
      return (await res.json()) as { data: AuditEvent[]; total_count: number; total_pages: number };
    },
    enabled: !!activeWorkspaceId,
  });

  return (
    <PageLayout
      title="Audit log"
      description="Who changed money or identity in this workspace. Reads are not logged."
      breadcrumbs={[{ label: "Workspace", href: "/workspace/general" }, { label: "Audit log" }]}
    >
      <div className="bg-white border border-[#e5e5e5]">
        <table className="w-full text-left text-[13px]">
          <thead className="bg-[#fafafa] text-[10px] font-bold uppercase tracking-widest text-[#71717a]">
            <tr>
              <th className="px-4 py-2">When</th>
              <th className="px-4 py-2">Actor</th>
              <th className="px-4 py-2">Action</th>
              <th className="px-4 py-2">Entity</th>
            </tr>
          </thead>
          <tbody>
            {isLoading && (
              <tr>
                <td colSpan={4} className="px-4 py-8 text-center text-[#71717a]">
                  <Loader2 size={16} className="inline animate-spin" />
                </td>
              </tr>
            )}
            {data?.data.map((row) => (
              <tr key={row.id} className="border-t border-[#f4f4f5]">
                <td className="px-4 py-2 font-mono text-[12px] text-[#52525b]">
                  {new Date(row.created_at).toLocaleString()}
                </td>
                <td className="px-4 py-2">{row.actor_email || "—"}</td>
                <td className="px-4 py-2 font-medium">{row.action}</td>
                <td className="px-4 py-2 text-[#71717a]">
                  {row.entity_type} · {row.entity_id.slice(0, 8)}
                </td>
              </tr>
            ))}
            {!isLoading && (data?.data.length ?? 0) === 0 && (
              <tr>
                <td colSpan={4} className="px-4 py-8 text-center text-[12px] text-[#71717a]">
                  No audit events yet.
                </td>
              </tr>
            )}
          </tbody>
        </table>
        {(data?.total_pages ?? 1) > 1 && (
          <div className="px-4 py-2 border-t border-[#f4f4f5] flex justify-end gap-2">
            <button type="button" disabled={page <= 1} onClick={() => setPage((p) => p - 1)} className="text-[11px] uppercase tracking-widest disabled:opacity-40">
              Prev
            </button>
            <button
              type="button"
              disabled={page >= (data?.total_pages ?? 1)}
              onClick={() => setPage((p) => p + 1)}
              className="text-[11px] uppercase tracking-widest disabled:opacity-40"
            >
              Next
            </button>
          </div>
        )}
      </div>
    </PageLayout>
  );
}
