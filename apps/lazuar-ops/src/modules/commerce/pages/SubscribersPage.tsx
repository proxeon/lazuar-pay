import { useState } from "react";
import { useOutletContext } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2, Search, Zap, X, AlertTriangle, Download, ArrowRightCircle, Plus, CalendarClock } from "lucide-react";
import { toast } from "sonner";
import { client, API_URL, type CommerceSubscriptionDto } from "../../../lib/api-client";
import { cn } from "../../../lib/utils";
import PageLayout from "../../core/components/PageLayout";
import SidePanel from "../../core/components/SidePanel";
import QuickCopy from "../../core/components/QuickCopy";
import { useDebounce } from "../../../hooks/use-debounce";
import CreateSubscriberModal from "../components/CreateSubscriberModal";
import RefundModal from "../components/RefundModal";
import { canRefund, remainingAmount, statusBadgeClass, statusLabel } from "../components/transactionStatus";
import type { components } from "../../../lib/api-client";
import type { OpsOutletContext } from "../../../App";

type TransactionLogDto = components["schemas"]["Commerce.TransactionLogDto"];

export default function SubscribersPage() {
  const { activeWorkspaceId, entitlements } = useOutletContext<OpsOutletContext>();
  const workspaceRole = (entitlements.find(e => e.workspace_id === activeWorkspaceId)?.role ?? "").toUpperCase();
  const canWrite = workspaceRole !== "VIEWER";
  const canAnonymize = workspaceRole === "ADMIN" || workspaceRole === "SUPER_ADMIN";
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [statusFilter, setStatusFilter] = useState("ALL");
  const [searchTerm, setSearchTerm] = useState("");
  const debouncedSearchTerm = useDebounce(searchTerm, 300);

  const [selectedSub, setSelectedSub] = useState<CommerceSubscriptionDto | null>(null);
  const [isExporting, setIsExporting] = useState(false);
  const [activeAction, setActiveAction] = useState<string | null>(null);
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);

  const [isPaymentModalOpen, setIsPaymentModalOpen] = useState(false);
  const [paymentAmount, setPaymentAmount] = useState("");
  const [paymentRef, setPaymentRef] = useState("");
  const [paymentMethod, setPaymentMethod] = useState("BANK_TRANSFER");

  const [refundTransaction, setRefundTransaction] = useState<TransactionLogDto | null>(null);
  const [pauseDunningModal, setPauseDunningModal] = useState({ isOpen: false, date: "" });
  const [pauseCollectionModal, setPauseCollectionModal] = useState({ isOpen: false, date: "" });
  const [planProductId, setPlanProductId] = useState("");
  const [seatQty, setSeatQty] = useState("1");

  const { data: products } = useQuery({
    queryKey: ["commerce-products"],
    queryFn: async () => {
      const { data, error } = await client.GET("/admin/commerce/products");
      if (error) throw new Error(error.detail);
      return data;
    },
    enabled: !!activeWorkspaceId
  });

  const { data: subscribersData, isLoading } = useQuery({
    queryKey: ["commerce-subscribers", page, debouncedSearchTerm],
    queryFn: async () => {
      const { data, error } = await client.GET("/admin/commerce/subscribers", {
        params: { query: { page, limit: 50, search: debouncedSearchTerm || undefined } }
      });
      if (error) throw new Error(error.detail);
      return data;
    },
    enabled: !!activeWorkspaceId
  });

  const { data: paymentsData, isLoading: isPaymentsLoading } = useQuery({
    queryKey: ["commerce-payments", selectedSub?.id],
    queryFn: async () => {
      if (!selectedSub) return null;
      const { data, error } = await client.GET("/admin/commerce/transactions", {
        params: { query: { page: 1, limit: 20, subscription_id: selectedSub.id } }
      });
      if (error) throw new Error(error.detail);
      return data;
    },
    enabled: !!selectedSub && !!activeWorkspaceId,
    refetchInterval: (query) => {
      const rows = query.state.data?.data ?? [];
      return rows.some((tx) => tx.status === "REFUND_PENDING") ? 2000 : false;
    }
  });

  const handleExport = async () => {
    setIsExporting(true);
    try {
      const response = await fetch(`${API_URL}/admin/commerce/subscribers/export`, {
        headers: { "X-Tenant-Id": localStorage.getItem("ops_active_workspace_id") || "" },
        credentials: "include",
      });
      if (!response.ok) throw new Error("Export request failed");
      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `Subscribers_Export_${new Date().toISOString().slice(0,10)}.csv`;
      document.body.appendChild(a);
      a.click();
      a.remove();
      window.URL.revokeObjectURL(url);
    } catch {
      toast.error("Failed to export data.");
    } finally {
      setIsExporting(false);
    }
  };

  const actionMutation = useMutation({
    mutationFn: async ({ action, payload }: { action: string, payload?: any }) => {
      if (!selectedSub) throw new Error("No subscriber selected.");

      if (action === "cancel") {
        const { error } = await client.POST("/admin/commerce/subscribers/{id}/cancel", {
          params: { path: { id: selectedSub.id } },
          body: { at_period_end: payload?.at_period_end === true },
        });
        if (error) throw new Error(error.detail || "Cancel failed");
        return;
      }

      if (action === "keep") {
        const { error } = await client.POST("/admin/commerce/subscribers/{id}/keep", {
          params: { path: { id: selectedSub.id } },
        });
        if (error) throw new Error(error.detail || "Keep failed");
        return;
      }

      if (action === "record-payment") {
        const { error } = await client.POST("/admin/commerce/subscribers/{id}/record-payment", {
          params: { path: { id: selectedSub.id } },
          body: {
            amount: payload.amount,
            payment_method: payload.payment_method,
            reference_number: payload.reference_number || undefined,
          },
        });
        if (error) throw new Error(error.detail || "Record payment failed");
        return;
      }

      if (action === "change-plan") {
        const response = await fetch(`${API_URL}/admin/commerce/subscribers/${selectedSub.id}/change-plan`, {
          method: "POST",
          credentials: "include",
          headers: {
            "Content-Type": "application/json",
            "X-Tenant-Id": localStorage.getItem("ops_active_workspace_id") || "",
          },
          body: JSON.stringify({ product_id: payload?.product_id ?? null }),
        });
        if (!response.ok) {
          const body = await response.json().catch(() => null);
          throw new Error(body?.status || body?.detail || "Change plan failed");
        }
        return response.json();
      }

      if (action === "quantity") {
        const response = await fetch(`${API_URL}/admin/commerce/subscribers/${selectedSub.id}/quantity`, {
          method: "POST",
          credentials: "include",
          headers: {
            "Content-Type": "application/json",
            "X-Tenant-Id": localStorage.getItem("ops_active_workspace_id") || "",
          },
          body: JSON.stringify({ quantity: payload.quantity }),
        });
        if (!response.ok) {
          const body = await response.json().catch(() => null);
          throw new Error(body?.status || body?.detail || "Set seats failed");
        }
        return response.json();
      }

      if (action === "collection/pause") {
        const response = await fetch(`${API_URL}/admin/commerce/subscribers/${selectedSub.id}/collection/pause`, {
          method: "POST",
          credentials: "include",
          headers: {
            "Content-Type": "application/json",
            "X-Tenant-Id": localStorage.getItem("ops_active_workspace_id") || "",
          },
          body: JSON.stringify({ resume_on: payload.resume_on }),
        });
        if (!response.ok) {
          const body = await response.json().catch(() => null);
          throw new Error(body?.status || body?.detail || "Pause collection failed");
        }
        return;
      }

      if (action === "collection/resume") {
        const response = await fetch(`${API_URL}/admin/commerce/subscribers/${selectedSub.id}/collection/resume`, {
          method: "POST",
          credentials: "include",
          headers: {
            "X-Tenant-Id": localStorage.getItem("ops_active_workspace_id") || "",
          },
        });
        if (!response.ok) {
          const body = await response.json().catch(() => null);
          throw new Error(body?.status || body?.detail || "Resume collection failed");
        }
        return;
      }

      if (action === "dunning/pause") {
        const { error } = await client.POST("/admin/commerce/subscribers/{id}/dunning/pause", {
          params: { path: { id: selectedSub.id } },
          body: { pause_until: payload.pause_until },
        });
        if (error) throw new Error(error.detail || "Pause dunning failed");
        return;
      }

      if (action === "dunning/resume") {
        const { error } = await client.POST("/admin/commerce/subscribers/{id}/dunning/resume", {
          params: { path: { id: selectedSub.id } },
        });
        if (error) throw new Error(error.detail || "Resume dunning failed");
        return;
      }

      if (action === "anonymize") {
        const { error } = await client.POST("/admin/commerce/subscribers/{id}/anonymize", {
          params: { path: { id: selectedSub.id } },
        });
        if (error) throw new Error(error.detail || "Anonymize failed");
        return;
      }

      throw new Error(`Unknown subscriber action: ${action}`);
    },
    onMutate: (variables) => {
      const actionKey = variables.action;
      setActiveAction(actionKey);
    },
    onSettled: () => {
      setActiveAction(null);
    },
    onSuccess: async (_, variables) => {
      toast.success(`Action successfully executed.`);
      await queryClient.invalidateQueries({ queryKey: ["commerce-subscribers"] });
      await queryClient.invalidateQueries({ queryKey: ["commerce-payments"] });
      await queryClient.invalidateQueries({ queryKey: ["commerce-stats"] });

      if (variables.action === "record-payment") {
        setIsPaymentModalOpen(false);
        setPaymentAmount("");
        setPaymentRef("");
        const cached = queryClient.getQueriesData<{ data?: CommerceSubscriptionDto[] }>({ queryKey: ["commerce-subscribers"] });
        for (const [, data] of cached) {
          const match = data?.data?.find((s) => s.id === selectedSub?.id);
          if (match) {
            setSelectedSub(match);
            break;
          }
        }
      } else if (variables.action === "collection/pause") {
        setPauseCollectionModal({ isOpen: false, date: "" });
        setSelectedSub(prev => prev ? { ...prev, collection_paused_until: new Date(variables.payload.resume_on).toISOString() } : null);
      } else if (variables.action === "collection/resume") {
        setSelectedSub(prev => prev ? { ...prev, collection_paused_until: undefined } : null);
      } else if (variables.action === "change-plan" || variables.action === "quantity") {
        const cached = queryClient.getQueriesData<{ data?: CommerceSubscriptionDto[] }>({ queryKey: ["commerce-subscribers"] });
        for (const [, data] of cached) {
          const match = data?.data?.find((s) => s.id === selectedSub?.id);
          if (match) {
            setSelectedSub(match);
            break;
          }
        }
      } else if (variables.action === "dunning/pause") {
        setPauseDunningModal({ isOpen: false, date: "" });
        setSelectedSub(prev => prev ? { ...prev, dunning_paused_until: new Date(variables.payload.pause_until).toISOString() } : null);
      } else if (variables.action === "dunning/resume") {
        setSelectedSub(prev => prev ? { ...prev, dunning_paused_until: undefined } : null);
      } else if (variables.action === "cancel") {
        if (variables.payload?.at_period_end) {
          setSelectedSub(prev => prev ? { ...prev, cancel_at_period_end: true } : null);
        } else {
          setSelectedSub(prev => prev ? { ...prev, status: "CANCELED", cancel_at_period_end: false } : null);
        }
      } else if (variables.action === "keep") {
        setSelectedSub(prev => prev ? { ...prev, cancel_at_period_end: false } : null);
      } else if (variables.action === "anonymize") {
        setSelectedSub(prev => prev ? {
          ...prev,
          customer_name: "Anonymized User",
          customer_email: `deleted_${prev.client_profile_id}@localhost`,
          customer_phone: "",
          status: "CANCELED",
          cancel_at_period_end: false,
        } : null);
      }
    },
    onError: (err: any) => toast.error("Action Failed", { description: err.message })
  });

  const displayedSubscribers = (subscribersData?.data || []).filter(sub => statusFilter === "ALL" || sub.status === statusFilter);

  return (
    <PageLayout 
      title="Subscriber Directory" 
      description="Manage active members, billing cycles, and access."
      breadcrumbs={[{ label: "Commerce", href: "/commerce/dashboard" }, { label: "Subscribers" }]}
      actionButton={
        <div className="flex items-center gap-2">
          {canWrite && (
          <button 
            onClick={handleExport} 
            disabled={isExporting}
            className="h-9 px-4 bg-white border border-[#e5e5e5] text-[#09090b] text-[11px] font-bold uppercase tracking-widest flex items-center gap-2 hover:bg-[#f4f4f5] transition-colors disabled:opacity-50"
          >
            {isExporting ? <Loader2 size={14} className="animate-spin" /> : <Download size={14} />} 
            Export CSV
          </button>
          )}
          {canWrite && (
          <button 
            onClick={() => setIsCreateModalOpen(true)}
            className="h-9 px-4 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest flex items-center gap-2 hover:bg-[#27272a] transition-colors"
          >
            <Plus size={14} /> Add Member
          </button>
          )}
        </div>
      }
    >
      <div className="bg-white border border-[#e5e5e5] rounded-none flex flex-col h-full">
        <div className="px-5 py-4 border-b border-[#f4f4f5] flex items-center justify-between bg-[#fafafa]/50">
          <div className="relative w-64">
            <Search size={14} className="absolute left-3 top-2 text-[#a1a1aa]" />
            <input 
              type="text" 
              placeholder="Search by name or email..." 
              value={searchTerm}
              onChange={e => setSearchTerm(e.target.value)}
              className="w-full h-8 pl-9 pr-3 text-[12px] bg-white border border-[#e5e5e5] focus:outline-none focus:border-[#09090b]" 
            />
          </div>
          <select 
            value={statusFilter} 
            onChange={(e) => setStatusFilter(e.target.value)}
            className="h-8 px-2 text-[10px] font-bold uppercase tracking-widest bg-white border border-[#e5e5e5] text-[#09090b] focus:outline-none focus:border-[#09090b]"
          >
            <option value="ALL">ALL STATUSES</option>
            <option value="ACTIVE">ACTIVE</option>
            <option value="TRIALING">TRIALING</option>
            <option value="PAST_DUE">PAST DUE</option>
            <option value="CANCELED">CANCELED</option>
            <option value="SUSPENDED">SUSPENDED</option>
          </select>
        </div>

        <div className="w-full overflow-x-auto min-h-[500px]">
          <table className="w-full text-left text-[13px] min-w-[800px]">
            <thead className="bg-white border-b border-[#f4f4f5]">
              <tr>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Customer</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Product</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Status</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] text-right">Paid through / Next due</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#f4f4f5]">
              {isLoading ? (
                <tr><td colSpan={4} className="py-12 text-center text-[#a1a1aa]"><Loader2 size={20} className="animate-spin mx-auto" /></td></tr>
              ) : displayedSubscribers.length === 0 ? (
                <tr><td colSpan={4} className="py-12 text-center text-[12px] text-[#71717a]">No subscribers found.</td></tr>
              ) : (
                displayedSubscribers.map((sub) => (
                  <tr key={sub.id} onClick={() => { setSelectedSub(sub); setSeatQty(String(sub.quantity ?? 1)); setPlanProductId(""); }} className="hover:bg-[#fafafa] transition-colors cursor-pointer group">
                    <td className="px-5 py-3.5 min-w-[200px]">
                      <div className="flex items-center gap-2">
                        <p className="font-medium text-[#09090b] text-[13px] group-hover:text-blue-600 transition-colors">{sub.customer_name}</p>
                        {sub.is_reminder_only ? (
                          <span className="text-[9px] px-1.5 py-0.5 border font-bold uppercase tracking-widest bg-amber-50 text-amber-800 border-amber-200">Reminder-only</span>
                        ) : sub.vaulted_token_id ? (
                          <Zap size={12} className="text-blue-500" title="Auto-Debit Enabled" />
                        ) : null}
                      </div>
                      <div className="flex items-center gap-1.5 mt-0.5">
                        <p className="text-[11px] text-[#71717a]">{sub.customer_email}</p>
                        <QuickCopy text={sub.customer_email} iconSize={10} className="opacity-0 group-hover:opacity-100 p-0.5" />
                      </div>
                    </td>
                    <td className="px-5 py-3.5">
                      <p className="text-[12px] text-[#09090b]">{sub.product_name}</p>
                      <p className="text-[10px] font-mono text-[#71717a]">RM {sub.product_price?.toFixed(2)}</p>
                    </td>
                    <td className="px-5 py-3.5">
                      <div className="flex items-center gap-1.5">
                        <span className={cn(
                          "text-[9px] px-1.5 py-0.5 border font-bold uppercase tracking-widest whitespace-nowrap",
                          sub.status === "ACTIVE" ? "bg-emerald-50 text-emerald-700 border-emerald-200" :
                          sub.status === "PAST_DUE" ? "bg-rose-50 text-rose-700 border-rose-200" :
                          "bg-zinc-100 text-zinc-600 border-zinc-200"
                        )}>
                          {sub.status.replace("_", " ")}
                        </span>
                        {sub.status === "TRIALING" && (
                          <span className="text-[9px] px-1.5 py-0.5 border font-bold uppercase tracking-widest whitespace-nowrap bg-sky-50 text-sky-800 border-sky-200">
                            Trial
                          </span>
                        )}
                        {sub.cancel_at_period_end && (
                          <span className="text-[9px] px-1.5 py-0.5 border font-bold uppercase tracking-widest whitespace-nowrap bg-amber-50 text-amber-800 border-amber-200">
                            Cancels
                          </span>
                        )}
                        {sub.collection_paused_until && new Date(sub.collection_paused_until).getTime() > Date.now() && (
                          <span className="text-[9px] px-1.5 py-0.5 border font-bold uppercase tracking-widest whitespace-nowrap bg-zinc-100 text-zinc-700 border-zinc-200">
                            Collection paused
                          </span>
                        )}
                        {sub.pending_product_name && (
                          <span className="text-[9px] px-1.5 py-0.5 border font-bold uppercase tracking-widest whitespace-nowrap bg-blue-50 text-blue-800 border-blue-200">
                            → {sub.pending_product_name}
                          </span>
                        )}
                        {sub.pending_quantity != null && (
                          <span className="text-[9px] px-1.5 py-0.5 border font-bold uppercase tracking-widest whitespace-nowrap bg-blue-50 text-blue-800 border-blue-200">
                            Seats → {sub.pending_quantity}
                          </span>
                        )}
                      </div>
                    </td>
                    <td className="px-5 py-3.5 text-right font-mono text-[#52525b] text-[11px] whitespace-nowrap">
                      {sub.next_billing_date ? new Date(sub.next_billing_date).toLocaleDateString('en-GB') : '-'}
                      <ArrowRightCircle size={14} className="inline ml-3 text-[#d4d4d8] group-hover:text-[#09090b] transition-colors" />
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      <SidePanel
        isOpen={!!selectedSub}
        onClose={() => setSelectedSub(null)}
        title="Member Console"
        subtitle={selectedSub ? `ID: ${selectedSub.id.toUpperCase()}` : ""}
        disableOutsideClick={activeAction !== null}
      >
        {selectedSub && (
          <div className="space-y-8 animate-in fade-in duration-200">
            <div className="space-y-4">
              <h3 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Customer Profile</h3>
              <div className="space-y-3 text-[12px]">
                <div>
                  <span className="text-[#a1a1aa] block mb-0.5">Name</span>
                  <span className="font-semibold text-[#09090b] text-[13px]">{selectedSub.customer_name}</span>
                </div>
                <div>
                  <span className="text-[#a1a1aa] block mb-0.5">Email Address</span>
                  <div className="flex items-center gap-2">
                    <a href={`mailto:${selectedSub.customer_email}`} className="font-medium text-blue-600 hover:opacity-85 transition-opacity underline underline-offset-2">
                      {selectedSub.customer_email}
                    </a>
                    <QuickCopy text={selectedSub.customer_email} iconSize={11} className="hover:bg-[#fafafa]" />
                  </div>
                </div>
                <div>
                  <span className="text-[#a1a1aa] block mb-0.5">Phone Number</span>
                  <div className="flex items-center gap-2">
                    {selectedSub.customer_phone ? (
                      <>
                        <a href={`tel:${selectedSub.customer_phone.replace(/[^0-9+]/g, "")}`} className="font-mono text-[#52525b] hover:text-blue-600 transition-colors underline underline-offset-2">
                          {selectedSub.customer_phone}
                        </a>
                        <QuickCopy text={selectedSub.customer_phone} iconSize={11} className="hover:bg-[#fafafa]" />
                        <a href={`https://wa.me/${selectedSub.customer_phone.replace(/[^0-9]/g, "")}`} target="_blank" rel="noopener noreferrer" className="ml-1 text-[10px] font-bold uppercase tracking-wider text-emerald-600 hover:text-emerald-700 transition-colors">
                          WhatsApp
                        </a>
                      </>
                    ) : (
                      <span className="text-[#a1a1aa] italic">Not Provided</span>
                    )}
                  </div>
                </div>
              </div>
            </div>

            <div className="space-y-4">
              <h3 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Subscription Details</h3>
              <div className="grid grid-cols-2 gap-4 text-[12px]">
                <div>
                  <span className="text-[#a1a1aa] block mb-1">Product</span>
                  <span className="font-medium text-[#09090b]">{selectedSub.product_name}</span>
                  {selectedSub.pending_product_name && (
                    <span className="block text-[10px] text-blue-700 mt-0.5">Changes to {selectedSub.pending_product_name} on next bill. No charge today.</span>
                  )}
                </div>
                <div>
                  <span className="text-[#a1a1aa] block mb-1">Status</span>
                  <span className="font-bold text-[#09090b]">
                    {selectedSub.status.replace("_", " ")}
                    {selectedSub.cancel_at_period_end ? " · cancels at period end" : ""}
                  </span>
                </div>
                <div>
                  <span className="text-[#a1a1aa] block mb-1">Paid through / Next due</span>
                  <span className="font-mono text-[#52525b]">{selectedSub.next_billing_date ? new Date(selectedSub.next_billing_date).toLocaleDateString() : '-'}</span>
                  {selectedSub.current_period_end && (
                    <span className="block text-[10px] text-[#a1a1aa] mt-0.5">Period ends {new Date(selectedSub.current_period_end).toLocaleDateString()}</span>
                  )}
                </div>
                <div><span className="text-[#a1a1aa] block mb-1">Seats</span><span className="font-medium text-[#09090b]">{selectedSub.quantity ?? 1}{selectedSub.pending_quantity != null ? ` → ${selectedSub.pending_quantity}` : ""}</span></div>
                {selectedSub.trial_ends_at && (
                  <div><span className="text-[#a1a1aa] block mb-1">Trial ends</span><span className="font-mono text-[#52525b]">{new Date(selectedSub.trial_ends_at).toLocaleDateString()}</span></div>
                )}
                {selectedSub.collection_paused_until && (
                  <div className="col-span-2"><span className="text-[#a1a1aa] block mb-1">Collection paused until</span><span className="font-mono text-[#52525b]">{new Date(selectedSub.collection_paused_until).toLocaleDateString()}</span></div>
                )}
                <div><span className="text-[#a1a1aa] block mb-1">Auto-Debit</span><span className="font-medium text-[#09090b]">{selectedSub.is_reminder_only ? "Reminder-only (pay link / record payment)" : selectedSub.vaulted_token_id ? "Auto-debit active" : "None"}</span></div>
                {selectedSub.current_renewal_checkout_url && (
                  <div className="col-span-2">
                    <span className="text-[#a1a1aa] block mb-1">Pay this cycle</span>
                    <div className="flex items-center gap-2">
                      <a href={selectedSub.current_renewal_checkout_url} target="_blank" rel="noopener noreferrer" className="font-mono text-[11px] text-blue-600 hover:opacity-85 underline underline-offset-2 truncate">
                        {selectedSub.current_renewal_checkout_url}
                      </a>
                      <QuickCopy text={selectedSub.current_renewal_checkout_url} iconSize={11} className="hover:bg-[#fafafa]" />
                    </div>
                  </div>
                )}
              </div>
            </div>

            {selectedSub.status === "PAST_DUE" && (
              <div className="space-y-4">
                <h3 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Revenue Recovery (Dunning)</h3>
                <div className="bg-amber-50 border border-amber-200 p-4 space-y-3 rounded-sm">
                  <div className="flex items-center gap-2 text-amber-800 font-bold text-[12px]">
                    <AlertTriangle size={14} /> Payment is Past Due
                  </div>
                  <div className="grid grid-cols-2 gap-2 text-[11px]">
                    <div>
                      <span className="text-amber-700/80 block">Active Campaign</span>
                      <span className="font-bold text-amber-900">{selectedSub.dunning_campaign_name || "None (Will not escalate)"}</span>
                    </div>
                    <div>
                      <span className="text-amber-700/80 block">Current Step</span>
                      <span className="font-bold text-amber-900">{selectedSub.current_dunning_step !== undefined ? `Step ${selectedSub.current_dunning_step}` : "N/A"}</span>
                    </div>
                  </div>
                  
                  {selectedSub.dunning_paused_until && new Date(selectedSub.dunning_paused_until).getTime() > Date.now() ? (
                    <div className="pt-2 border-t border-amber-200/50 flex items-center justify-between">
                      <div className="text-[10px] text-amber-800">
                        <span className="font-bold">Automations Paused</span> until {new Date(selectedSub.dunning_paused_until).toLocaleDateString()}
                      </div>
                      <button 
                        onClick={() => actionMutation.mutate({ action: "dunning/resume" })}
                        disabled={activeAction !== null}
                        className="text-[10px] font-bold uppercase tracking-widest bg-white border border-amber-200 text-amber-700 px-2 py-1 hover:bg-amber-100 transition-colors"
                      >
                        {activeAction === "dunning/resume" ? <Loader2 size={12} className="animate-spin" /> : "Resume"}
                      </button>
                    </div>
                  ) : (
                    <div className="pt-2 border-t border-amber-200/50">
                      <button 
                        onClick={() => setPauseDunningModal({ isOpen: true, date: "" })}
                        disabled={activeAction !== null}
                        className="w-full flex items-center justify-center gap-2 text-[10px] font-bold uppercase tracking-widest bg-white border border-amber-200 text-amber-700 h-8 hover:bg-amber-100 transition-colors"
                      >
                        <CalendarClock size={12} /> Pause recovery
                      </button>
                    </div>
                  )}
                </div>
              </div>
            )}

            {canWrite && (selectedSub.status === "ACTIVE" || selectedSub.status === "TRIALING") && (
              <div className="space-y-3">
                <h3 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Plan &amp; seats</h3>
                <p className="text-[11px] text-[#71717a]">No charge today. Changes start on the next billing date.</p>
                <div className="flex gap-2">
                  <select
                    value={planProductId}
                    onChange={(e) => setPlanProductId(e.target.value)}
                    className="flex-1 h-8 border border-[#e5e5e5] bg-white px-2 text-[12px]"
                  >
                    <option value="">Keep current / revert pending</option>
                    {(products || [])
                      .filter((p) => p.is_active && (p.interval === "mo" || p.interval === "yr") && p.id !== selectedSub.product_id)
                      .map((p) => (
                        <option key={p.id} value={p.id}>{p.name} · {p.interval} · RM {p.price}</option>
                      ))}
                  </select>
                  <button
                    onClick={() => actionMutation.mutate({ action: "change-plan", payload: { product_id: planProductId || null } })}
                    disabled={activeAction !== null}
                    className="h-8 px-3 border border-[#e5e5e5] text-[10px] font-bold uppercase tracking-widest"
                  >
                    {planProductId ? "Schedule" : "Revert"}
                  </button>
                </div>
                <div className="flex gap-2">
                  <input
                    type="number"
                    min={1}
                    max={99}
                    value={seatQty}
                    onChange={(e) => setSeatQty(e.target.value)}
                    className="w-20 h-8 border border-[#e5e5e5] px-2 text-[12px]"
                  />
                  <button
                    onClick={() => actionMutation.mutate({ action: "quantity", payload: { quantity: Number(seatQty) } })}
                    disabled={activeAction !== null}
                    className="h-8 px-3 border border-[#e5e5e5] text-[10px] font-bold uppercase tracking-widest"
                  >
                    Set seats
                  </button>
                </div>
                {selectedSub.status === "ACTIVE" && (
                  selectedSub.collection_paused_until && new Date(selectedSub.collection_paused_until).getTime() > Date.now() ? (
                    <button
                      onClick={() => actionMutation.mutate({ action: "collection/resume" })}
                      disabled={activeAction !== null}
                      className="h-8 w-full border border-[#e5e5e5] text-[10px] font-bold uppercase tracking-widest"
                    >
                      Resume collection
                    </button>
                  ) : (
                    <button
                      onClick={() => setPauseCollectionModal({ isOpen: true, date: "" })}
                      disabled={activeAction !== null}
                      className="h-8 w-full border border-[#e5e5e5] text-[10px] font-bold uppercase tracking-widest"
                    >
                      Pause collection
                    </button>
                  )
                )}
              </div>
            )}

            <div className="space-y-4">
              <h3 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Operations</h3>
              <div className="grid grid-cols-2 gap-2">
                {selectedSub.current_renewal_checkout_url && selectedSub.is_reminder_only && (selectedSub.status === "PAST_DUE" || selectedSub.status === "SUSPENDED") && (
                  <button
                    onClick={async () => {
                      try {
                        await navigator.clipboard.writeText(selectedSub.current_renewal_checkout_url!);
                        toast.success("Pay link copied to clipboard");
                      } catch {
                        toast.error("Could not copy pay link");
                      }
                    }}
                    disabled={activeAction !== null}
                    className="h-8 col-span-2 border border-amber-200 bg-amber-50 text-[10px] font-bold uppercase tracking-widest text-amber-800 hover:bg-amber-100 transition-colors disabled:opacity-50"
                  >
                    Copy pay link
                  </button>
                )}
                <button onClick={() => {
                  setPaymentAmount(selectedSub.product_price != null ? String(selectedSub.product_price) : "");
                  setPaymentMethod("BANK_TRANSFER");
                  setPaymentRef("");
                  setIsPaymentModalOpen(true);
                }} disabled={activeAction !== null || selectedSub.status === "CANCELED"} className="h-8 border border-[#e5e5e5] bg-white text-[10px] font-bold uppercase tracking-widest text-[#09090b] hover:bg-[#f4f4f5] transition-colors disabled:opacity-50">Log Payment</button>
                <button onClick={() => { if (window.confirm("Cancel this subscription immediately? Access ends now.")) actionMutation.mutate({ action: "cancel", payload: { at_period_end: false } }); }} disabled={activeAction !== null || selectedSub.status === "CANCELED"} className="h-8 border border-amber-200 bg-amber-50 text-[10px] font-bold uppercase tracking-widest text-amber-700 hover:bg-amber-100 transition-colors disabled:opacity-50 flex items-center justify-center gap-1.5">
                  {activeAction === "cancel" && <Loader2 size={12} className="animate-spin" />} Cancel Sub
                </button>
                {(selectedSub.status === "ACTIVE" || selectedSub.status === "TRIALING") && selectedSub.next_billing_date && new Date(selectedSub.next_billing_date).getTime() > Date.now() && !selectedSub.cancel_at_period_end && (
                  <button onClick={() => { if (window.confirm("Cancel at period end? Access continues until the paid-through date.")) actionMutation.mutate({ action: "cancel", payload: { at_period_end: true } }); }} disabled={activeAction !== null} className="h-8 col-span-2 border border-amber-200 bg-white text-[10px] font-bold uppercase tracking-widest text-amber-700 hover:bg-amber-50 transition-colors disabled:opacity-50 flex items-center justify-center gap-1.5">
                    {activeAction === "cancel" && <Loader2 size={12} className="animate-spin" />} Cancel at period end
                  </button>
                )}
                {selectedSub.cancel_at_period_end && selectedSub.status !== "CANCELED" && (
                  <button onClick={() => actionMutation.mutate({ action: "keep" })} disabled={activeAction !== null} className="h-8 col-span-2 border border-emerald-200 bg-emerald-50 text-[10px] font-bold uppercase tracking-widest text-emerald-800 hover:bg-emerald-100 transition-colors disabled:opacity-50 flex items-center justify-center gap-1.5">
                    {activeAction === "keep" && <Loader2 size={12} className="animate-spin" />} Keep plan
                  </button>
                )}
                {canAnonymize && (
                <button
                  onClick={() => {
                    if (!selectedSub) return;
                    const ok = window.confirm(
                      `Anonymize ${selectedSub.customer_email}?\n\nThis cannot be undone. Subscriptions cancel. Emails stop. Official receipts and MyInvois submissions keep the buyer identity that was filed.`,
                    );
                    if (ok) actionMutation.mutate({ action: "anonymize" });
                  }}
                  disabled={activeAction !== null}
                  className="h-8 col-span-2 border border-rose-200 bg-rose-50 text-[10px] font-bold uppercase tracking-widest text-rose-700 hover:bg-rose-100 transition-colors disabled:opacity-50 flex items-center justify-center gap-1.5"
                >
                  {activeAction === "anonymize" && <Loader2 size={12} className="animate-spin" />} Anonymize
                </button>
                )}
                {!selectedSub.is_reminder_only && (
                  <button
                    onClick={async () => {
                      if (!selectedSub) return;
                      setActiveAction("portal-link");
                      try {
                        const returnUrl = window.location.origin;
                        const { data, error } = await client.POST("/admin/commerce/subscribers/portal-link", {
                          body: { customer_email: selectedSub.customer_email, return_url: returnUrl },
                        });
                        if (error) throw new Error(error.detail || "Failed to generate portal link");
                        if (data?.url) {
                          await navigator.clipboard.writeText(data.url);
                          toast.success("Stripe portal link copied to clipboard");
                        }
                      } catch (err: any) {
                        toast.error("Portal link failed", { description: err.message });
                      } finally {
                        setActiveAction(null);
                      }
                    }}
                    disabled={activeAction !== null || !selectedSub.customer_email}
                    className="h-8 col-span-2 border border-[#e5e5e5] bg-white text-[10px] font-bold uppercase tracking-widest text-[#09090b] hover:bg-[#f4f4f5] transition-colors disabled:opacity-50 flex items-center justify-center gap-1.5"
                  >
                    {activeAction === "portal-link" && <Loader2 size={12} className="animate-spin" />} Copy Portal Link
                  </button>
                )}
              </div>
            </div>

            <div className="space-y-4">
              <h3 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Payment Ledger</h3>
              {isPaymentsLoading ? (
                <div className="flex justify-center p-4"><Loader2 className="animate-spin text-[#a1a1aa]" size={16} /></div>
              ) : (
                <div className="space-y-2">
                  {paymentsData?.data?.map((payment) => {
                    const refundable = canRefund(payment);
                    return (
                      <div key={payment.id} className="p-3 border border-[#e5e5e5] bg-[#fafafa] flex items-center justify-between rounded-sm">
                        <div className="flex flex-col gap-1">
                          <p className="text-[12px] font-bold text-[#09090b]">RM {payment.amount.toFixed(2)} <span className="font-normal text-[#71717a]">via {payment.recorded_by_name || "GATEWAY"}{payment.gateway_name ? ` · ${payment.gateway_name}` : ""}</span></p>
                          <div className="flex items-center gap-2">
                            <span className="text-[10px] font-mono font-bold text-[#09090b]">{payment.id.substring(0,8)}</span>
                            <span className={cn("text-[9px] px-1.5 py-0.5 border font-bold uppercase tracking-widest", statusBadgeClass(payment.status))}>
                              {statusLabel(payment.status, payment.refunded_amount)}
                            </span>
                          </div>
                          {(payment.refunded_amount ?? 0) > 0 && payment.status !== "REFUNDED" && (
                            <p className="text-[10px] font-mono text-amber-700">Remaining RM {remainingAmount(payment).toFixed(2)}</p>
                          )}
                          <p className="text-[10px] font-mono text-[#a1a1aa] mt-0.5">{new Date(payment.created_at).toLocaleString('en-GB')}</p>
                        </div>
                        {payment.status === "REFUND_PENDING" && (
                          <span className="text-[10px] font-bold uppercase tracking-widest text-blue-700">Refund in progress</span>
                        )}
                        {refundable && (
                          <button onClick={() => setRefundTransaction(payment)} disabled={activeAction !== null} className="text-[10px] font-bold uppercase tracking-widest text-rose-600 hover:underline disabled:opacity-50 flex items-center gap-1">
                            {payment.status === "PARTIALLY_REFUNDED" ? "Refund rest" : payment.status === "REFUND_FAILED" ? "Retry" : "Refund"}
                          </button>
                        )}
                      </div>
                    );
                  })}
                  {(!paymentsData?.data || paymentsData?.data?.length === 0) && <p className="text-[11px] text-[#a1a1aa]">No payments logged.</p>}
                </div>
              )}
            </div>
          </div>
        )}
      </SidePanel>

      {isCreateModalOpen && (
        <CreateSubscriberModal onClose={() => setIsCreateModalOpen(false)} />
      )}

      {isPaymentModalOpen && (
        <div className="fixed inset-0 z-[60] flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/20 backdrop-blur-sm" onClick={() => !activeAction && setIsPaymentModalOpen(false)} />
          <form onSubmit={(e) => { e.preventDefault(); actionMutation.mutate({ action: "record-payment", payload: { amount: parseFloat(paymentAmount), payment_method: paymentMethod, reference_number: paymentRef }}); }} className="relative bg-white border border-[#e5e5e5] shadow-2xl w-full max-w-sm flex flex-col animate-in zoom-in-95 duration-200">
            <div className="p-4 border-b border-[#e5e5e5] bg-[#fafafa] flex items-center justify-between">
              <h3 className="text-[13px] font-bold uppercase tracking-widest text-[#09090b]">Log Offline Payment</h3>
              <button type="button" onClick={() => setIsPaymentModalOpen(false)} disabled={activeAction !== null} className="text-[#a1a1aa] hover:text-[#09090b] disabled:opacity-50 p-1"><X size={16} /></button>
            </div>
            <div className="p-5 space-y-4">
              <p className="text-[12px] text-[#52525b] leading-relaxed">This grants one period from today.</p>
              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Amount Paid (MYR) *</label>
                <input required type="number" step="0.01" value={paymentAmount} onChange={e => setPaymentAmount(e.target.value)} disabled={activeAction !== null} className="w-full h-9 border border-[#e5e5e5] px-3 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50" />
              </div>
              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Method *</label>
                <select value={paymentMethod} onChange={e => setPaymentMethod(e.target.value)} disabled={activeAction !== null} className="w-full h-9 border border-[#e5e5e5] px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50">
                  <option value="BANK_TRANSFER">Bank Transfer (Manual)</option>
                  <option value="CASH">Cash</option>
                  <option value="COMPED">Complimentary (RM 0)</option>
                </select>
              </div>
              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Reference ID (Optional)</label>
                <input value={paymentRef} onChange={e => setPaymentRef(e.target.value)} disabled={activeAction !== null} className="w-full h-9 border border-[#e5e5e5] px-3 font-mono text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
              </div>
            </div>
            <div className="p-4 border-t border-[#f4f4f5] bg-[#fafafa]/50 flex justify-end gap-2">
              <button type="button" onClick={() => setIsPaymentModalOpen(false)} disabled={activeAction !== null} className="px-4 h-8 text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:bg-[#e5e5e5] hover:text-[#09090b] border border-[#e5e5e5] bg-white transition-colors disabled:opacity-50 rounded-sm">Cancel</button>
              <button type="submit" disabled={activeAction !== null} className="px-5 h-8 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest hover:bg-[#27272a] disabled:opacity-50 flex items-center gap-1.5 rounded-sm">
                {activeAction === "record-payment" && <Loader2 size={13} className="animate-spin" />} Save Payment
              </button>
            </div>
          </form>
        </div>
      )}

      {pauseDunningModal.isOpen && (
        <div className="fixed inset-0 z-[60] flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/20 backdrop-blur-sm" onClick={() => !activeAction && setPauseDunningModal({ isOpen: false, date: "" })} />
          <form onSubmit={(e) => { e.preventDefault(); actionMutation.mutate({ action: "dunning/pause", payload: { pause_until: new Date(pauseDunningModal.date).toISOString() }}); }} className="relative bg-white border border-[#e5e5e5] shadow-2xl w-full max-w-sm flex flex-col animate-in zoom-in-95 duration-200">
            <div className="p-4 border-b border-[#e5e5e5] bg-[#fafafa] flex items-center justify-between">
              <h3 className="text-[13px] font-bold uppercase tracking-widest text-[#09090b]">Pause recovery</h3>
              <button type="button" onClick={() => setPauseDunningModal({ isOpen: false, date: "" })} disabled={activeAction !== null} className="text-[#a1a1aa] hover:text-[#09090b] disabled:opacity-50 p-1"><X size={16} /></button>
            </div>
            <div className="p-5 space-y-4">
              <p className="text-[12px] text-[#52525b] leading-relaxed">
                Temporarily pause all automated dunning emails and escalation actions (like suspension) for this customer until a specific date.
              </p>
              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Pause Until *</label>
                <input required type="datetime-local" value={pauseDunningModal.date} onChange={e => setPauseDunningModal({ ...pauseDunningModal, date: e.target.value })} disabled={activeAction !== null} className="w-full h-9 border border-[#e5e5e5] px-3 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50" />
              </div>
            </div>
            <div className="p-4 border-t border-[#f4f4f5] bg-[#fafafa]/50 flex justify-end gap-2">
              <button type="button" onClick={() => setPauseDunningModal({ isOpen: false, date: "" })} disabled={activeAction !== null} className="px-4 h-8 text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:bg-[#e5e5e5] hover:text-[#09090b] border border-[#e5e5e5] bg-white transition-colors disabled:opacity-50 rounded-sm">Cancel</button>
              <button type="submit" disabled={activeAction !== null} className="px-5 h-8 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest hover:bg-[#27272a] disabled:opacity-50 flex items-center gap-1.5 rounded-sm">
                {activeAction === "dunning/pause" && <Loader2 size={13} className="animate-spin" />} Pause Rule
              </button>
            </div>
          </form>
        </div>
      )}

      {pauseCollectionModal.isOpen && (
        <div className="fixed inset-0 z-[60] flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/20 backdrop-blur-sm" onClick={() => !activeAction && setPauseCollectionModal({ isOpen: false, date: "" })} />
          <form onSubmit={(e) => { e.preventDefault(); actionMutation.mutate({ action: "collection/pause", payload: { resume_on: new Date(pauseCollectionModal.date).toISOString() }}); }} className="relative bg-white border border-[#e5e5e5] shadow-2xl w-full max-w-sm flex flex-col animate-in zoom-in-95 duration-200">
            <div className="p-4 border-b border-[#e5e5e5] bg-[#fafafa] flex items-center justify-between">
              <h3 className="text-[13px] font-bold uppercase tracking-widest text-[#09090b]">Pause collection</h3>
              <button type="button" onClick={() => setPauseCollectionModal({ isOpen: false, date: "" })} disabled={activeAction !== null} className="text-[#a1a1aa] hover:text-[#09090b] disabled:opacity-50 p-1"><X size={16} /></button>
            </div>
            <div className="p-5 space-y-4">
              <p className="text-[12px] text-[#52525b] leading-relaxed">
                Stop billing until this date. Access stays ACTIVE. No charge and no dunning emails during the holiday.
              </p>
              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Resume on *</label>
                <input required type="datetime-local" value={pauseCollectionModal.date} onChange={e => setPauseCollectionModal({ ...pauseCollectionModal, date: e.target.value })} disabled={activeAction !== null} className="w-full h-9 border border-[#e5e5e5] px-3 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50" />
              </div>
            </div>
            <div className="p-4 border-t border-[#f4f4f5] bg-[#fafafa]/50 flex justify-end gap-2">
              <button type="button" onClick={() => setPauseCollectionModal({ isOpen: false, date: "" })} disabled={activeAction !== null} className="px-4 h-8 text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:bg-[#e5e5e5] border border-[#e5e5e5] bg-white">Cancel</button>
              <button type="submit" disabled={activeAction !== null} className="px-5 h-8 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest">Pause collection</button>
            </div>
          </form>
        </div>
      )}

      {refundTransaction && selectedSub && (
        <RefundModal
          transaction={refundTransaction}
          subscriptionId={selectedSub.id}
          onClose={() => setRefundTransaction(null)}
        />
      )}
    </PageLayout>
  );
}
