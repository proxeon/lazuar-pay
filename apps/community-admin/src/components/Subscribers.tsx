import { useState } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import { client } from "../lib/api-client";
import type { Subscriber } from "../lib/api-client";
import { Menu, MoreHorizontal, Check, Calendar, XCircle, RefreshCw, Plus, History, Globe, UserCog, DownloadCloud, Send, Edit, CalendarClock, MessageSquare, BellOff } from "lucide-react";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";
import { toast } from "sonner";
import AddSubscriberModal from "./AddSubscriberModal";
import RecordPaymentModal from "./RecordPaymentModal";
import PaymentHistoryModal from "./PaymentHistoryModal";
import SendReminderModal from "./SendReminderModal";
import EditSubscriberModal from "./EditSubscriberModal";
import ScheduleReminderModal from "./ScheduleReminderModal";
import ReminderHistoryModal from "./ReminderHistoryModal";
import PauseRemindersModal from "./PauseRemindersModal";

export default function Subscribers({ isMobile, toggleSidebar }: any) {
  const queryClient = useQueryClient();
  const [selectedTab, setSelectedTab] = useState<'ALL' | 'ACTIVE' | 'OVERDUE' | 'CANCELED'>('ALL');
  const [openMenuId, setOpenMenuId] = useState<string | null>(null);
  const [menuPos, setMenuPos] = useState({ top: 0, right: 0 });
  const [isExporting, setIsExporting] = useState(false);
  
  const [showAddModal, setShowAddModal] = useState(false);
  const [recordPaymentSub, setRecordPaymentSub] = useState<Subscriber | null>(null);
  const [historySub, setHistorySub] = useState<Subscriber | null>(null);
  const [reminderSub, setReminderSub] = useState<Subscriber | null>(null);
  const [scheduleSub, setScheduleSub] = useState<Subscriber | null>(null);
  const [editSub, setEditSub] = useState<Subscriber | null>(null);
  const [reminderHistorySub, setReminderHistorySub] = useState<Subscriber | null>(null);
  const [pauseReminderSub, setPauseReminderSub] = useState<Subscriber | null>(null);

  const handleMenuClick = (e: React.MouseEvent, id: string) => {
    e.stopPropagation();
    if (openMenuId === id) {
      setOpenMenuId(null);
    } else {
      const rect = e.currentTarget.getBoundingClientRect();
      setMenuPos({ top: rect.bottom, right: window.innerWidth - rect.right });
      setOpenMenuId(id);
    }
  };

  const { data: subscribers = [], isLoading, isFetching } = useQuery<Subscriber[]>({
    queryKey: ["community-subs"],
    queryFn: async () => {
      const { data, error } = await client.GET("/admin/community/subscribers");
      if (error) throw new Error(error.detail || "Failed to fetch subscribers");
      return data ?? [];
    },
  });

  const handleExportCsv = async () => {
    setIsExporting(true);
    try {
      const { data, error } = await client.GET("/admin/community/subscribers/export", {
        parseAs: "blob"
      });
      if (error) throw new Error(error.detail || "Export failed");

      const url = window.URL.createObjectURL(data as Blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = `Subscribers_Export.csv`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);
      window.URL.revokeObjectURL(url);
      toast.success("CSV Export downloaded successfully.");
    } catch (err: any) {
      toast.error("Export failed", { description: err.message });
    } finally {
      setIsExporting(false);
    }
  };

  const handleExtendGrace = (id: string, name: string) => {
    setOpenMenuId(null);
    const input = window.prompt(`How many days do you want to extend the grace period for ${name}?`, "5");
    if (!input) return;
    const days = parseInt(input, 10);
    if (isNaN(days) || days <= 0) { toast.error("Invalid number of days."); return; }

    toast.promise(
      client.POST("/admin/community/subscribers/{id}/extend-grace", {
        params: { path: { id } },
        body: { days }
      }).then(({ error }) => {
        if (error) throw new Error(error.detail || "Failed to extend grace limit.");
        queryClient.invalidateQueries({ queryKey: ["community-subs"] });
      }),
      { loading: `Extending grace period by ${days} days...`, success: `Deferred billing schedule for ${name} by ${days} days.`, error: (err) => err.message }
    );
  };

  const handleCancelSubscription = (id: string, name: string) => {
    setOpenMenuId(null);
    if (!window.confirm(`Are you sure you want to cancel the subscription for ${name}? They will lose access.`)) return;

    toast.promise(
      client.POST("/admin/community/subscribers/{id}/cancel", {
        params: { path: { id } }
      }).then(({ error }) => {
        if (error) throw new Error(error.detail || "Failed to cancel subscription.");
        queryClient.invalidateQueries({ queryKey: ["community-subs"] });
      }),
      { loading: "Cancelling subscription...", success: `Suspended billing parameters for ${name}.`, error: (err) => err.message }
    );
  };

  const filteredSubscribers = subscribers.filter((sub) => {
    if (selectedTab === "ACTIVE") return sub.status === "ACTIVE";
    if (selectedTab === "OVERDUE") return sub.status === "GRACE_PERIOD" || sub.status === "SUSPENDED" || sub.status === "PAST_DUE";
    if (selectedTab === "CANCELED") return sub.status === "CANCELED" || sub.status === "CANCELLED";
    return true;
  });

  const countAll = subscribers.length;
  const countActive = subscribers.filter(s => s.status === 'ACTIVE').length;
  const countOverdue = subscribers.filter(s => s.status === 'GRACE_PERIOD' || s.status === 'SUSPENDED' || s.status === 'PAST_DUE').length;
  const countCanceled = subscribers.filter(s => s.status === 'CANCELED' || s.status === 'CANCELLED').length;

  const tabsConfig = [
    { id: "ALL" as const, label: "All Subscribers", count: countAll },
    { id: "ACTIVE" as const, label: "Active", count: countActive },
    { id: "OVERDUE" as const, label: "Overdue", count: countOverdue, alert: countOverdue > 0 },
    { id: "CANCELED" as const, label: "Canceled", count: countCanceled }
  ];

  const renderSourceBadge = (source?: string) => {
    if (!source) return null;
    if (source === "ONLINE_CHECKOUT") {
        return <span className="text-[9px] uppercase tracking-widest bg-blue-50 text-blue-600 border border-blue-200 px-1.5 py-0.5 mt-1 inline-flex items-center gap-1 rounded-none"><Globe size={10} /> Online</span>;
    }
    if (source === "MANUAL_ENTRY") {
        return <span className="text-[9px] uppercase tracking-widest bg-zinc-100 text-zinc-600 dark:bg-zinc-900 dark:text-zinc-400 border border-zinc-200 dark:border-zinc-800 px-1.5 py-0.5 mt-1 inline-flex items-center gap-1 rounded-none"><UserCog size={10} /> Manual</span>;
    }
    if (source === "IMPORTED") {
        return <span className="text-[9px] uppercase tracking-widest bg-purple-50 text-purple-600 border border-purple-200 px-1.5 py-0.5 mt-1 inline-flex items-center gap-1 rounded-none"><DownloadCloud size={10} /> Imported</span>;
    }
    return <span className="text-[9px] uppercase tracking-widest bg-secondary/60 text-muted-foreground px-1.5 py-0.5 mt-1 inline-block border border-border/40 rounded-none">{source.replace(/_/g, " ")}</span>;
  };

  return (
    <div className="flex-1 w-full p-4 md:p-8 mx-auto max-w-[1240px] flex flex-col gap-6">
      <header className="flex items-center justify-between pb-2 flex-wrap gap-4">
        <div className="flex items-center gap-3">
          {isMobile && <button onClick={toggleSidebar} className="p-1.5 hover:bg-secondary rounded-none transition-colors"><Menu size={20} /></button>}
          <div>
            <h1 className="text-[20px] font-semibold tracking-tight text-foreground">Subscribers</h1>
            <p className="text-[11px] font-bold uppercase tracking-[0.2em] text-muted-foreground mt-1">Manage active profiles and manually intercept recurring states.</p>
          </div>
        </div>

        <div className="flex items-center gap-2">
          <button onClick={() => queryClient.invalidateQueries({ queryKey: ["community-subs"] })} disabled={isFetching} className="p-2 border border-border/60 bg-card hover:bg-secondary rounded-none transition-colors text-foreground flex items-center justify-center disabled:opacity-50" title="Refresh Subscribers">
            <RefreshCw size={16} className={isFetching ? "animate-spin text-muted-foreground" : ""} />
          </button>
          <button onClick={handleExportCsv} disabled={isExporting} className="inline-flex items-center h-10 px-4 bg-background border border-border/60 text-foreground text-sm font-bold tracking-wide uppercase rounded-none hover:bg-secondary transition-colors disabled:opacity-50">
            <DownloadCloud className={`w-4 h-4 mr-2 ${isExporting ? 'animate-pulse' : ''}`} /> Export CSV
          </button>
          <button onClick={() => setShowAddModal(true)} className="inline-flex items-center h-10 px-4 bg-foreground text-background text-sm font-bold tracking-wide uppercase rounded-none hover:bg-foreground/90 transition-colors">
            <Plus className="w-4 h-4 mr-2" /> Add Subscriber
          </button>
        </div>
      </header>

      {/* State Filter Tabs */}
      <div className="flex border-b border-border/60 gap-2 overflow-x-auto scrollbar-none">
        {tabsConfig.map((tab) => (
          <button key={tab.id} onClick={() => setSelectedTab(tab.id)} className={`pb-2.5 px-3 text-xs font-bold uppercase tracking-widest border-b-2 transition-all duration-150 whitespace-nowrap -mb-[2px] flex items-center gap-2 focus:outline-none ${selectedTab === tab.id ? "border-foreground text-foreground" : "border-transparent text-muted-foreground hover:text-foreground"}`}>
            <span>{tab.label}</span>
            <span className={`px-1.5 py-0.5 rounded-none text-[10px] leading-none ${tab.id === selectedTab ? "bg-foreground text-background" : tab.alert ? "bg-amber-100 text-amber-800 border border-amber-200" : "bg-secondary text-muted-foreground border border-border/60"}`}>{tab.count}</span>
          </button>
        ))}
      </div>

      <div className="bg-card border border-border/60 rounded-none shadow-sm overflow-hidden">
        <Table>
          <TableHeader>
            <TableRow className="bg-secondary/50 hover:bg-secondary/50">
              <TableHead className="w-[280px] font-bold text-xs uppercase tracking-widest text-muted-foreground">Customer</TableHead>
              <TableHead className="font-bold text-xs uppercase tracking-widest text-muted-foreground">Plan</TableHead>
              <TableHead className="font-bold text-xs uppercase tracking-widest text-muted-foreground">Billing Cycle</TableHead>
              <TableHead className="font-bold text-xs uppercase tracking-widest text-muted-foreground">Access Status</TableHead>
              <TableHead className="w-[80px] text-right font-bold text-xs uppercase tracking-widest text-muted-foreground">Actions</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoading ? (
              <TableRow><TableCell colSpan={5} className="text-center py-8 text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Loading subscribers index...</TableCell></TableRow>
            ) : filteredSubscribers.length === 0 ? (
              <TableRow><TableCell colSpan={5} className="text-center py-12 text-[11px] font-bold uppercase tracking-widest text-muted-foreground">No subscriber entries matched this filter.</TableCell></TableRow>
            ) : (
              filteredSubscribers.map((sub) => {
                const isOverdue = sub.status === "GRACE_PERIOD" || sub.status === "SUSPENDED" || sub.status === "PAST_DUE";
                const isRemindersPaused = sub.reminders_paused_until && new Date(sub.reminders_paused_until).getTime() > Date.now();
                const isUnpaidInvoice = sub.is_reminder_only && isOverdue;

                return (
                  <TableRow key={sub.id} className={`group hover:bg-secondary/40 border-b-border/60 ${isUnpaidInvoice ? "bg-amber-50/30 dark:bg-amber-950/10" : ""}`}>
                    <TableCell className="align-middle">
                      <div className="font-semibold text-sm text-foreground">{sub.customer_name}</div>
                      <div className="text-[11px] text-muted-foreground font-mono mt-1 tracking-tight">{sub.customer_phone || sub.customer_email}</div>
                      <div className="flex items-center flex-wrap gap-1.5 mt-1">
                        {renderSourceBadge(sub.source)}
                        {sub.is_reminder_only && <span className="text-[9px] uppercase tracking-widest bg-blue-50 text-blue-700 border border-blue-200 dark:bg-blue-950/50 dark:text-blue-400 dark:border-blue-900 px-1.5 py-0.5 inline-block rounded-none">Invoice Tracking</span>}
                        {isRemindersPaused && <span className="text-[9px] uppercase tracking-widest bg-amber-50 text-amber-700 dark:bg-amber-950/50 dark:text-amber-400 border border-amber-200 dark:border-amber-900 px-1.5 py-0.5 inline-flex items-center gap-1 rounded-none"><BellOff size={10} /> Paused</span>}
                      </div>
                    </TableCell>
                    <TableCell className="align-middle">
                      <div className="text-sm font-medium text-foreground">{sub.plan_name}</div>
                      <div className="text-xs text-muted-foreground mt-1 font-mono">RM {sub.plan_price.toFixed(2)}</div>
                    </TableCell>
                    <TableCell className="align-middle">
                      <div className="text-xs text-foreground flex items-center gap-1.5">
                        <span className="text-muted-foreground font-medium uppercase tracking-widest text-[10px]">Joined:</span>
                        <span className="font-mono">{new Date(sub.created_at).toLocaleDateString("en-MY", { year: 'numeric', month: 'short', day: 'numeric' })}</span>
                      </div>
                      <div className={`text-xs mt-1.5 flex items-center gap-1.5 ${isOverdue ? "text-amber-600 dark:text-amber-500 font-semibold" : "text-foreground"}`}>
                        <span className="text-muted-foreground font-medium uppercase tracking-widest text-[10px]">Next Due:</span>
                        <span className={isOverdue ? "font-bold" : "font-mono"}>{sub.next_billing_date ? new Date(sub.next_billing_date).toLocaleDateString("en-MY", { year: 'numeric', month: 'short', day: 'numeric' }) : "N/A"}</span>
                        {sub.days_overdue && <span className="ml-1 text-[10px] uppercase tracking-wider bg-amber-50 dark:bg-amber-950 text-amber-700 dark:text-amber-400 border border-amber-200 dark:border-amber-900 px-1.5 py-0.5 rounded-none font-bold">{sub.days_overdue}d overdue</span>}
                      </div>
                    </TableCell>
                    <TableCell className="align-middle">
                      {isUnpaidInvoice ? (
                        <Badge className="text-[10px] font-bold border py-0.5 px-2 tracking-widest uppercase rounded-none bg-amber-100 text-amber-800 border-amber-300 dark:bg-amber-900/50 dark:text-amber-400 dark:border-amber-700 hover:bg-amber-100">UNPAID INVOICE</Badge>
                      ) : (
                        <Badge className={`text-[10px] font-bold border py-0.5 px-2 tracking-widest uppercase rounded-none ${sub.status === "ACTIVE" ? "bg-emerald-50 text-emerald-700 border-emerald-200 dark:bg-emerald-950/50 dark:text-emerald-400 dark:border-emerald-900 hover:bg-emerald-50" : sub.status === "GRACE_PERIOD" || sub.status === "PAST_DUE" ? "bg-amber-50 text-amber-700 border-amber-200 dark:bg-amber-950/50 dark:text-amber-400 dark:border-amber-900 hover:bg-amber-50" : sub.status === "SUSPENDED" ? "bg-rose-50 text-rose-700 border-rose-200 dark:bg-rose-950/50 dark:text-rose-400 dark:border-rose-900 hover:bg-rose-50" : sub.status === "PENDING" ? "bg-secondary text-muted-foreground border-border/60 hover:bg-secondary" : "bg-zinc-100 text-zinc-400 border-border/60 dark:bg-zinc-900 dark:text-zinc-500 line-through hover:bg-zinc-100 dark:hover:bg-zinc-900"}`}>{sub.status.replace("_", " ")}</Badge>
                      )}
                    </TableCell>
                    <TableCell className="text-right align-middle">
                      <button onClick={(e) => handleMenuClick(e, sub.id)} className="p-1.5 rounded-none hover:bg-secondary border border-transparent hover:border-border/60 text-muted-foreground hover:text-foreground transition-colors inline-flex"><MoreHorizontal size={14} /></button>
                    </TableCell>
                  </TableRow>
                );
              })
            )}
          </TableBody>
        </Table>
      </div>

      {openMenuId && (
        <>
          <div className="fixed inset-0 z-40" onClick={() => setOpenMenuId(null)} />
          <div className="fixed w-56 bg-card border border-border/60 rounded-none shadow-[4px_4px_0px_0px_rgba(0,0,0,0.05)] z-50 py-1 text-left animate-in fade-in slide-in-from-top-1 duration-100" style={{ top: menuPos.top + 8, right: menuPos.right }}>
            {(() => {
              const sub = subscribers.find(s => s.id === openMenuId);
              if (!sub) return null;
              return (
                <>
                  <button onClick={() => { setEditSub(sub); setOpenMenuId(null); }} className="w-full flex items-center gap-2.5 px-4 py-2 text-xs text-foreground hover:bg-secondary font-bold uppercase tracking-wide"><Edit size={14} />Edit Subscriber</button>
                  <button onClick={() => { setHistorySub(sub); setOpenMenuId(null); }} className="w-full flex items-center gap-2.5 px-4 py-2 text-xs text-foreground hover:bg-secondary font-bold uppercase tracking-wide"><History size={14} />View Payment History</button>
                  <button onClick={() => { setReminderHistorySub(sub); setOpenMenuId(null); }} className="w-full flex items-center gap-2.5 px-4 py-2 text-xs text-foreground hover:bg-secondary font-bold uppercase tracking-wide"><MessageSquare size={14} />View Message Logs</button>
                  <hr className="border-border/60 my-1" />
                  
                  {sub.status !== "CANCELED" && sub.status !== "CANCELLED" && (
                    <>
                      <button onClick={() => { setRecordPaymentSub(sub); setOpenMenuId(null); }} className="w-full flex items-center gap-2.5 px-4 py-2 text-xs text-emerald-700 dark:text-emerald-500 hover:bg-secondary font-bold uppercase tracking-wide"><Check size={14} />Record Payment</button>
                      <button onClick={() => { setReminderSub(sub); setOpenMenuId(null); }} className="w-full flex items-center gap-2.5 px-4 py-2 text-xs text-blue-700 dark:text-blue-500 hover:bg-secondary font-bold uppercase tracking-wide"><Send size={14} />Send Reminder</button>
                      <button onClick={() => { setScheduleSub(sub); setOpenMenuId(null); }} className="w-full flex items-center gap-2.5 px-4 py-2 text-xs text-purple-700 dark:text-purple-500 hover:bg-secondary font-bold uppercase tracking-wide"><CalendarClock size={14} />Schedule Reminder</button>
                      <button onClick={() => { setPauseReminderSub(sub); setOpenMenuId(null); }} className="w-full flex items-center gap-2.5 px-4 py-2 text-xs text-amber-700 dark:text-amber-500 hover:bg-secondary font-bold uppercase tracking-wide"><BellOff size={14} />Pause Reminders</button>
                      <button onClick={() => handleExtendGrace(sub.id, sub.customer_name)} className="w-full flex items-center gap-2.5 px-4 py-2 text-xs text-amber-700 dark:text-amber-500 hover:bg-secondary font-bold uppercase tracking-wide"><Calendar size={14} />Extend Grace Period</button>
                      <hr className="border-border/60 my-1" />
                    </>
                  )}
                  <button onClick={() => handleCancelSubscription(sub.id, sub.customer_name)} className="w-full flex items-center gap-2.5 px-4 py-2 text-xs text-rose-600 dark:text-rose-500 hover:bg-secondary font-bold uppercase tracking-wide"><XCircle size={14} />Cancel Subscription</button>
                </>
              );
            })()}
          </div>
        </>
      )}

      {/* Modals */}
      {showAddModal && <AddSubscriberModal onClose={() => setShowAddModal(false)} onSuccess={() => { queryClient.invalidateQueries({ queryKey: ["community-subs"] }); setShowAddModal(false); }} />}
      {editSub && <EditSubscriberModal sub={editSub} onClose={() => setEditSub(null)} onSuccess={() => { queryClient.invalidateQueries({ queryKey: ["community-subs"] }); setEditSub(null); }} />}
      {recordPaymentSub && <RecordPaymentModal sub={recordPaymentSub} onClose={() => setRecordPaymentSub(null)} onSuccess={() => { queryClient.invalidateQueries({ queryKey: ["community-subs"] }); setRecordPaymentSub(null); }} />}
      {historySub && <PaymentHistoryModal sub={historySub} onClose={() => setHistorySub(null)} />}
      {reminderHistorySub && <ReminderHistoryModal sub={reminderHistorySub} onClose={() => setReminderHistorySub(null)} />}
      {reminderSub && <SendReminderModal sub={reminderSub} onClose={() => setReminderSub(null)} />}
      {scheduleSub && <ScheduleReminderModal sub={scheduleSub} onClose={() => setScheduleSub(null)} />}
      {pauseReminderSub && <PauseRemindersModal sub={pauseReminderSub} onClose={() => setPauseReminderSub(null)} onSuccess={() => { queryClient.invalidateQueries({ queryKey: ["community-subs"] }); setPauseReminderSub(null); }} />}
    </div>
  );
}
