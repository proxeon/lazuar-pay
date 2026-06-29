// apps/ops-page/src/modules/workspace/pages/DeveloperSettingsPage.tsx
import { useState, useEffect } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2, Code, Key, Zap, Save, RefreshCw, XCircle, CheckCircle2 } from "lucide-react";
import { toast } from "sonner";
import { client, type components } from "../../../lib/api-client";
import { useOutletContext } from "react-router-dom";
import PageLayout from "../../core/components/PageLayout";
import QuickCopy from "../../core/components/QuickCopy";
import { cn } from "../../../lib/utils";

type WebhookEndpointDto = components["schemas"]["One.WebhookEndpointDto"];
type WebhookDeliveryLogDto = components["schemas"]["One.WebhookDeliveryLogDto"];

export default function DeveloperSettingsPage() {
  const { activeWorkspaceId } = useOutletContext<{ activeWorkspaceId: string }>();
  const queryClient = useQueryClient();
  
  const [url, setUrl] = useState("");
  const [isActive, setIsActive] = useState(false);
  const [secretKey, setSecretKey] = useState("");
  
  const { data: webhook, isLoading: isWebhookLoading } = useQuery({
    queryKey: ["developer-webhook", activeWorkspaceId],
    queryFn: async () => {
      const { data, error, response } = await client.GET("/one/workspaces/{id}/webhooks", {
        params: { path: { id: activeWorkspaceId } }
      });
      if (response.status === 404) return null;
      if (error) throw new Error(error.detail);
      return data as WebhookEndpointDto;
    },
    enabled: !!activeWorkspaceId
  });

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

  useEffect(() => {
    if (webhook) {
      setUrl(webhook.url);
      setIsActive(webhook.is_active);
      setSecretKey(webhook.secret_key);
    }
  }, [webhook]);

  const saveMutation = useMutation({
    mutationFn: async () => {
      const { error } = await client.PUT("/one/workspaces/{id}/webhooks", {
        params: { path: { id: activeWorkspaceId } },
        body: { url: url.trim(), is_active: isActive }
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("Webhook settings saved.");
      queryClient.invalidateQueries({ queryKey: ["developer-webhook", activeWorkspaceId] });
    },
    onError: (err: any) => toast.error(err.message || "Failed to save webhook settings.")
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    saveMutation.mutate();
  };

  return (
    <PageLayout
      title="Developer API & Webhooks"
      description="Connect Lazuar to your external SaaS or application."
      breadcrumbs={[{ label: "Workspace" }, { label: "Developer Settings" }]}
    >
      <div className="flex flex-col lg:flex-row gap-6 items-start">
        
        <div className="flex-1 w-full bg-white border border-[#e5e5e5] rounded-none flex flex-col">
          <form onSubmit={handleSubmit} className="flex flex-col">
            <div className="p-6 md:p-8 space-y-8">
              
              <div className="space-y-4">
                <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5 flex items-center gap-2">
                  <Code size={13} /> Outbound Webhook
                </label>
                <p className="text-[13px] text-[#71717a] leading-relaxed">
                  When a customer completes a checkout or cancels a subscription, Lazuar will automatically send an HTTP POST request to this URL with the transaction details.
                </p>
                <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                  <div className="space-y-1.5 md:col-span-2">
                    <label className="text-[11px] font-semibold text-[#09090b]">Payload URL</label>
                    <input 
                      type="url" 
                      required 
                      value={url} 
                      onChange={(e) => setUrl(e.target.value)} 
                      disabled={saveMutation.isPending || isWebhookLoading} 
                      placeholder="https://your-saas.com/api/webhooks/lazuar"
                      className="w-full h-10 border border-[#e5e5e5] bg-white px-3 font-mono text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50" 
                    />
                  </div>
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-semibold text-[#09090b]">Status</label>
                    <select 
                      value={isActive ? "true" : "false"} 
                      onChange={e => setIsActive(e.target.value === "true")} 
                      disabled={saveMutation.isPending || isWebhookLoading}
                      className="w-full h-10 border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50"
                    >
                      <option value="true">Active (Listening)</option>
                      <option value="false">Disabled</option>
                    </select>
                  </div>
                </div>
              </div>

              {secretKey && (
                <div className="space-y-4">
                  <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5 flex items-center gap-2">
                    <Key size={13} /> Signing Secret
                  </label>
                  <p className="text-[13px] text-[#71717a] leading-relaxed">
                    Verify incoming requests to ensure they originated from Lazuar. We use HMAC-SHA256 signatures passed in the <code className="bg-[#f4f4f5] px-1 py-0.5 rounded-sm">X-Lazuar-Signature</code> header.
                  </p>
                  <div className="flex items-center gap-0 border border-[#e5e5e5] bg-[#fafafa]">
                    <div className="flex-1 px-4 py-2 font-mono text-[13px] text-[#09090b] overflow-x-auto truncate">
                      {secretKey}
                    </div>
                    <div className="border-l border-[#e5e5e5] p-2 bg-white">
                      <QuickCopy text={secretKey} iconSize={16} />
                    </div>
                  </div>
                </div>
              )}
            </div>

            <div className="flex items-center justify-end p-5 border-t border-[#f4f4f5] bg-[#fafafa]/50 mt-auto">
              <button type="submit" disabled={saveMutation.isPending || isWebhookLoading} className="h-10 px-8 bg-[#09090b] text-white text-[11px] font-bold tracking-widest uppercase rounded-none hover:bg-[#27272a] disabled:opacity-50 transition-colors flex items-center gap-2">
                {saveMutation.isPending ? <Loader2 size={13} className="animate-spin" /> : <Save size={13} />} Save Webhook
              </button>
            </div>
          </form>
        </div>

        <div className="w-full lg:w-[420px] bg-white border border-[#e5e5e5] rounded-none flex flex-col shrink-0 min-h-[500px] max-h-[600px]">
          <div className="px-5 py-4 border-b border-[#f4f4f5] flex items-center justify-between bg-[#fafafa]/50 shrink-0">
            <h3 className="text-[11px] font-bold uppercase tracking-widest text-[#09090b] flex items-center gap-2">
              <Zap size={14} className="text-[#a1a1aa]" /> Delivery Logs
            </h3>
            <button 
              onClick={() => queryClient.invalidateQueries({ queryKey: ["developer-webhook-logs"] })}
              disabled={isLogsLoading}
              className="text-[#71717a] hover:text-[#09090b] transition-colors p-1"
            >
              <RefreshCw size={14} className={cn(isLogsLoading && "animate-spin")} />
            </button>
          </div>
          <div className="flex-1 overflow-y-auto">
            {isLogsLoading ? (
              <div className="p-8 flex justify-center"><Loader2 className="animate-spin text-[#a1a1aa]" /></div>
            ) : !logs || logs.length === 0 ? (
              <div className="p-8 text-center text-[#71717a] text-[12px]">No webhook deliveries logged yet.</div>
            ) : (
              <div className="divide-y divide-[#f4f4f5]">
                {logs.map((log) => (
                  <div key={log.id} className="p-4 hover:bg-[#fafafa] transition-colors">
                    <div className="flex justify-between items-start mb-2">
                      <div className="flex items-center gap-2">
                        {log.status === "SUCCESS" ? (
                          <CheckCircle2 size={14} className="text-emerald-500" />
                        ) : log.status === "PENDING" ? (
                          <Loader2 size={14} className="text-blue-500 animate-spin" />
                        ) : (
                          <XCircle size={14} className="text-rose-500" />
                        )}
                        <span className="text-[12px] font-mono font-bold text-[#09090b]">{log.event_type}</span>
                      </div>
                      <span className="text-[10px] text-[#a1a1aa] font-mono">{new Date(log.created_at).toLocaleString('en-GB')}</span>
                    </div>
                    {log.status === "FAILED" && log.last_error && (
                      <p className="text-[11px] text-rose-600 bg-rose-50 p-2 border border-rose-100 mt-2 font-mono break-all">
                        {log.last_error}
                      </p>
                    )}
                    {(log.status === "PENDING" && log.attempt_count > 0) && (
                      <p className="text-[10px] text-amber-600 mt-1">Retrying... (Attempt {log.attempt_count})</p>
                    )}
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>

      </div>
    </PageLayout>
  );
}
