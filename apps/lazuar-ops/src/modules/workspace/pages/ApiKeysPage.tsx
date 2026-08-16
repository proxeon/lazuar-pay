import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { useOutletContext } from "react-router-dom";
import {
  Loader2,
  Plus,
  Key,
  Trash2,
  ExternalLink,
  AlertTriangle,
  X,
  BookOpen,
} from "lucide-react";
import { toast } from "sonner";
import { client, type components } from "../../../lib/api-client";
import PageLayout from "../../core/components/PageLayout";
import QuickCopy from "../../core/components/QuickCopy";
import { cn } from "../../../lib/utils";

type ApiKeyDto = components["schemas"]["One.ApiKeyDto"];
type GenerateApiKeyResponseDto = components["schemas"]["One.GenerateApiKeyResponseDto"];

const DOCS_BASE = import.meta.env.VITE_DOCS_URL || "/docs";

/** Closed platform scope catalog (must match Modules.One.Domain.PlatformApiScopes). */
const SCOPE_CATALOG = [
  {
    group: "LHDN documents",
    scopes: [
      { id: "lhdn.documents:write", label: "Write", hint: "Submit / cancel documents" },
      { id: "lhdn.documents:read", label: "Read", hint: "Status & TIN validate" },
    ],
  },
  {
    group: "Payments (checkouts)",
    scopes: [
      { id: "payments.checkouts:write", label: "Write", hint: "Create M2M checkouts" },
      { id: "payments.checkouts:read", label: "Read", hint: "Poll checkout status" },
    ],
  },
  {
    group: "Commerce subscriptions",
    scopes: [
      { id: "commerce.subscriptions:read", label: "Read", hint: "List / get Hub subscriptions" },
      { id: "commerce.subscriptions:write", label: "Write", hint: "Cancel (implies read)" },
    ],
  },
  {
    group: "Webhooks",
    scopes: [
      { id: "webhooks.endpoints:manage", label: "Manage endpoints", hint: "Register URLs via API" },
    ],
  },
] as const;

const PRESET_LHDN = ["lhdn.documents:write", "lhdn.documents:read"] as const;
/** Least-privilege Payments integrator (no LHDN). Matches PlatformApiScopes.DefaultAuraIntegratorScopes. */
const PRESET_PAYMENTS_INTEGRATOR = [
  "payments.checkouts:write",
  "payments.checkouts:read",
  "webhooks.endpoints:manage",
] as const;

