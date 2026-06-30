import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Loader2, RefreshCw, XCircle, CheckCircle2 } from "lucide-react";
import { client, type components } from "../../../lib/api-client";
import { useOutletContext } from "react-router-dom";
import PageLayout from "../../core/components/PageLayout";
import { cn } from "../../../lib/utils";

type WebhookDeliveryLogDto = components["schemas"]["One.WebhookDeliveryLogDto"];

export default function DeliveryLogsPage() {
  const { activeWorkspaceId } = useOutletContext<{ activeWorkspaceId: string }>();
  const queryClient = useQueryClient();

  const { data: logs, isLoading: isLogsLoading } = useQuery({
    queryKey: ["developer-webhook-logs", activeWorkspaceId],
    queryFn: async () => {
      const { data, error } = await client.GET("/one/workspaces/{id}/webhooks/logs", {
        params: { path: { id: activeWorkspaceId } }
      });
      if (error) throw new Error(error.detail);
      return data as WebhookDeliveryLogDto[];
    },
    enabled: !!activeWorkspaceId
  });

  return (
    <PageLayout
      title="Delivery Logs"
      description="Audit the outbound webhook payloads sent from Lazuar to your external endpoints."
      breadcrumbs={[{ label: "Developer" }, { label: "Delivery Logs" }]}
      actionButton={
        <button 
          onClick={() => queryClient.invalidateQueries({ queryKey: ["developer-webhook-logs"] })}
          disabled={isLogsLoading}
          className="h-9 px-4 bg-white border border-[#e5e5e5] text-[#09090b] text-[11px] font-bold uppercase tracking-widest flex items-center gap-2 hover:bg-[#fafafa] transition-colors disabled:opacity-50"
        >
          <RefreshCw size={14} className={cn(isLogsLoading && "animate-spin")} /> Refresh
        </button>
      }
    >
      <div className="bg-white border border-[#e5e5e5] rounded-none flex flex-col h-full min-h-[600px]">
        <div className="w-full overflow-x-auto">
          <table className="w-full text-left text-[13px] min-w-[800px]">
            <thead className="bg-[#fafafa] border-b border-[#e5e5e5] select-none">
              <tr>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[20%]">Status</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[20%]">Event Type</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[40%]">Response / Details</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[20%] text-right">Timestamp</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#f4f4f5]">
              {isLogsLoading ? (
                <tr>
                  <td colSpan={4} className="py-12 text-center text-[#a1a1aa]">
                    <Loader2 className="animate-spin mx-auto" size={20} />
                  </td>
                </tr>
              ) : !logs || logs.length === 0 ? (
                <tr>
                  <td colSpan={4} className="py-12 text-center text-[#71717a] text-[13px]">
                    No webhook deliveries logged yet.
                  </td>
                </tr>
              ) : (
                logs.map((log) => (
                  <tr key={log.id} className="hover:bg-[#fafafa] transition-colors">
                    <td className="px-5 py-4">
                      <div className="flex items-center gap-2">
                        {log.status === "SUCCESS" ? (
                          <CheckCircle2 size={16} className="text-emerald-500" />
                        ) : log.status === "PENDING" ? (
                          <Loader2 size={16} className="text-blue-500 animate-spin" />
                        ) : (
                          <XCircle size={16} className="text-rose-500" />
                        )}
                        <span className="text-[12px] font-bold uppercase tracking-widest text-[#09090b]">{log.status}</span>
                      </div>
                    </td>
                    <td className="px-5 py-4">
                      <span className="font-mono font-bold text-[#09090b] text-[12px]">{log.event_type}</span>
                    </td>
                    <td className="px-5 py-4">
                      {log.status === "FAILED" && log.last_error ? (
                        <p className="text-[11px] text-rose-600 font-mono break-all line-clamp-2">
                          {log.last_error}
                        </p>
                      ) : (log.status === "PENDING" && log.attempt_count > 0) ? (
                        <p className="text-[11px] text-amber-600 font-mono">
                          Retrying... (Attempt {log.attempt_count})
                        </p>
                      ) : (
                        <p className="text-[11px] text-[#71717a] font-mono">
                          HTTP 200 OK
                        </p>
                      )}
                    </td>
                    <td className="px-5 py-4 text-right">
                      <span className="text-[11px] font-mono text-[#71717a]">
                        {new Date(log.created_at).toLocaleString('en-GB', { dateStyle: 'short', timeStyle: 'medium' })}
                      </span>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
    </PageLayout>
  );
}
