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

export default function SubscribersPage() {
  const { activeWorkspaceId } = useOutletContext<{ activeWorkspaceId: string | null }>();
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

  const [refundModal, setRefundModal] = useState({ isOpen: false, paymentId: "", reason: "" });
  const [pauseDunningModal, setPauseDunningModal] = useState({ isOpen: false, date: "" });

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
        params: { query: { page: 1, limit: 20, search: selectedSub.customer_email } }
      });
      if (error) throw new Error(error.detail);
      return data;
    },
    enabled: !!selectedSub && !!activeWorkspaceId
  });

  const handleExport = async () => {
    setIsExporting(true);
    try {
      const response = await fetch(`${API_URL}/admin/commerce/subscribers/export`, {
        headers: { "X-Tenant-Id": localStorage.getItem("ops_active_workspace_id") || "" },
      });
      const blob = await response.blob();
      const url = window.URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `Subscribers_Export_${new Date().toISOString().slice(0,10)}.csv`;
      document.body.appendChild(a);
      a.click();
      a.remove();
    } catch {
      toast.error("Failed to export data.");
    } finally {
      setIsExporting(false);
    }
  };

  const actionMutation = useMutation({
    mutationFn: async ({ action, payload }: { action: string, payload?: any }) => {
      if (!selectedSub) throw new Error("No subscriber selected.");
      const endpoint = `/admin/commerce/subscribers/{id}/${action}` as any; 
      const { error } = await client.POST(endpoint, {
        params: { path: { id: selectedSub.id } },
        body: payload
      });
      if (error) throw new Error(error.detail);
    },
    onMutate: (variables) => {
      const actionKey = variables.action === "refund" ? `refund-${variables.payload.payment_record_id}` : variables.action;
      setActiveAction(actionKey);
    },
    onSettled: () => {
      setActiveAction(null);
    },
    onSuccess: (_, variables) => {
      toast.success(`Action successfully executed.`);
      queryClient.invalidateQueries({ queryKey: ["commerce-subscribers"] });
      queryClient.invalidateQueries({ queryKey: ["commerce-payments"] });
      
      if (variables.action === "record-payment") {
        setIsPaymentModalOpen(false);
        setPaymentAmount("");
        setPaymentRef("");
        setSelectedSub(prev => prev ? { ...prev, status: "ACTIVE" } : null);
      } else if (variables.action === "refund") {
        setRefundModal({ isOpen: false, paymentId: "", reason: "" });
      } else if (variables.action === "dunning/pause") {
        setPauseDunningModal({ isOpen: false, date: "" });
        setSelectedSub(prev => prev ? { ...prev, dunning_paused_until: new Date(variables.payload.pause_until).toISOString() } : null);
      } else if (variables.action === "dunning/resume") {
        setSelectedSub(prev => prev ? { ...prev, dunning_paused_until: undefined } : null);
      } else if (variables.action === "cancel") {
        setSelectedSub(prev => prev ? { ...prev, status: "CANCELLED" } : null);
      } else if (variables.action === "ban") {
        setSelectedSub(prev => prev ? { ...prev, status: "BANNED" } : null);
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
          <button 
            onClick={handleExport} 
            disabled={isExporting}
            className="h-9 px-4 bg-white border border-[#e5e5e5] text-[#09090b] text-[11px] font-bold uppercase tracking-widest flex items-center gap-2 hover:bg-[#f4f4f5] transition-colors disabled:opacity-50"
          >
            {isExporting ? <Loader2 size={14} className="animate-spin" /> : <Download size={14} />} 
            Export CSV
          </button>
          <button 
            onClick={() => setIsCreateModalOpen(true)}
            className="h-9 px-4 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest flex items-center gap-2 hover:bg-[#27272a] transition-colors"
          >
            <Plus size={14} /> Add Member
          </button>
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
            <option value="PAST_DUE">PAST DUE</option>
            <option value="CANCELLED">CANCELLED</option>
          </select>
        </div>

        <div className="w-full overflow-x-auto min-h-[500px]">
          <table className="w-full text-left text-[13px] min-w-[800px]">
            <thead className="bg-white border-b border-[#f4f4f5]">
              <tr>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Customer</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Product</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Status</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] text-right">Period End</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#f4f4f5]">
              {isLoading ? (
                <tr><td colSpan={4} className="py-12 text-center text-[#a1a1aa]"><Loader2 size={20} className="animate-spin mx-auto" /></td></tr>
              ) : displayedSubscribers.length === 0 ? (
                <tr><td colSpan={4} className="py-12 text-center text-[12px] text-[#71717a]">No subscribers found.</td></tr>
              ) : (
                displayedSubscribers.map((sub) => (
                  <tr key={sub.id} onClick={() => setSelectedSub(sub)} className="hover:bg-[#fafafa] transition-colors cursor-pointer group">
                    <td className="px-5 py-3.5 min-w-[200px]">
                      <div className="flex items-center gap-2">
                        <p className="font-medium text-[#09090b] text-[13px] group-hover:text-blue-600 transition-colors">{sub.customer_name}</p>
                        {sub.vaulted_token_id && <Zap size={12} className="text-blue-500" title="Auto-Debit Enabled" />}
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
                      <span className={cn(
                        "text-[9px] px-1.5 py-0.5 border font-bold uppercase tracking-widest whitespace-nowrap",
                        sub.status === "ACTIVE" ? "bg-emerald-50 text-emerald-700 border-emerald-200" :
                        sub.status === "PAST_DUE" ? "bg-rose-50 text-rose-700 border-rose-200" :
                        "bg-zinc-100 text-zinc-600 border-zinc-200"
                      )}>
                        {sub.status.replace("_", " ")}
                      </span>
                    </td>
                    <td className="px-5 py-3.5 text-right font-mono text-[#52525b] text-[11px] whitespace-nowrap">
                      {sub.current_period_end ? new Date(sub.current_period_end).toLocaleDateString('en-GB') : '-'}
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
                <div><span className="text-[#a1a1aa] block mb-1">Product</span><span className="font-medium text-[#09090b]">{selectedSub.product_name}</span></div>
                <div><span className="text-[#a1a1aa] block mb-1">Status</span><span className="font-bold text-[#09090b]">{selectedSub.status.replace("_", " ")}</span></div>
                <div><span className="text-[#a1a1aa] block mb-1">Period Ends</span><span className="font-mono text-[#52525b]">{selectedSub.current_period_end ? new Date(selectedSub.current_period_end).toLocaleDateString() : '-'}</span></div>
                <div><span className="text-[#a1a1aa] block mb-1">Auto-Debit</span><span className="font-medium text-[#09090b]">{selectedSub.vaulted_token_id ? "Active" : "None"}</span></div>
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
                        <CalendarClock size={12} /> Pause Automations
                      </button>
                    </div>
                  )}
                </div>
              </div>
            )}

            <div className="space-y-4">
              <h3 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Operations</h3>
              <div className="grid grid-cols-2 gap-2">
                <button onClick={() => setIsPaymentModalOpen(true)} disabled={activeAction !== null} className="h-8 border border-[#e5e5e5] bg-white text-[10px] font-bold uppercase tracking-widest text-[#09090b] hover:bg-[#f4f4f5] transition-colors disabled:opacity-50">Log Payment</button>
                <button onClick={() => { if (window.confirm("Are you sure you want to cancel this subscription?")) actionMutation.mutate({ action: "cancel" }); }} disabled={activeAction !== null} className="h-8 border border-amber-200 bg-amber-50 text-[10px] font-bold uppercase tracking-widest text-amber-700 hover:bg-amber-100 transition-colors disabled:opacity-50 flex items-center justify-center gap-1.5">
                  {activeAction === "cancel" && <Loader2 size={12} className="animate-spin" />} Cancel Sub
                </button>
                <button onClick={() => { if (window.confirm("CRITICAL: Ban user and revoke all access immediately?")) actionMutation.mutate({ action: "ban" }); }} disabled={activeAction !== null} className="h-8 col-span-2 border border-rose-200 bg-rose-50 text-[10px] font-bold uppercase tracking-widest text-rose-700 hover:bg-rose-100 transition-colors flex items-center justify-center gap-1.5 disabled:opacity-50">
                  {activeAction === "ban" ? <Loader2 size={12} className="animate-spin" /> : <AlertTriangle size={12} />} Ban User
                </button>
              </div>
            </div>

            <div className="space-y-4">
              <h3 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Payment Ledger</h3>
              {isPaymentsLoading ? (
                <div className="flex justify-center p-4"><Loader2 className="animate-spin text-[#a1a1aa]" size={16} /></div>
              ) : (
                <div className="space-y-2">
                  {paymentsData?.data?.map((payment: any) => {
                    const isRefunding = activeAction === `refund-${payment.id}`;
                    return (
                      <div key={payment.id} className="p-3 border border-[#e5e5e5] bg-[#fafafa] flex items-center justify-between rounded-sm">
                        <div className="flex flex-col gap-1">
                          <p className="text-[12px] font-bold text-[#09090b]">RM {payment.amount.toFixed(2)} <span className="font-normal text-[#71717a]">via {payment.payment_method}</span></p>
                          <div className="flex items-center gap-2">
                            <span className="text-[10px] font-mono font-bold text-[#09090b]">{payment.id.substring(0,8)}</span>
                          </div>
                          <p className="text-[10px] font-mono text-[#a1a1aa] mt-0.5">{new Date(payment.created_at).toLocaleString('en-GB')}</p>
                        </div>
                        {payment.status === "CONFIRMED" && payment.amount > 0 && (
                          <button onClick={() => setRefundModal({ isOpen: true, paymentId: payment.id, reason: "" })} disabled={activeAction !== null} className="text-[10px] font-bold uppercase tracking-widest text-rose-600 hover:underline disabled:opacity-50 flex items-center gap-1">
                            {isRefunding && <Loader2 size={10} className="animate-spin" />} Refund
                          </button>
                        )}
                        {payment.status === "REFUNDED" && <span className="text-[10px] font-bold uppercase tracking-widest text-amber-600 border border-amber-200 bg-amber-50 px-1.5 py-0.5">Refunded</span>}
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
              <h3 className="text-[13px] font-bold uppercase tracking-widest text-[#09090b]">Pause Automations</h3>
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

      {refundModal.isOpen && (
        <div className="fixed inset-0 z-[60] flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/20 backdrop-blur-sm" onClick={() => !activeAction && setRefundModal({ isOpen: false, paymentId: "", reason: "" })} />
          <form onSubmit={(e) => { e.preventDefault(); actionMutation.mutate({ action: "refund", payload: { payment_record_id: refundModal.paymentId, reason: refundModal.reason }}); }} className="relative bg-white border border-rose-200 shadow-2xl w-full max-w-sm flex flex-col animate-in zoom-in-95 duration-200">
            <div className="p-4 border-b border-rose-200 bg-rose-50 flex items-center justify-between">
              <h3 className="text-[13px] font-bold uppercase tracking-widest text-rose-700">Issue Refund</h3>
              <button type="button" onClick={() => setRefundModal({ isOpen: false, paymentId: "", reason: "" })} disabled={activeAction !== null} className="text-rose-400 hover:text-rose-700 disabled:opacity-50 p-1"><X size={16} /></button>
            </div>
            <div className="p-5 space-y-4">
              <p className="text-[12px] text-[#52525b] leading-relaxed">You are about to issue a full refund for this transaction. This action cannot be undone.</p>
              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Reason (Optional)</label>
                <input type="text" value={refundModal.reason} onChange={e => setRefundModal({ ...refundModal, reason: e.target.value })} disabled={activeAction !== null} placeholder="e.g. Customer requested cancellation" className="w-full h-9 border border-[#e5e5e5] px-3 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50" />
              </div>
            </div>
            <div className="p-4 border-t border-rose-100 bg-rose-50/50 flex justify-end gap-2">
              <button type="button" onClick={() => setRefundModal({ isOpen: false, paymentId: "", reason: "" })} disabled={activeAction !== null} className="px-4 h-8 text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:bg-[#e5e5e5] hover:text-[#09090b] border border-[#e5e5e5] bg-white transition-colors disabled:opacity-50 rounded-sm">Cancel</button>
              <button type="submit" disabled={activeAction !== null} className="px-5 h-8 bg-rose-600 text-white text-[11px] font-bold uppercase tracking-widest hover:bg-rose-700 disabled:opacity-50 flex items-center gap-1.5 rounded-sm">
                {activeAction === "refund" && <Loader2 size={13} className="animate-spin" />} Process Refund
              </button>
            </div>
          </form>
        </div>
      )}
    </PageLayout>
  );
}
