// apps/community-admin/src/components/ReminderHistoryModal.tsx

import { useQuery } from "@tanstack/react-query";
import { X, Loader2, MessageSquare } from "lucide-react";
import { api } from "../lib/api";
import type { Subscriber, DeliveryHistoryItem } from "../lib/api";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";

interface ReminderHistoryModalProps {
  sub: Subscriber;
  onClose: () => void;
}

export default function ReminderHistoryModal({ sub, onClose }: ReminderHistoryModalProps) {
  const { data: records, isLoading } = useQuery<DeliveryHistoryItem[]>({
    queryKey: ["community-reminders", sub.id],
    queryFn: () => api.getReminderHistory(sub.id),
  });

  const formatDate = (dateStr: string) => {
    return new Date(dateStr).toLocaleDateString("en-MY", { 
      year: 'numeric', month: 'short', day: 'numeric',
      hour: '2-digit', minute: '2-digit'
    });
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative bg-card border border-border/60 rounded-none shadow-[8px_8px_0px_0px_rgba(0,0,0,0.1)] w-full max-w-4xl overflow-hidden flex flex-col animate-in fade-in zoom-in-95 duration-200 max-h-[85vh]">
        <div className="flex items-center justify-between p-5 border-b border-border/60 shrink-0">
          <div>
            <h3 className="text-sm font-bold uppercase tracking-widest text-foreground flex items-center gap-2">
              <MessageSquare size={16} /> Delivery History
            </h3>
            <p className="text-[11px] text-muted-foreground mt-1">Message logs and delivery receipts for {sub.customer_name}.</p>
          </div>
          <button onClick={onClose} className="text-muted-foreground hover:bg-secondary rounded-none transition-colors p-1"><X size={16} /></button>
        </div>

        <div className="flex-1 overflow-auto p-0">
          <Table>
            <TableHeader className="sticky top-0 bg-secondary/80 backdrop-blur-sm z-10">
              <TableRow>
                <TableHead className="w-[160px] font-bold text-[10px] uppercase tracking-widest text-muted-foreground">Date / Time</TableHead>
                <TableHead className="w-[100px] font-bold text-[10px] uppercase tracking-widest text-muted-foreground">Channel</TableHead>
                <TableHead className="font-bold text-[10px] uppercase tracking-widest text-muted-foreground">Message</TableHead>
                <TableHead className="w-[120px] font-bold text-[10px] uppercase tracking-widest text-muted-foreground text-right">Status</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoading ? (
                <TableRow>
                  <TableCell colSpan={4} className="text-center py-12">
                    <Loader2 className="h-5 w-5 animate-spin mx-auto text-muted-foreground" />
                    <p className="text-[10px] uppercase tracking-widest text-muted-foreground mt-3 font-bold">Loading Logs...</p>
                  </TableCell>
                </TableRow>
              ) : !records || records.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={4} className="text-center py-12 text-[11px] font-bold uppercase tracking-widest text-muted-foreground">
                    No messages sent yet.
                  </TableCell>
                </TableRow>
              ) : (
                records.map((record) => (
                  <TableRow key={record.id} className="hover:bg-secondary/40">
                    <TableCell className="align-middle">
                      <div className="text-xs font-medium text-foreground">{formatDate(record.created_at)}</div>
                    </TableCell>
                    <TableCell className="align-middle">
                      <Badge variant="outline" className={`text-[9px] uppercase tracking-widest rounded-none border px-1.5 py-0 ${
                        record.channel === "WHATSAPP" ? "bg-emerald-50 text-emerald-700 border-emerald-200" 
                        : "bg-blue-50 text-blue-700 border-blue-200"
                      }`}>{record.channel}</Badge>
                      <div className="text-[10px] text-muted-foreground font-mono mt-1 tracking-tight truncate max-w-[120px]" title={record.recipient}>
                        {record.recipient}
                      </div>
                    </TableCell>
                    <TableCell className="align-middle">
                      <div className="text-xs font-semibold text-foreground">{record.template_name || "(Custom Message)"}</div>
                      {record.subject && (
                        <div className="text-[11px] text-muted-foreground mt-0.5 tracking-tight truncate max-w-[300px]" title={record.subject}>
                          Subj: {record.subject}
                        </div>
                      )}
                    </TableCell>
                    <TableCell className="align-middle text-right">
                      <Badge variant="outline" className={`text-[9px] uppercase tracking-widest rounded-none border px-1.5 py-0 ${
                        record.status === "SENT" ? "bg-emerald-50 text-emerald-700 border-emerald-200" 
                        : record.status.startsWith("SKIPPED") ? "bg-amber-50 text-amber-700 border-amber-200"
                        : "bg-rose-50 text-rose-700 border-rose-200"
                      }`}>{record.status.replace(/_/g, ' ')}</Badge>
                      
                      {record.error_message && (
                        <div className="text-[9px] text-rose-600 dark:text-rose-400 font-mono mt-1 tracking-tight truncate max-w-[150px] ml-auto" title={record.error_message}>
                          {record.error_message}
                        </div>
                      )}
                    </TableCell>
                  </TableRow>
                ))
              )}
            </TableBody>
          </Table>
        </div>
      </div>
    </div>
  );
}