export default function ApiKeysPage() {
  const { activeWorkspaceId } = useOutletContext<{ activeWorkspaceId: string }>();
  const queryClient = useQueryClient();

  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [name, setName] = useState("");
  const [isTestMode, setIsTestMode] = useState(true);
  const [selectedScopes, setSelectedScopes] = useState<string[]>([]);
  const [createdKey, setCreatedKey] = useState<GenerateApiKeyResponseDto | null>(null);

  const { data: keys, isLoading } = useQuery({
    queryKey: ["developer-api-keys", activeWorkspaceId],
    queryFn: async () => {
      const { data, error } = await client.GET("/one/api-keys");
      if (error) throw new Error(error.detail);
      return (data ?? []) as ApiKeyDto[];
    },
    enabled: !!activeWorkspaceId,
  });

  const createMutation = useMutation({
    mutationFn: async () => {
      const { data, error } = await client.POST("/one/api-keys", {
        body: {
          name: name.trim(),
          is_test_mode: isTestMode,
          scopes: selectedScopes,
        },
      });
      if (error) throw new Error(error.detail);
      return data as GenerateApiKeyResponseDto;
    },
    onSuccess: (data) => {
      setCreatedKey(data);
      setName("");
      setIsTestMode(true);
      setSelectedScopes([]);
      queryClient.invalidateQueries({ queryKey: ["developer-api-keys", activeWorkspaceId] });
      toast.success("API key created. Copy it now — it will not be shown again.");
    },
    onError: (err: Error) => toast.error(err.message || "Failed to create API key."),
  });

  const revokeMutation = useMutation({
    mutationFn: async (id: string) => {
      const { error } = await client.DELETE("/one/api-keys/{id}", {
        params: { path: { id } },
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("API key revoked.");
      queryClient.invalidateQueries({ queryKey: ["developer-api-keys", activeWorkspaceId] });
    },
    onError: (err: Error) => toast.error(err.message || "Failed to revoke API key."),
  });

  const toggleScope = (scopeId: string) => {
    setSelectedScopes((prev) =>
      prev.includes(scopeId) ? prev.filter((s) => s !== scopeId) : [...prev, scopeId]
    );
  };

  const applyPreset = (preset: readonly string[]) => {
    setSelectedScopes([...preset]);
  };

  const handleCreate = (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim()) {
      toast.error("Name is required.");
      return;
    }
    if (selectedScopes.length === 0) {
      toast.error("Select at least one scope (or use a preset).");
      return;
    }
    createMutation.mutate();
  };

  const handleCloseCreate = () => {
    if (createMutation.isPending) return;
    setIsCreateOpen(false);
    setCreatedKey(null);
    setName("");
    setIsTestMode(true);
    setSelectedScopes([]);
  };

  const handleRevoke = (key: ApiKeyDto) => {
    if (
      !window.confirm(
        `Revoke API key “${key.name}” (${key.prefix}…${key.hint})? Integrations using this key will stop working immediately.`
      )
    ) {
      return;
    }
    revokeMutation.mutate(key.id);
  };

  const displayPrefix = (key: ApiKeyDto) => `${key.prefix}…${key.hint}`;

  return (
    <PageLayout
      title="API Keys"
      description="Create and manage secret keys for server-to-server access (LHDN, payments, webhooks). Keys are shown in full only once at creation. Prefer least privilege."
      breadcrumbs={[{ label: "Developer" }, { label: "API Keys" }]}
      actionButton={
        <button
          type="button"
          onClick={() => {
            setCreatedKey(null);
            setIsCreateOpen(true);
          }}
          className="h-9 px-4 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest flex items-center gap-2 hover:bg-[#27272a] transition-colors"
        >
          <Plus size={14} /> Create Key
        </button>
      }
    >
      <div className="space-y-6">
        <div className="flex flex-wrap items-center gap-3 text-[13px]">
          <span className="inline-flex items-center gap-1.5 text-[#71717a]">
            <BookOpen size={14} className="text-[#a1a1aa]" />
            API reference
          </span>
          <a
            href={`${DOCS_BASE}/lhdn`}
            target="_blank"
            rel="noopener noreferrer"
            className="inline-flex items-center gap-1.5 text-[12px] font-semibold text-[#09090b] hover:underline"
          >
            LHDN docs <ExternalLink size={12} className="text-[#a1a1aa]" />
          </a>
          <span className="text-[#d4d4d8]">·</span>
          <a
            href={`${DOCS_BASE}/one`}
            target="_blank"
            rel="noopener noreferrer"
            className="inline-flex items-center gap-1.5 text-[12px] font-semibold text-[#09090b] hover:underline"
          >
            Platform (One) docs <ExternalLink size={12} className="text-[#a1a1aa]" />
          </a>
          <span className="text-[#d4d4d8]">·</span>
          <a
            href={`${DOCS_BASE}/auth`}
            target="_blank"
            rel="noopener noreferrer"
            className="inline-flex items-center gap-1.5 text-[12px] font-semibold text-[#09090b] hover:underline"
          >
            Auth &amp; scopes <ExternalLink size={12} className="text-[#a1a1aa]" />
          </a>
        </div>

        <div className="bg-white border border-[#e5e5e5] rounded-none flex flex-col h-full min-h-[400px]">
          <div className="w-full overflow-x-auto">
            <table className="w-full text-left text-[13px] min-w-[880px]">
              <thead className="bg-[#fafafa] border-b border-[#e5e5e5] select-none">
                <tr>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">
                    Name
                  </th>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">
                    Prefix / Env
                  </th>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">
                    Scopes
                  </th>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">
                    Status
                  </th>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">
                    Created
                  </th>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] text-right">
                    Actions
                  </th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#f4f4f5]">
                {isLoading ? (
                  <tr>
                    <td colSpan={6} className="py-12 text-center text-[#a1a1aa]">
                      <Loader2 className="animate-spin mx-auto" size={20} />
                    </td>
                  </tr>
                ) : !keys || keys.length === 0 ? (
                  <tr>
                    <td colSpan={6} className="py-12 text-center text-[#71717a] text-[13px]">
                      No API keys yet. Create a test or live key to authenticate integrations.
                    </td>
                  </tr>
                ) : (
                  keys.map((key) => (
                    <tr
                      key={key.id}
                      className={cn(
                        "hover:bg-[#fafafa] transition-colors",
                        !key.is_active && "opacity-60"
                      )}
                    >
                      <td className="px-5 py-4">
                        <div className="flex items-center gap-2">
                          <Key size={14} className="text-[#a1a1aa] shrink-0" />
                          <span className="font-medium text-[#09090b]">{key.name}</span>
                        </div>
                      </td>
                      <td className="px-5 py-4">
                        <span className="font-mono text-[12px] text-[#09090b]">
                          {displayPrefix(key)}
                        </span>
                        {key.prefix.startsWith("sk_test") && (
                          <span className="ml-2 inline-flex items-center px-1.5 py-0.5 text-[9px] font-bold uppercase tracking-widest bg-amber-50 text-amber-700 border border-amber-200">
                            Test
                          </span>
                        )}
                        {key.prefix.startsWith("sk_live") && (
                          <span className="ml-2 inline-flex items-center px-1.5 py-0.5 text-[9px] font-bold uppercase tracking-widest bg-emerald-50 text-emerald-700 border border-emerald-200">
                            Live
                          </span>
                        )}
                      </td>
                      <td className="px-5 py-4">
                        <div className="flex flex-wrap gap-1 max-w-[280px]">
                          {(key.scopes ?? []).length === 0 ? (
                            <span className="inline-flex items-center px-1.5 py-0.5 text-[9px] font-bold uppercase tracking-widest bg-amber-50 text-amber-800 border border-amber-200">
                              Legacy unscoped / LHDN default
                            </span>
                          ) : (
                            (key.scopes ?? []).map((scope) => (
                              <span
                                key={scope}
                                className="inline-flex items-center px-1.5 py-0.5 text-[9px] font-mono font-medium bg-[#f4f4f5] text-[#3f3f46] border border-[#e4e4e7]"
                                title={scope}
                              >
                                {scope}
                              </span>
                            ))
                          )}
                        </div>
                      </td>
                      <td className="px-5 py-4">
                        {key.is_active ? (
                          <span className="text-[11px] font-bold uppercase tracking-widest text-emerald-600">
                            Active
                          </span>
                        ) : (
                          <span className="text-[11px] font-bold uppercase tracking-widest text-[#a1a1aa]">
                            Revoked
                          </span>
                        )}
                      </td>
                      <td className="px-5 py-4">
                        <span className="text-[11px] font-mono text-[#71717a]">
                          {new Date(key.created_at).toLocaleString("en-GB", {
                            dateStyle: "short",
                            timeStyle: "medium",
                          })}
                        </span>
                      </td>
                      <td className="px-5 py-4 text-right">
                        {key.is_active && (
                          <button
                            type="button"
                            onClick={() => handleRevoke(key)}
                            disabled={revokeMutation.isPending}
                            className="inline-flex items-center gap-1.5 h-8 px-3 text-[10px] font-bold uppercase tracking-widest text-rose-700 border border-rose-200 bg-rose-50 hover:bg-rose-100 transition-colors disabled:opacity-50"
                          >
                            {revokeMutation.isPending ? (
                              <Loader2 size={12} className="animate-spin" />
                            ) : (
                              <Trash2 size={12} />
                            )}
                            Revoke
                          </button>
                        )}
                      </td>
                    </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
        </div>
      </div>

      {isCreateOpen && (
        <div className="fixed inset-0 z-[60] flex items-center justify-center p-4">
          <div
            className="absolute inset-0 bg-black/20 backdrop-blur-sm transition-opacity"
            onClick={handleCloseCreate}
          />
          <div className="relative bg-white border border-[#e5e5e5] shadow-2xl w-full max-w-lg flex flex-col animate-in fade-in zoom-in-95 duration-200 max-h-[90vh]">
            <div className="flex items-center justify-between p-4 border-b border-[#e5e5e5] bg-[#fafafa]/50 shrink-0">
              <h3 className="text-[13px] font-bold uppercase tracking-widest text-[#09090b]">
                {createdKey ? "API Key Created" : "Create API Key"}
              </h3>
              <button
                type="button"
                onClick={handleCloseCreate}
                disabled={createMutation.isPending}
                className="text-[#a1a1aa] hover:bg-[#e5e5e5] hover:text-[#09090b] transition-colors p-1 disabled:opacity-50"
              >
                <X size={16} />
              </button>
            </div>

            {createdKey ? (
              <div className="p-6 space-y-6 overflow-y-auto">
                <div className="flex items-start gap-3 p-3 border border-amber-200 bg-amber-50">
                  <AlertTriangle size={16} className="text-amber-600 shrink-0 mt-0.5" />
                  <p className="text-[13px] text-amber-900 leading-relaxed">
                    Copy this secret now. For security, Lazuar only shows the full key once. Store it
                    in your vault or environment variables.
                  </p>
                </div>

                <div className="flex items-start gap-3 p-3 border border-amber-200 bg-amber-50">
                  <AlertTriangle size={16} className="text-amber-600 shrink-0 mt-0.5" />
                  <p className="text-[13px] text-amber-900 leading-relaxed">
                    This is a <strong>Lazuar Pay</strong> secret (<span className="font-mono">sk_test_</span>{" "}
                    / <span className="font-mono">sk_live_</span>), not a Stripe secret. Stripe merchant
                    keys use the same prefix. Paste it only into AuraBook{" "}
                    <strong>Guest payments → Lazuar Pay</strong>, or send it as{" "}
                    <span className="font-mono">Authorization: Bearer …</span> to Lazuar Pay. Never drop
                    it into Stripe Dashboard or a Stripe SDK.
                  </p>
                </div>

                <div className="space-y-1.5">
                  <label className="text-[11px] font-semibold text-[#09090b]">Secret key</label>
                  <div className="flex items-center gap-0 border border-[#e5e5e5] bg-[#fafafa]">
                    <div className="flex-1 px-4 py-2.5 font-mono text-[12px] text-[#09090b] overflow-x-auto break-all">
                      {createdKey.plain_key}
                    </div>
                    <div className="border-l border-[#e5e5e5] p-2 bg-white shrink-0">
                      <QuickCopy text={createdKey.plain_key} iconSize={16} />
                    </div>
                  </div>
                </div>

                <div className="grid grid-cols-2 gap-4 text-[12px]">
                  <div>
                    <span className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block mb-1">
                      Name
                    </span>
                    <span className="text-[#09090b] font-medium">{createdKey.name}</span>
                  </div>
                  <div>
                    <span className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block mb-1">
                      Prefix
                    </span>
                    <span className="font-mono text-[#09090b]">
                      {createdKey.prefix}…{createdKey.hint}
                    </span>
                  </div>
                </div>

                {(createdKey.scopes ?? []).length > 0 && (
                  <div>
                    <span className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block mb-2">
                      Scopes
                    </span>
                    <div className="flex flex-wrap gap-1">
                      {(createdKey.scopes ?? []).map((scope) => (
                        <span
                          key={scope}
                          className="inline-flex items-center px-1.5 py-0.5 text-[9px] font-mono font-medium bg-[#f4f4f5] text-[#3f3f46] border border-[#e4e4e7]"
                        >
                          {scope}
                        </span>
                      ))}
                    </div>
                  </div>
                )}

                <div className="flex justify-end pt-2 border-t border-[#f4f4f5]">
                  <button
                    type="button"
                    onClick={handleCloseCreate}
                    className="h-10 px-8 bg-[#09090b] text-white text-[11px] font-bold tracking-widest uppercase rounded-none hover:bg-[#27272a] transition-colors"
                  >
                    Done
                  </button>
                </div>
              </div>
            ) : (
              <form onSubmit={handleCreate} className="flex flex-col min-h-0">
                <div className="p-6 space-y-6 overflow-y-auto">
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-semibold text-[#09090b]">Name *</label>
                    <input
                      type="text"
                      required
                      value={name}
                      onChange={(e) => setName(e.target.value)}
                      disabled={createMutation.isPending}
                      placeholder="e.g. Aura integrator, CI sandbox"
                      className="w-full h-10 border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50"
                    />
                  </div>

                  <div className="space-y-1.5">
                    <label className="text-[11px] font-semibold text-[#09090b]">Environment</label>
                    <select
                      value={isTestMode ? "test" : "live"}
                      onChange={(e) => setIsTestMode(e.target.value === "test")}
                      disabled={createMutation.isPending}
                      className="w-full h-10 border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50"
                    >
                      <option value="test">Test (sk_test_…)</option>
                      <option value="live">Live (sk_live_…)</option>
                    </select>
                    <p className="text-[12px] text-[#71717a] leading-relaxed pt-1">
                      Test keys are for sandbox integrations. Live keys can affect real data —
                      protect them like passwords.
                    </p>
                  </div>

                  <div className="space-y-2">
                    <div className="flex flex-wrap items-center justify-between gap-2">
                      <label className="text-[11px] font-semibold text-[#09090b]">Scopes *</label>
                      <div className="flex flex-wrap gap-2">
                        <button
                          type="button"
                          onClick={() => applyPreset(PRESET_LHDN)}
                          disabled={createMutation.isPending}
                          className="h-7 px-2.5 text-[9px] font-bold uppercase tracking-widest border border-[#e5e5e5] bg-white text-[#09090b] hover:bg-[#fafafa] disabled:opacity-50"
                        >
                          LHDN documents
                        </button>
                        <button
                          type="button"
                          onClick={() => applyPreset(PRESET_PAYMENTS_INTEGRATOR)}
                          disabled={createMutation.isPending}
                          className="h-7 px-2.5 text-[9px] font-bold uppercase tracking-widest border border-[#e5e5e5] bg-white text-[#09090b] hover:bg-[#fafafa] disabled:opacity-50"
                        >
                          Payments integrator
                        </button>
                      </div>
                    </div>
                    <p className="text-[12px] text-[#71717a] leading-relaxed">
                      Keys never mint other keys or write payment-config secrets. API clients must
                      send <span className="font-mono">scopes</span> — there is no default. Choose
                      the minimum product scopes your integration needs.
                    </p>
                    <div className="space-y-3 border border-[#e5e5e5] p-3 bg-[#fafafa]/40">
                      {SCOPE_CATALOG.map((group) => (
                        <div key={group.group}>
                          <p className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] mb-1.5">
                            {group.group}
                          </p>
                          <div className="space-y-1.5">
                            {group.scopes.map((scope) => {
                              const checked = selectedScopes.includes(scope.id);
                              return (
                                <label
                                  key={scope.id}
                                  className={cn(
                                    "flex items-start gap-2.5 p-2 border cursor-pointer transition-colors",
                                    checked
                                      ? "border-[#09090b] bg-white"
                                      : "border-transparent hover:bg-white/80"
                                  )}
                                >
                                  <input
                                    type="checkbox"
                                    checked={checked}
                                    onChange={() => toggleScope(scope.id)}
                                    disabled={createMutation.isPending}
                                    className="mt-0.5"
                                  />
                                  <span className="min-w-0">
                                    <span className="block font-mono text-[11px] text-[#09090b]">
                                      {scope.id}
                                    </span>
                                    <span className="block text-[11px] text-[#71717a]">
                                      {scope.label} — {scope.hint}
                                    </span>
                                  </span>
                                </label>
                              );
                            })}
                          </div>
                        </div>
                      ))}
                    </div>
                  </div>
                </div>

                <div className="flex items-center justify-end gap-3 p-5 border-t border-[#f4f4f5] bg-[#fafafa]/50 shrink-0">
                  <button
                    type="button"
                    onClick={handleCloseCreate}
                    disabled={createMutation.isPending}
                    className="h-10 px-5 border border-[#e5e5e5] bg-white text-[11px] font-bold tracking-widest uppercase text-[#09090b] hover:bg-[#fafafa] disabled:opacity-50 transition-colors"
                  >
                    Cancel
                  </button>
                  <button
                    type="submit"
                    disabled={
                      createMutation.isPending || !name.trim() || selectedScopes.length === 0
                    }
                    className="h-10 px-8 bg-[#09090b] text-white text-[11px] font-bold tracking-widest uppercase rounded-none hover:bg-[#27272a] disabled:opacity-50 transition-colors flex items-center gap-2"
                  >
                    {createMutation.isPending ? (
                      <Loader2 size={13} className="animate-spin" />
                    ) : (
                      <Key size={13} />
                    )}
                    Create Key
                  </button>
                </div>
              </form>
            )}
          </div>
        </div>
      )}
    </PageLayout>
  );
}
