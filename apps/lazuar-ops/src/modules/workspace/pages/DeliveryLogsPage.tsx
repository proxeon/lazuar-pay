import { Fragment, useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Loader2, RefreshCw, XCircle, CheckCircle2, ChevronDown, ChevronRight } from "lucide-react";
import { client, type components } from "../../../lib/api-client";
import { useOutletContext } from "react-router-dom";
import PageLayout from "../../core/components/PageLayout";
import { cn } from "../../../lib/utils";

type WebhookDeliveryLogDto = components["schemas"]["One.WebhookDeliveryLogDto"];

function shortId(id: string): string {
  if (!id) return "—";
  return id.length > 8 ? `${id.slice(0, 8)}…` : id;
}

function statusPresentation(status: string) {
  const normalized = (status || "").toUpperCase();
  if (normalized === "SUCCESS" || normalized === "DELIVERED") {
    return {
      icon: <CheckCircle2 size={16} className="text-emerald-500 shrink-0" />,
      label: normalized,
      tone: "success" as const,
    };
  }
  if (normalized === "PENDING" || normalized === "RETRYING") {
    return {
      icon: <Loader2 size={16} className="text-blue-500 animate-spin shrink-0" />,
      label: normalized,
      tone: "pending" as const,
    };
  }
  return {
    icon: <XCircle size={16} className="text-rose-500 shrink-0" />,
    label: normalized || "UNKNOWN",
    tone: "failed" as const,
  };
}

