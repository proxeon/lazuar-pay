import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2, Code, Key, Save, Plus } from "lucide-react";
import { toast } from "sonner";
import { client, type components } from "../../../lib/api-client";
import { useOutletContext } from "react-router-dom";
import PageLayout from "../../core/components/PageLayout";
import QuickCopy from "../../core/components/QuickCopy";

type WebhookEndpointDto = components["schemas"]["One.WebhookEndpointDto"];
type CreateWebhookEndpointResponseDto = components["schemas"]["One.CreateWebhookEndpointResponseDto"];

export default function DeveloperSettingsPage() {
  const { activeWorkspaceId } = useOutletContext<{ activeWorkspaceId: string }>();
  const queryClient = useQueryClient();

  const [url, setUrl] = useState("");
  const [isActive, setIsActive] = useState(true);
  /** Shown only once after create — never re-fetched from GET. */
  const [revealedSecret, setRevealedSecret] = useState<string | null>(null);
  const [editingId, setEditingId] = useState<string | null>(null);

  const { data: endpoints = [], isLoading: isWebhookLoading } = useQuery({
    queryKey: ["developer-webhooks", activeWorkspaceId],
    queryFn: async () => {
      const { data, error } = await client.GET("/one/workspaces/{id}/webhooks", {
        params: { path: { id: activeWorkspaceId } },
      });
      if (error) throw new Error(error.detail);
      return (data ?? []) as WebhookEndpointDto[];
    },
    enabled: !!activeWorkspaceId,
  });

  const createMutation = useMutation({
    mutationFn: async () => {
      const { data, error } = await client.POST("/one/workspaces/{id}/webhooks", {
        params: { path: { id: activeWorkspaceId } },
        body: { url: url.trim(), is_active: isActive, enabled_events: [] },
      });
      if (error) throw new Error(error.detail);
      return data as CreateWebhookEndpointResponseDto;
    },
    onSuccess: (created) => {
      toast.success("Webhook endpoint created. Copy the signing secret now — it will not be shown again.");
      setRevealedSecret(created.secret_key);
      setUrl("");
      setIsActive(true);
      setEditingId(null);
      queryClient.invalidateQueries({ queryKey: ["developer-webhooks", activeWorkspaceId] });
    },
    onError: (err: any) => toast.error(err.message || "Failed to create webhook endpoint."),
  });

  const updateMutation = useMutation({
    mutationFn: async () => {
      if (!editingId) throw new Error("No endpoint selected.");
      const { error } = await client.PUT("/one/workspaces/{id}/webhooks/{endpointId}", {
        params: { path: { id: activeWorkspaceId, endpointId: editingId } },
        body: { url: url.trim(), is_active: isActive },
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("Webhook endpoint updated.");
      setUrl("");
      setIsActive(true);
      setEditingId(null);
      setRevealedSecret(null);
      queryClient.invalidateQueries({ queryKey: ["developer-webhooks", activeWorkspaceId] });
    },
    onError: (err: any) => toast.error(err.message || "Failed to update webhook endpoint."),
  });

  const isPending = createMutation.isPending || updateMutation.isPending;

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (editingId) {
      updateMutation.mutate();
    } else {
      createMutation.mutate();
    }
  };

  const startEdit = (ep: WebhookEndpointDto) => {
    setEditingId(ep.id);
    setUrl(ep.url);
    setIsActive(ep.is_active);
    setRevealedSecret(null);
  };

  const startCreate = () => {
    setEditingId(null);
    setUrl("");
    setIsActive(true);
    setRevealedSecret(null);
  };

  return (
    <PageLayout
      title="Outbound Webhooks"
      description="Configure real-time event dispatching to connect Lazuar to your external SaaS or application."
      breadcrumbs={[{ label: "Developer" }, { label: "Outbound Webhooks" }]}
    >
      <div className="max-w-3xl space-y-6">
        {/* Existing endpoints (list without full secret) */}
        <div className="bg-white border border-[#e5e5e5] rounded-none">
          <div className="p-6 md:p-8 space-y-4">
            <div className="flex items-center justify-between border-b border-[#f4f4f5] pb-1.5">
              <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] flex items-center gap-2">
                <Code size={13} /> Registered endpoints
              </label>
              <button
                type="button"
                onClick={startCreate}
                className="text-[11px] font-bold uppercase tracking-widest text-[#09090b] flex items-center gap-1 hover:underline"
              >
                <Plus size={12} /> New
              </button>
            </div>

            {isWebhookLoading ? (
              <div className="flex items-center gap-2 text-[13px] text-[#71717a]">
                <Loader2 size={14} className="animate-spin" /> Loading…
              </div>
            ) : endpoints.length === 0 ? (
              <p className="text-[13px] text-[#71717a]">
                No endpoints yet. Create one below — Lazuar will POST signed events when checkouts and subscriptions change.
              </p>
            ) : (
              <ul className="divide-y divide-[#f4f4f5]">
                {endpoints.map((ep) => (
                  <li key={ep.id} className="py-3 flex flex-col sm:flex-row sm:items-center gap-2 sm:gap-4">
                    <div className="flex-1 min-w-0">
                      <div className="font-mono text-[13px] text-[#09090b] truncate">{ep.url}</div>
                      <div className="text-[11px] text-[#71717a] mt-0.5">
                        {ep.is_active ? "Active" : "Disabled"}
                        {ep.has_secret && ep.secret_hint ? ` · secret …${ep.secret_hint}` : ""}
                        {ep.enabled_events?.length
                          ? ` · ${ep.enabled_events.length} event filter(s)`
                          : " · all events"}
                      </div>
                    </div>
                    <button
                      type="button"
                      onClick={() => startEdit(ep)}
                      className="text-[11px] font-bold uppercase tracking-widest text-[#09090b] hover:underline shrink-0"
                    >
                      Edit
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </div>

        {/* Create / update form */}
        <div className="bg-white border border-[#e5e5e5] rounded-none flex flex-col">
          <form onSubmit={handleSubmit} className="flex flex-col">
            <div className="p-6 md:p-8 space-y-8">
              <div className="space-y-4">
                <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5 flex items-center gap-2">
                  <Code size={13} /> {editingId ? "Update endpoint" : "Create endpoint"}
                </label>
                <p className="text-[13px] text-[#71717a] leading-relaxed">
                  When a customer completes a checkout or a subscription lifecycle event fires, Lazuar POSTs a signed
                  payload to every active workspace endpoint that accepts the event type.
                </p>
                <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                  <div className="space-y-1.5 md:col-span-2">
                    <label className="text-[11px] font-semibold text-[#09090b]">Payload URL</label>
                    <input
                      type="url"
                      required
                      value={url}
                      onChange={(e) => setUrl(e.target.value)}
                      disabled={isPending || isWebhookLoading}
                      placeholder="https://your-saas.com/api/webhooks/lazuar"
                      className="w-full h-10 border border-[#e5e5e5] bg-white px-3 font-mono text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50"
                    />
                  </div>
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-semibold text-[#09090b]">Status</label>
                    <select
                      value={isActive ? "true" : "false"}
                      onChange={(e) => setIsActive(e.target.value === "true")}
                      disabled={isPending || isWebhookLoading}
                      className="w-full h-10 border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50"
                    >
                      <option value="true">Active (Listening)</option>
                      <option value="false">Disabled</option>
                    </select>
                  </div>
                </div>
              </div>

              {revealedSecret && (
                <div className="space-y-4">
                  <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5 flex items-center gap-2">
                    <Key size={13} /> Signing Secret (shown once)
                  </label>
                  <p className="text-[13px] text-[#71717a] leading-relaxed">
                    Verify requests with HMAC-SHA256. Header{" "}
                    <code className="bg-[#f4f4f5] px-1 py-0.5 rounded-sm">X-Lazuar-Signature</code> is{" "}
                    <code className="bg-[#f4f4f5] px-1 py-0.5 rounded-sm">t=…,v1=…</code> over{" "}
                    <code className="bg-[#f4f4f5] px-1 py-0.5 rounded-sm">{"{timestamp}.{body}"}</code>.
                  </p>
                  <div className="flex items-center gap-0 border border-[#e5e5e5] bg-[#fafafa]">
                    <div className="flex-1 px-4 py-2 font-mono text-[13px] text-[#09090b] overflow-x-auto truncate">
                      {revealedSecret}
                    </div>
                    <div className="border-l border-[#e5e5e5] p-2 bg-white">
                      <QuickCopy text={revealedSecret} iconSize={16} />
                    </div>
                  </div>
                </div>
              )}
            </div>

            <div className="flex items-center justify-end gap-3 p-5 border-t border-[#f4f4f5] bg-[#fafafa]/50 mt-auto">
              {editingId && (
                <button
                  type="button"
                  onClick={startCreate}
                  disabled={isPending}
                  className="h-10 px-4 text-[11px] font-bold tracking-widest uppercase text-[#71717a] hover:text-[#09090b] disabled:opacity-50"
                >
                  Cancel
                </button>
              )}
              <button
                type="submit"
                disabled={isPending || isWebhookLoading || !url.trim()}
                className="h-10 px-8 bg-[#09090b] text-white text-[11px] font-bold tracking-widest uppercase rounded-none hover:bg-[#27272a] disabled:opacity-50 transition-colors flex items-center gap-2"
              >
                {isPending ? <Loader2 size={13} className="animate-spin" /> : <Save size={13} />}
                {editingId ? "Save changes" : "Create webhook"}
              </button>
            </div>
          </form>
        </div>
      </div>
    </PageLayout>
  );
}
