import { useState, useEffect } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2, Code, Key, Save } from "lucide-react";
import { toast } from "sonner";
import { client, type components } from "../../../lib/api-client";
import { useOutletContext } from "react-router-dom";
import PageLayout from "../../core/components/PageLayout";
import QuickCopy from "../../core/components/QuickCopy";

type WebhookEndpointDto = components["schemas"]["One.WebhookEndpointDto"];

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
      title="Outbound Webhooks"
      description="Configure real-time event dispatching to connect Lazuar to your external SaaS or application."
      breadcrumbs={[{ label: "Developer" }, { label: "Outbound Webhooks" }]}
    >
      <div className="max-w-3xl bg-white border border-[#e5e5e5] rounded-none flex flex-col">
        <form onSubmit={handleSubmit} className="flex flex-col">
          <div className="p-6 md:p-8 space-y-8">
            
            <div className="space-y-4">
              <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5 flex items-center gap-2">
                <Code size={13} /> Endpoint Configuration
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
    </PageLayout>
  );
}
