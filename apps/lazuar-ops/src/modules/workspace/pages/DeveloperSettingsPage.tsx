import { useState, useRef } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2, Code, Key, Save, Plus } from "lucide-react";
import { toast } from "sonner";
import { client, type components } from "../../../lib/api-client";
import { useOutletContext } from "react-router-dom";
import PageLayout from "../../core/components/PageLayout";
import QuickCopy from "../../core/components/QuickCopy";
import { cn } from "../../../lib/utils";

type WebhookEndpointDto = components["schemas"]["One.WebhookEndpointDto"];
type CreateWebhookEndpointResponseDto = components["schemas"]["One.CreateWebhookEndpointResponseDto"];

/** Catalog of outbound events currently emitted to workspace endpoints. Empty selection = all events. */
const WEBHOOK_EVENT_OPTIONS = [
  { value: "subscription.activated", label: "Subscription activated", hint: "New paid subscription" },
  { value: "subscription.resumed", label: "Subscription resumed", hint: "Recovered from past due / suspend" },
  { value: "subscription.suspended", label: "Subscription suspended", hint: "Dunning final action" },
  { value: "subscription.canceled", label: "Subscription canceled", hint: "Cancel or dunning cancel" },
  { value: "subscription.past_due", label: "Subscription past due", hint: "Renewal failed" },
  { value: "order.completed", label: "Order completed", hint: "One-time purchase" },
  { value: "payment_link.paid", label: "Payment link paid", hint: "Custom payment link settled" },
  { value: "payment.completed", label: "Payment completed", hint: "M2M / integrator checkout paid" },
  { value: "payment.failed", label: "Payment failed", hint: "M2M / integrator checkout failed at gateway" },
] as const;

