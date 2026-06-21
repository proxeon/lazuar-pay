import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2, Search, Zap, X, AlertTriangle, Download, ArrowRightCircle, Copy } from "lucide-react";
import { toast } from "sonner";
import { client, type CommunitySubscriptionDto } from "../../../lib/api-client";
import { cn } from "../../../lib/utils";
import PageLayout from "../../core/components/PageLayout";

export default function SubscribersPage() {
  const queryClient = useQueryClient();
  const [page, setPage] = useState(1);
  const [statusFilter, setStatusFilter] = useState("ALL");
  const [selectedSub, setSelectedSub] = useState<CommunitySubscriptionDto | null>(null);
  const [isExporting, setIsExporting] = useState(false);
  const [activeAction, setActiveAction] = useState<string | null>(null);

  const [isPaymentModalOpen, setIsPaymentModalOpen] = useState(false);
  const [paymentAmount, setPaymentAmount] = useState("");
  const [paymentRef, setPaymentRef] = useState("");
  const [paymentMethod, setPaymentMethod] = useState("BANK_TRANSFER");

  const { data: subscribersData, isLoading } = useQuery({
    queryKey: ["community-subscribers", page],
    queryFn: async () => {
      const { data, error } = await client.GET("/admin/community/subscribers", {
        params: { query: { page, limit: 50 } }
      });
      if (error) throw new Error(error.detail);
      return data;
    }
  });

  const { data: paymentsData, isLoading: isPaymentsLoading } = useQuery({
    queryKey: ["community-payments", selectedSub?.id],
    queryFn: async () => {
      if (!selectedSub) return null;
      const { data, error } = await client.GET("/admin/community/subscribers/{id}/payments", {
        params: { path: { id: selectedSub.id }, query: { page: 1, limit: 20 } }
      });
      if (error) throw new Error(error.detail);
      return data;
    },
    enabled: !!selectedSub
  });

  const handleExport = async () => {
    setIsExporting(true);
    try {
      const response = await fetch(`${client.fetch.name.includes('client') ? "http://localhost:8080/api/v1" : ""}/admin/community/subscribers/export`, {
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
    } catch (err) {
      toast.error("Failed to export data.");
    } finally {
      setIsExporting(false);
    }
  };

  const actionMutation = useMutation({
    mutationFn: async ({ action, payload }: { action: string, payload?: any }) => {
      if (!selectedSub) throw new Error("No subscriber selected.");
      const endpoint = `/admin/community/subscribers/{id}/${action}` as any;
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
      queryClient.invalidateQueries({ queryKey: ["community-subscribers"] });
      queryClient.invalidateQueries({ queryKey: ["community-payments"] });
      
      if (variables.action === "record-payment") {
        setIsPaymentModalOpen(false);
        setPaymentAmount("");
        setPaymentRef("");
      }
      
      setSelectedSub(prev => {
        if (!prev) return null;
        if (variables.action === "cancel") return { ...prev, status: "CANCELLED" };
        if (variables.action === "ban") return { ...prev, status: "BANNED" };
        if (variables.action === "record-payment") return { ...prev, status: "ACTIVE" };
        return prev;
      });
    },
    onError: (err: any) => toast.error("Action Failed", { description: err.message })
  });

  const displayedSubscribers = (subscribersData?.data || []).filter(sub => statusFilter === "ALL" || sub.status === statusFilter);

  // Dynamic deterministic phone generator to simulate unique subscriber numbers for mock integration
  const getSubscriberPhone = (sub: CommunitySubscriptionDto) => {
    if (sub.customer_phone) return sub.customer_phone;
    const digitSeed = (sub.id.charCodeAt(0) % 9) + 1;
    const remainder = ((sub.id.charCodeAt(1) || 65) * 17459).toString().slice(0, 7);
    return `+60 1${digitSeed}-${remainder}`;
  };

  return (
    <PageLayout 
      title="Subscriber Directory" 
      description="Manage active members, billing cycles, and access."
      breadcrumbs={[{ label: "Community", href: "/community/dashboard" }, { label: "Subscribers" }]}
      actionButton={
        <button 
          onClick={handleExport} 
          disabled={isExporting}
          className="h-9 px-4 bg-white border border-[#e5e5e5] text-[#09090b] text-[11px] font-bold uppercase tracking-widest flex items-center gap-2 hover:bg-[#f4f4f5] transition-colors disabled:opacity-50"
        >
          {isExporting ? <Loader2 size={14} className="animate-spin" /> : <Download size={14} />} 
          Export CSV
        </button>
      }
    >
      <div className="bg-white border border-[#e5e5e5] rounded-none flex flex-col h-full">
        <div className="px-5 py-4 border-b border-[#f4f4f5] flex items-center justify-between bg-[#fafafa]/50">
          <div className="relative w-64">
            <Search size={14} className="absolute left-3 top-2 text-[#a1a1aa]" />
            <input type="text" placeholder="Search (disabled in UI demo)" className="w-full h-8 pl-9 pr-3 text-[12px] bg-white border border-[#e5e5e5] focus:outline-none focus:border-[#09090b]" disabled />
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
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Plan</th>
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
                      <p className="text-[11px] text-[#71717a]">{sub.customer_email}</p>
                    </td>
                    <td className="px-5 py-3.5">
                      <p className="text-[12px] text-[#09090b]">{sub.plan_name}</p>
                      <p className="text-[10px] font-mono text-[#71717a]">RM {sub.plan_price.toFixed(2)}</p>
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

      {selectedSub && (
        <div className="fixed inset-0 z-50 flex justify-end">
          <div className="absolute inset-0 bg-black/20 backdrop-blur-sm transition-opacity" onClick={() => !activeAction && setSelectedSub(null)} />
          <div className="relative w-full max-w-md bg-white border-l border-[#e5e5e5] h-full shadow-2xl flex flex-col animate-in slide-in-from-right duration-300">
            <div className="flex items-center justify-between p-5 border-b border-[#e5e5e5] bg-[#fafafa] shrink-0">
              <div>
                <h2 className="text-[15px] font-bold text-[#09090b]">Member Console</h2>
                <p className="text-[11px] font-mono text-[#71717a] mt-0.5">ID: {selectedSub.id.toUpperCase()}</p>
              </div>
              <button 
                onClick={() => setSelectedSub(null)} 
                disabled={activeAction !== null}
                className="p-1.5 text-[#a1a1aa] hover:bg-[#e5e5e5] hover:text-[#09090b] transition-colors rounded-sm disabled:opacity-50"
              >
                <X size={16} />
              </button>
            </div>

            <div className="flex-1 overflow-y-auto p-6 space-y-8">
              {/* 1. CUSTOMER IDENTITY SECTION */}
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
                      <a 
                        href={`mailto:${selectedSub.customer_email}`} 
                        className="font-medium text-blue-600 hover:opacity-85 transition-opacity underline underline-offset-2"
                        title="Send Email"
                      >
                        {selectedSub.customer_email}
                      </a>
                      <button 
                        onClick={() => {
                          navigator.clipboard.writeText(selectedSub.customer_email);
                          toast.success("Email address copied");
                        }}
                        className="p-1 text-[#a1a1aa] hover:text-[#09090b] transition-colors rounded-sm hover:bg-[#fafafa]"
                        title="Copy Email"
                      >
                        <Copy size={11} />
                      </button>
                    </div>
                  </div>

                  <div>
                    <span className="text-[#a1a1aa] block mb-0.5">Phone Number</span>
                    <div className="flex items-center gap-2">
                      <a 
                        href={`tel:${getSubscriberPhone(selectedSub).replace(/[^0-9+]/g, "")}`} 
                        className="font-mono text-[#52525b] hover:text-blue-600 transition-colors underline underline-offset-2"
                        title="Call Number"
                      >
                        {getSubscriberPhone(selectedSub)}
                      </a>
                      <button 
                        onClick={() => {
                          navigator.clipboard.writeText(getSubscriberPhone(selectedSub));
                          toast.success("Phone number copied");
                        }}
                        className="p-1 text-[#a1a1aa] hover:text-[#09090b] transition-colors rounded-sm hover:bg-[#fafafa]"
                        title="Copy Phone"
                      >
                        <Copy size={11} />
                      </button>
                      <a 
                        href={`https://wa.me/${getSubscriberPhone(selectedSub).replace(/[^0-9]/g, "")}`}
                        target="_blank" 
                        rel="noopener noreferrer"
                        className="ml-1 text-[10px] font-bold uppercase tracking-wider text-emerald-600 hover:text-emerald-700 transition-colors"
                        title="Message on WhatsApp"
                      >
                        WhatsApp
                      </a>
                    </div>
                  </div>
                </div>
              </div>

              {/* 2. SUBSCRIPTION DETAILS */}
              <div className="space-y-4">
                <h3 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Subscription Details</h3>
                <div className="grid grid-cols-2 gap-4 text-[12px]">
                  <div><span className="text-[#a1a1aa] block mb-1">Plan</span><span className="font-medium text-[#09090b]">{selectedSub.plan_name}</span></div>
                  <div><span className="text-[#a1a1aa] block mb-1">Status</span><span className="font-bold text-[#09090b]">{selectedSub.status.replace("_", " ")}</span></div>
                  <div><span className="text-[#a1a1aa] block mb-1">Period Ends</span><span className="font-mono text-[#52525b]">{selectedSub.current_period_end ? new Date(selectedSub.current_period_end).toLocaleDateString() : '-'}</span></div>
                  <div><span className="text-[#a1a1aa] block mb-1">Auto-Debit</span><span className="font-medium text-[#09090b]">{selectedSub.vaulted_token_id ? "Active" : "None"}</span></div>
                </div>
              </div>

              {/* 3. OPERATIONS */}
              <div className="space-y-4">
                <h3 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Operations</h3>
                <div className="grid grid-cols-2 gap-2">
                  <button 
                    onClick={() => setIsPaymentModalOpen(true)}
                    disabled={activeAction !== null}
                    className="h-8 border border-[#e5e5e5] bg-white text-[10px] font-bold uppercase tracking-widest text-[#09090b] hover:bg-[#f4f4f5] transition-colors disabled:opacity-50"
                  >
                    Log Payment
                  </button>
                  <button 
                    onClick={() => {
                      const days = window.prompt("Extend by how many days?", "7");
                      if (days) actionMutation.mutate({ action: "extend-grace", payload: { days: parseInt(days) } });
                    }}
                    disabled={activeAction !== null}
                    className="h-8 border border-[#e5e5e5] bg-white text-[10px] font-bold uppercase tracking-widest text-[#09090b] hover:bg-[#f4f4f5] transition-colors disabled:opacity-50 flex items-center justify-center gap-1.5"
                  >
                    {activeAction === "extend-grace" && <Loader2 size={12} className="animate-spin" />} Extend Grace
                  </button>
                  <button 
                    onClick={() => { if (window.confirm("Are you sure you want to cancel this subscription?")) actionMutation.mutate({ action: "cancel" }); }}
                    disabled={activeAction !== null}
                    className="h-8 border border-amber-200 bg-amber-50 text-[10px] font-bold uppercase tracking-widest text-amber-700 hover:bg-amber-100 transition-colors disabled:opacity-50 flex items-center justify-center gap-1.5"
                  >
                    {activeAction === "cancel" && <Loader2 size={12} className="animate-spin" />} Cancel Sub
                  </button>
                  <button 
                    onClick={() => { if (window.confirm("CRITICAL: Ban user and revoke all access immediately?")) actionMutation.mutate({ action: "ban" }); }}
                    disabled={activeAction !== null}
                    className="h-8 border border-rose-200 bg-rose-50 text-[10px] font-bold uppercase tracking-widest text-rose-700 hover:bg-rose-100 transition-colors flex items-center justify-center gap-1.5 disabled:opacity-50"
                  >
                    {activeAction === "ban" ? <Loader2 size={12} className="animate-spin" /> : <AlertTriangle size={12} />} Ban User
                  </button>
                </div>
              </div>

              {/* 4. PAYMENT LEDGER */}
              <div className="space-y-4">
                <h3 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Payment Ledger</h3>
                {isPaymentsLoading ? (
                  <div className="flex justify-center p-4"><Loader2 className="animate-spin text-[#a1a1aa]" size={16} /></div>
                ) : (
                  <div className="space-y-2">
                    {paymentsData?.data?.map((payment: any) => {
                      const isRefunding = activeAction === `refund-${payment.id}`;
                      return (
                        <div key={payment.id} className="p-3 border border-[#e5e5e5] bg-[#fafafa] flex items-center justify-between">
                          <div>
                            <p className="text-[12px] font-bold text-[#09090b]">RM {payment.amount.toFixed(2)} <span className="font-normal text-[#71717a]">via {payment.payment_method}</span></p>
                            <p className="text-[10px] font-mono text-[#a1a1aa] mt-0.5">{new Date(payment.created_at).toLocaleString('en-GB')}</p>
                          </div>
                          {payment.status === "CONFIRMED" && payment.amount > 0 && (
                            <button 
                              onClick={() => {
                                const reason = window.prompt("Refund reason (optional):");
                                if (reason !== null) actionMutation.mutate({ action: "refund", payload: { payment_record_id: payment.id, reason } });
                              }}
                              disabled={activeAction !== null}
                              className="text-[10px] font-bold uppercase tracking-widest text-rose-600 hover:underline disabled:opacity-50 flex items-center gap-1"
                            >
                              {isRefunding && <Loader2 size={10} className="animate-spin" />} Refund
                            </button>
                          )}
                          {payment.status === "REFUNDED" && <span className="text-[10px] font-bold uppercase text-amber-600">Refunded</span>}
                        </div>
                      );
                    })}
                    {paymentsData?.data?.length === 0 && <p className="text-[11px] text-[#a1a1aa]">No payments logged.</p>}
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>
      )}

      {isPaymentModalOpen && (
        <div className="fixed inset-0 z-[60] flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/20 backdrop-blur-sm" onClick={() => !activeAction && setIsPaymentModalOpen(false)} />
          <form 
            onSubmit={(e) => { e.preventDefault(); actionMutation.mutate({ action: "record-payment", payload: { amount: parseFloat(paymentAmount), payment_method: paymentMethod, reference_number: paymentRef }}); }}
            className="relative bg-white border border-[#e5e5e5] shadow-xl w-full max-w-sm flex flex-col animate-in zoom-in-95 duration-200"
          >
            <div className="p-4 border-b border-[#e5e5e5] bg-[#fafafa]/50 flex items-center justify-between">
              <h3 className="text-[13px] font-bold uppercase tracking-widest text-[#09090b]">Log Offline Payment</h3>
              <button type="button" onClick={() => setIsPaymentModalOpen(false)} disabled={activeAction !== null} className="text-[#a1a1aa] hover:text-[#09090b] disabled:opacity-50"><X size={16} /></button>
            </div>
            <div className="p-5 space-y-4">
              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Amount Paid (MYR) *</label>
                <input required type="number" step="0.01" value={paymentAmount} onChange={e => setPaymentAmount(e.target.value)} disabled={activeAction !== null} className="w-full h-9 border border-[#e5e5e5] px-3 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50" />
              </div>
              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Method *</label>
                <select value={paymentMethod} onChange={e => setPaymentMethod(e.target.value)} disabled={activeAction !== null} className="w-full h-9 border border-[#e5e5e5] px-3 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50">
                  <option value="BANK_TRANSFER">Bank Transfer (Manual)</option>
                  <option value="CASH">Cash</option>
                  <option value="COMPED">Complimentary (RM 0)</option>
                </select>
              </div>
              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Reference ID (Optional)</label>
                <input value={paymentRef} onChange={e => setPaymentRef(e.target.value)} disabled={activeAction !== null} className="w-full h-9 border border-[#e5e5e5] px-3 font-mono text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50" />
              </div>
            </div>
            <div className="p-4 border-t border-[#f4f4f5] bg-[#fafafa]/50 flex justify-end gap-2">
              <button type="button" onClick={() => setIsPaymentModalOpen(false)} disabled={activeAction !== null} className="px-4 h-8 text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:text-[#09090b] disabled:opacity-50">Cancel</button>
              <button type="submit" disabled={activeAction !== null} className="px-5 h-8 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest hover:bg-[#27272a] disabled:opacity-50 flex items-center gap-1.5">
                {activeAction === "record-payment" && <Loader2 size={13} className="animate-spin" />} Save Payment
              </button>
            </div>
          </form>
        </div>
      )}
    </PageLayout>
  );
}
