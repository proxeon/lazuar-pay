import { useQuery } from "@tanstack/react-query";
import { X, Loader2 } from "lucide-react";
import { client } from "../lib/api-client";
import type { Subscriber, PaymentRecord } from "../lib/api-client";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";

interface PaymentHistoryModalProps {
  sub: Subscriber;
  onClose: () => void;
}

export default function PaymentHistoryModal({ sub, onClose }: PaymentHistoryModalProps) {
  const { data: records, isLoading } = useQuery<PaymentRecord[]>({
    queryKey: ["community-payments", sub.id],
    queryFn: async () => {
        // Fallback to raw fetch since endpoint mapping wasn't added to TypeSpec in Phase 1
        const res = await fetch(`${client.baseUrl}/admin/community/subscribers/${sub.id}/payments`, {
            headers: { Authorization: `Bearer ${localStorage.getItem("community_admin_token")}` }
        });
        if (!res.ok) throw new Error("Failed to fetch payment history");
        return await res.json();
    },
  });

  const formatMethod = (method: string) => {
    return method.replace(/_/g, ' ').replace(/\b\w/g, l => l.toUpperCase());
  };

  const formatDate = (dateStr: string) => {
    return new Date(dateStr).toLocaleDateString("en-MY", { year: 'numeric', month: 'short', day: 'numeric' });
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative bg-card border border-border/60 rounded-none shadow-[8px_8px_0px_0px_rgba(0,0,0,0.1)] w-full max-w-3xl overflow-hidden flex flex-col animate-in fade-in zoom-in-95 duration-200 max-h-[85vh]">
        <div className="flex items-center justify-between p-5 border-b border-border/60 shrink-0">
          <div>
            <h3 className="text-sm font-bold uppercase tracking-widest text-foreground">Payment History</h3>
            <p className="text-[11px] text-muted-foreground mt-1">Transaction ledger for {sub.customer_name}.</p>
          </div>
          <button onClick={onClose} className="text-muted-foreground hover:bg-secondary rounded-none transition-colors p-1"><X size={16} /></button>
        </div>

        <div className="flex-1 overflow-auto p-0">
          <Table>
            <TableHeader className="sticky top-0 bg-secondary/80 backdrop-blur-sm z-10">
              <TableRow>
                <TableHead className="w-[120px] font-bold text-[10px] uppercase tracking-widest text-muted-foreground">Date</TableHead>
                <TableHead className="font-bold text-[10px] uppercase tracking-widest text-muted-foreground">Amount</TableHead>
                <TableHead className="font-bold text-[10px] uppercase tracking-widest text-muted-foreground">Method & Ref</TableHead>
                <TableHead className="font-bold text-[10px] uppercase tracking-widest text-muted-foreground">Period Covered</TableHead>
                <TableHead className="font-bold text-[10px] uppercase tracking-widest text-muted-foreground">Recorded By</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {isLoading ? (
                <TableRow>
                  <TableCell colSpan={5} className="text-center py-12">
                    <Loader2 className="h-5 w-5 animate-spin mx-auto text-muted-foreground" />
                    <p className="text-[10px] uppercase tracking-widest text-muted-foreground mt-3 font-bold">Loading Ledger...</p>
                  </TableCell>
                </TableRow>
              ) : !records || records.length === 0 ? (
                <TableRow>
                  <TableCell colSpan={5} className="text-center py-12 text-[11px] font-bold uppercase tracking-widest text-muted-foreground">
                    No payments recorded yet.
                  </TableCell>
                </TableRow>
              ) : (
                records.map((record) => (
                  <TableRow key={record.id} className="hover:bg-secondary/40">
                    <TableCell className="align-middle">
                      <div className="text-xs font-medium text-foreground">{formatDate(record.created_at)}</div>
                      <Badge variant="outline" className={`text-[9px] mt-1 uppercase tracking-widest rounded-none border px-1.5 py-0 ${
                        record.status === "CONFIRMED" ? "bg-emerald-50 text-emerald-700 border-emerald-200" 
                        : record.status === "REFUNDED" ? "bg-rose-50 text-rose-700 border-rose-200" 
                        : "bg-amber-50 text-amber-700 border-amber-200"
                      }`}>{record.status}</Badge>
                    </TableCell>
                    <TableCell className="align-middle">
                      <div className="text-sm font-bold font-mono tracking-tight text-foreground">
                        {record.currency} {record.amount.toFixed(2)}
                      </div>
                    </TableCell>
                    <TableCell className="align-middle">
                      <div className="text-xs font-semibold text-foreground">{formatMethod(record.payment_method)}</div>
                      {record.reference_number && (
                        <div className="text-[10px] text-muted-foreground font-mono mt-0.5 tracking-tight truncate max-w-[150px]" title={record.reference_number}>
                          Ref: {record.reference_number}
                        </div>
                      )}
                    </TableCell>
                    <TableCell className="align-middle">
                      <div className="text-[11px] text-foreground tabular-nums">
                        {formatDate(record.period_start)} <span className="text-muted-foreground mx-1">→</span> {formatDate(record.period_end)}
                      </div>
                    </TableCell>
                    <TableCell className="align-middle">
                      <div className="text-[11px] text-muted-foreground truncate max-w-[120px]" title={record.recorded_by || "System"}>
                        {record.recorded_by || "SYSTEM"}
                      </div>
                      {record.notes && (
                        <div className="text-[10px] text-muted-foreground italic mt-0.5 truncate max-w-[120px]" title={record.notes}>
                          "{record.notes}"
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