export default function DeveloperSettingsPage() {
  const { activeWorkspaceId } = useOutletContext<{ activeWorkspaceId: string }>();
  const queryClient = useQueryClient();

  const [url, setUrl] = useState("");
  const [isActive, setIsActive] = useState(true);
  /** Empty = receive all event types (server contract). */
  const [enabledEvents, setEnabledEvents] = useState<string[]>([]);
  /** Shown only once after create — never re-fetched from GET. */
  const [revealedSecret, setRevealedSecret] = useState<string | null>(null);
  const [editingId, setEditingId] = useState<string | null>(null);
  const formSectionRef = useRef<HTMLDivElement>(null);
  const urlInputRef = useRef<HTMLInputElement>(null);

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
        body: {
          url: url.trim(),
          is_active: isActive,
          enabled_events: enabledEvents,
        },
      });
      if (error) throw new Error(error.detail);
      return data as CreateWebhookEndpointResponseDto;
    },
    onSuccess: (created) => {
      toast.success("Webhook endpoint created. Copy the signing secret now — it will not be shown again.");
      setRevealedSecret(created.secret_key);
      setUrl("");
      setIsActive(true);
      setEnabledEvents([]);
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
        body: {
          url: url.trim(),
          is_active: isActive,
          enabled_events: enabledEvents,
        },
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("Webhook endpoint updated.");
      setUrl("");
      setIsActive(true);
      setEnabledEvents([]);
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

  const focusCreateForm = () => {
    // Form is always mounted below the list; New only reset state before — looked broken.
    requestAnimationFrame(() => {
      formSectionRef.current?.scrollIntoView({ behavior: "smooth", block: "start" });
      urlInputRef.current?.focus({ preventScroll: true });
    });
  };

  const startEdit = (ep: WebhookEndpointDto) => {
    setEditingId(ep.id);
    setUrl(ep.url);
    setIsActive(ep.is_active);
    setEnabledEvents(ep.enabled_events ?? []);
    setRevealedSecret(null);
    focusCreateForm();
  };

  const startCreate = () => {
    setEditingId(null);
    setUrl("");
    setIsActive(true);
    setEnabledEvents([]);
    setRevealedSecret(null);
    focusCreateForm();
  };

  const toggleEvent = (eventValue: string) => {
    setEnabledEvents((prev) =>
      prev.includes(eventValue) ? prev.filter((e) => e !== eventValue) : [...prev, eventValue]
    );
  };

  const selectAllEvents = () => setEnabledEvents(WEBHOOK_EVENT_OPTIONS.map((o) => o.value));
  const clearEventFilter = () => setEnabledEvents([]);

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
                disabled={!activeWorkspaceId}
                className="text-[11px] font-bold uppercase tracking-widest text-[#09090b] flex items-center gap-1 hover:underline disabled:opacity-40 disabled:no-underline"
              >
                <Plus size={12} /> New
              </button>
            </div>

            {!activeWorkspaceId ? (
              <p className="text-[13px] text-rose-600">
                Select a workspace first — webhooks are registered per workspace.
              </p>
            ) : isWebhookLoading ? (
              <div className="flex items-center gap-2 text-[13px] text-[#71717a]">
                <Loader2 size={14} className="animate-spin" /> Loading…
              </div>
            ) : endpoints.length === 0 ? (
              <p className="text-[13px] text-[#71717a]">
                No endpoints yet. Use the form below (or click{" "}
                <button type="button" onClick={startCreate} className="font-semibold text-[#09090b] underline">
                  New
                </button>
                ) — Lazuar will POST signed events when checkouts and subscriptions change.
              </p>
            ) : (
              <ul className="divide-y divide-[#f4f4f5]">
                {endpoints.map((ep) => (
                  <li key={ep.id} className="py-3 flex flex-col sm:flex-row sm:items-start gap-2 sm:gap-4">
                    <div className="flex-1 min-w-0">
                      <div className="font-mono text-[13px] text-[#09090b] truncate">{ep.url}</div>
                      <div className="text-[11px] text-[#71717a] mt-0.5">
                        {ep.is_active ? "Active" : "Disabled"}
                        {ep.has_secret && ep.secret_hint ? ` · secret …${ep.secret_hint}` : ""}
                        {ep.enabled_events?.length
                          ? ` · ${ep.enabled_events.length} event filter(s)`
                          : " · all events"}
                      </div>
                      {ep.enabled_events?.length > 0 && (
                        <div className="flex flex-wrap gap-1 mt-1.5">
                          {ep.enabled_events.map((ev) => (
                            <span
                              key={ev}
                              className="text-[9px] font-mono font-semibold px-1.5 py-0.5 border border-[#e5e5e5] bg-[#fafafa] text-[#52525b]"
                            >
                              {ev}
                            </span>
                          ))}
                        </div>
                      )}
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

        {/* Create / update form — always visible; "New" scrolls here and clears edit mode */}
        <div
          ref={formSectionRef}
          id="webhook-endpoint-form"
          className="bg-white border border-[#e5e5e5] rounded-none flex flex-col scroll-mt-6"
        >
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
                      ref={urlInputRef}
                      type="url"
                      required
                      value={url}
                      onChange={(e) => setUrl(e.target.value)}
                      disabled={isPending || isWebhookLoading || !activeWorkspaceId}
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

              <div className="space-y-3">
                <div className="flex items-center justify-between border-b border-[#f4f4f5] pb-1.5 gap-3">
                  <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a]">
                    Event subscriptions
                  </label>
                  <div className="flex items-center gap-3 shrink-0">
                    <button
                      type="button"
                      onClick={selectAllEvents}
                      disabled={isPending || isWebhookLoading}
                      className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] hover:text-[#09090b] disabled:opacity-50"
                    >
                      Select all
                    </button>
                    <button
                      type="button"
                      onClick={clearEventFilter}
                      disabled={isPending || isWebhookLoading}
                      className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] hover:text-[#09090b] disabled:opacity-50"
                    >
                      All events
                    </button>
                  </div>
                </div>
                <p className="text-[12px] text-[#71717a] leading-relaxed">
                  Leave none selected to receive every event type. Selecting any box filters this endpoint to those
                  events only.
                </p>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-2">
                  {WEBHOOK_EVENT_OPTIONS.map((opt) => {
                    const checked = enabledEvents.includes(opt.value);
                    return (
                      <label
                        key={opt.value}
                        className={cn(
                          "flex items-start gap-2.5 p-3 border cursor-pointer transition-colors",
                          checked
                            ? "border-[#09090b] bg-[#fafafa]"
                            : "border-[#e5e5e5] bg-white hover:border-[#d4d4d8]",
                          (isPending || isWebhookLoading) && "opacity-50 cursor-not-allowed"
                        )}
                      >
                        <input
                          type="checkbox"
                          checked={checked}
                          onChange={() => toggleEvent(opt.value)}
                          disabled={isPending || isWebhookLoading}
                          className="mt-0.5 rounded-sm border-[#e5e5e5] text-[#09090b] focus:ring-[#09090b]"
                        />
                        <span className="min-w-0">
                          <span className="block text-[12px] font-semibold text-[#09090b]">{opt.label}</span>
                          <span className="block font-mono text-[10px] text-[#71717a] mt-0.5">{opt.value}</span>
                          <span className="block text-[10px] text-[#a1a1aa] mt-0.5">{opt.hint}</span>
                        </span>
                      </label>
                    );
                  })}
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