export default function DeliveryLogsPage() {
  const { activeWorkspaceId } = useOutletContext<{ activeWorkspaceId: string }>();
  const queryClient = useQueryClient();
  const [expandedId, setExpandedId] = useState<string | null>(null);

  const { data: logs, isLoading: isLogsLoading } = useQuery({
    queryKey: ["developer-webhook-logs", activeWorkspaceId],
    queryFn: async () => {
      const { data, error } = await client.GET("/one/workspaces/{id}/webhooks/logs", {
        params: { path: { id: activeWorkspaceId } },
      });
      if (error) throw new Error(error.detail);
      return data as WebhookDeliveryLogDto[];
    },
    enabled: !!activeWorkspaceId,
  });

  return (
    <PageLayout
      title="Delivery Logs"
      description="Audit outbound webhook deliveries from Lazuar to your workspace endpoints."
      breadcrumbs={[{ label: "Developer" }, { label: "Delivery Logs" }]}
      actionButton={
        <button
          onClick={() =>
            queryClient.invalidateQueries({ queryKey: ["developer-webhook-logs", activeWorkspaceId] })
          }
          disabled={isLogsLoading}
          className="h-9 px-4 bg-white border border-[#e5e5e5] text-[#09090b] text-[11px] font-bold uppercase tracking-widest flex items-center gap-2 hover:bg-[#fafafa] transition-colors disabled:opacity-50"
        >
          <RefreshCw size={14} className={cn(isLogsLoading && "animate-spin")} /> Refresh
        </button>
      }
    >
      <div className="bg-white border border-[#e5e5e5] rounded-none flex flex-col h-full min-h-[600px]">
        <div className="w-full overflow-x-auto">
          <table className="w-full text-left text-[13px] min-w-[960px]">
            <thead className="bg-[#fafafa] border-b border-[#e5e5e5] select-none">
              <tr>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[8%]" />
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[14%]">
                  Status
                </th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[18%]">
                  Event Type
                </th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[12%]">
                  Delivery
                </th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[10%]">
                  Attempts
                </th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[20%]">
                  Error / Details
                </th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[18%] text-right">
                  Timestamp
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#f4f4f5]">
              {isLogsLoading ? (
                <tr>
                  <td colSpan={7} className="py-12 text-center text-[#a1a1aa]">
                    <Loader2 className="animate-spin mx-auto" size={20} />
                  </td>
                </tr>
              ) : !logs || logs.length === 0 ? (
                <tr>
                  <td colSpan={7} className="py-12 text-center text-[#71717a] text-[13px]">
                    No webhook deliveries logged yet.
                  </td>
                </tr>
              ) : (
                logs.map((log) => {
                  const presentation = statusPresentation(log.status);
                  const isExpanded = expandedId === log.id;
                  const hasDetail = Boolean(log.last_error) || presentation.tone !== "success";

                  return (
                    <Fragment key={log.id}>
                      <tr
                        className={cn(
                          "hover:bg-[#fafafa] transition-colors",
                          hasDetail && "cursor-pointer",
                          isExpanded && "bg-[#fafafa]"
                        )}
                        onClick={() => {
                          if (!hasDetail) return;
                          setExpandedId(isExpanded ? null : log.id);
                        }}
                      >
                        <td className="px-5 py-4">
                          {hasDetail ? (
                            isExpanded ? (
                              <ChevronDown size={14} className="text-[#a1a1aa]" />
                            ) : (
                              <ChevronRight size={14} className="text-[#a1a1aa]" />
                            )
                          ) : (
                            <span className="inline-block w-3.5" />
                          )}
                        </td>
                        <td className="px-5 py-4">
                          <div className="flex items-center gap-2">
                            {presentation.icon}
                            <span className="text-[12px] font-bold uppercase tracking-widest text-[#09090b]">
                              {presentation.label}
                            </span>
                          </div>
                        </td>
                        <td className="px-5 py-4">
                          <span className="font-mono font-bold text-[#09090b] text-[12px]">
                            {log.event_type || "—"}
                          </span>
                        </td>
                        <td className="px-5 py-4">
                          <span
                            className="font-mono text-[11px] text-[#71717a]"
                            title={log.id}
                          >
                            {shortId(log.id)}
                          </span>
                        </td>
                        <td className="px-5 py-4">
                          <span className="font-mono text-[12px] text-[#09090b]">{log.attempt_count ?? 0}</span>
                        </td>
                        <td className="px-5 py-4">
                          {log.last_error ? (
                            <p className="text-[11px] text-rose-600 font-mono break-all line-clamp-2">
                              {log.last_error}
                            </p>
                          ) : presentation.tone === "pending" ? (
                            <p className="text-[11px] text-amber-600 font-mono">
                              {(log.attempt_count ?? 0) > 0
                                ? `In flight (attempt ${log.attempt_count})`
                                : "Queued"}
                            </p>
                          ) : presentation.tone === "success" ? (
                            <p className="text-[11px] text-[#71717a] font-mono">Delivered</p>
                          ) : (
                            <p className="text-[11px] text-[#a1a1aa] font-mono">No error detail</p>
                          )}
                        </td>
                        <td className="px-5 py-4 text-right">
                          <span className="text-[11px] font-mono text-[#71717a]">
                            {new Date(log.created_at).toLocaleString("en-GB", {
                              dateStyle: "short",
                              timeStyle: "medium",
                            })}
                          </span>
                        </td>
                      </tr>
                      {isExpanded && (
                        <tr className="bg-[#fafafa]">
                          <td colSpan={7} className="px-5 py-4">
                            <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-[12px]">
                              <div className="space-y-1">
                                <div className="text-[9px] font-bold uppercase tracking-widest text-[#71717a]">
                                  Delivery ID
                                </div>
                                <div className="font-mono text-[#09090b] break-all">{log.id}</div>
                              </div>
                              <div className="space-y-1">
                                <div className="text-[9px] font-bold uppercase tracking-widest text-[#71717a]">
                                  Event type
                                </div>
                                <div className="font-mono text-[#09090b]">{log.event_type || "—"}</div>
                              </div>
                              <div className="space-y-1">
                                <div className="text-[9px] font-bold uppercase tracking-widest text-[#71717a]">
                                  Status
                                </div>
                                <div className="font-mono text-[#09090b]">{log.status}</div>
                              </div>
                              <div className="space-y-1">
                                <div className="text-[9px] font-bold uppercase tracking-widest text-[#71717a]">
                                  Attempt count
                                </div>
                                <div className="font-mono text-[#09090b]">{log.attempt_count ?? 0}</div>
                              </div>
                              <div className="space-y-1 md:col-span-2">
                                <div className="text-[9px] font-bold uppercase tracking-widest text-[#71717a]">
                                  Last error
                                </div>
                                <pre className="font-mono text-[11px] text-rose-700 whitespace-pre-wrap break-all bg-white border border-[#e5e5e5] p-3 min-h-[2.5rem]">
                                  {log.last_error || "—"}
                                </pre>
                              </div>
                              <p className="md:col-span-2 text-[11px] text-[#a1a1aa]">
                                Redeliver / resend is not available yet (API residual). Retry is handled by the
                                outbound dispatcher for failed deliveries.
                              </p>
                            </div>
                          </td>
                        </tr>
                      )}
                    </Fragment>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </div>
    </PageLayout>
  );
}
