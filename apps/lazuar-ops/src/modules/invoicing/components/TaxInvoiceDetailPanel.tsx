import { useState, useMemo, useEffect } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Loader2, X, Download, AlertTriangle, FileText } from "lucide-react";
import { toast } from "sonner";
import { API_URL, client, type components } from "../../../lib/api-client";
import { cn } from "../../../lib/utils";
import SidePanel from "../../core/components/SidePanel";
import QuickCopy from "../../core/components/QuickCopy";
import { classifySalesDocument } from "../lib/salesDocumentType";

type BaseLedgerEntryDto = components["schemas"]["Billing.LedgerEntryDto"];

interface LedgerEntryExtended extends BaseLedgerEntryDto {
  customer_type?: string;
  tax_invoice_id?: string;
  customer_document_number?: string;
  lhdn_validation_status?: string;
}

interface TaxInvoiceDetailPanelProps {
  invoice: LedgerEntryExtended | null;
  onClose: () => void;
}

export default function TaxInvoiceDetailPanel({ invoice, onClose }: TaxInvoiceDetailPanelProps) {
  const queryClient = useQueryClient();
  const [isCancelModalOpen, setIsCancelModalOpen] = useState(false);
  const [cancelReason, setCancelReason] = useState("");
  const [isDownloading, setIsDownloading] = useState(false);

  const lhdnInternalId = invoice?.customer_document_number
    || (invoice?.tax_invoice_id && !/^[0-9a-f-]{36}$/i.test(invoice.tax_invoice_id) ? invoice.tax_invoice_id : null);

  const { data: lhdnDoc } = useQuery({
    queryKey: ["lhdn-document", lhdnInternalId],
    enabled: !!lhdnInternalId,
    queryFn: async () => {
      const { data, error } = await client.GET("/lhdn/documents/{internalId}", {
        params: { path: { internalId: lhdnInternalId! } },
      });
      if (error) throw new Error(error.detail || "LHDN document not found.");
      return data;
    },
    refetchInterval: (query) => {
      const status = query.state.data?.status;
      return status === "PENDING" || status === "SUBMITTED" ? 5000 : false;
    },
  });

  const cancelMutation = useMutation({
    mutationFn: async () => {
      const internalId = invoice?.customer_document_number
        || (invoice?.tax_invoice_id && !/^[0-9a-f-]{36}$/i.test(invoice.tax_invoice_id) ? invoice.tax_invoice_id : null);
      if (!internalId) throw new Error("Cannot cancel without a customer document number.");
      if (!cancelReason.trim()) throw new Error("A reason is required by LHDN for cancellation.");

      // Client baseUrl is already .../api/v1 — path must be relative (/lhdn/...), not /api/v1/lhdn/...
      const { error } = await client.POST("/lhdn/documents/{internalId}/cancel", {
        params: { path: { internalId } },
        body: { reason: cancelReason.trim() },
      });

      if (error) throw new Error(error.detail || "LHDN cancellation failed.");
    },
    onSuccess: () => {
      toast.success("e-Invoice cancelled successfully. Contra-entries recorded.");
      queryClient.invalidateQueries({ queryKey: ["billing-ledger-invoices"] });
      setIsCancelModalOpen(false);
      onClose();
    },
    onError: (err: any) => toast.error("Cancellation Failed", { description: err.message })
  });

  const handleDownload = async () => {
    if (!invoice) return;
    setIsDownloading(true);
    try {
      const { data, error } = await client.GET("/admin/billing/ledger/{id}/document", {
        params: { path: { id: invoice.id } }
      });
      if (error || !data) throw new Error(error?.detail || "Failed to generate download link.");
      
      window.open(data.url, "_blank");
    } catch (err: any) {
      toast.error("Download failed", { description: err.message });
    } finally {
      setIsDownloading(false);
    }
  };

  const math = useMemo(() => {
    if (!invoice) return { subtotal: 0, discount: 0, tax: 0, total: 0, currency: "MYR", lines: [] };

    let subtotal = 0;
    let discount = 0;
    let tax = 0;
    let currency = "MYR";
    const displayLines: any[] = [];

    invoice.lines.forEach((line) => {
      currency = line.currency;
      if (line.account_type === "REVENUE_GROSS" || line.account_type === "REVENUE_RECOGNIZED") {
        subtotal += Math.abs(line.amount);
        displayLines.push({ description: invoice.description || "Sales Revenue", amount: Math.abs(line.amount), type: "revenue" });
      }
      if (line.account_type === "EXPENSE_DISCOUNT") {
        discount += Math.abs(line.amount);
        displayLines.push({ description: "Discount Applied", amount: -Math.abs(line.amount), type: "discount" });
      }
      if (line.account_type === "LIABILITY_TAX_PAYABLE") {
        tax += Math.abs(line.amount);
      }
    });

    const total = subtotal - discount + tax;

    return { subtotal, discount, tax, total, currency, lines: displayLines };
  }, [invoice]);

  const liveStatus = lhdnDoc?.status || invoice?.lhdn_validation_status;
  const qrLink = liveStatus === "VALID" ? lhdnDoc?.qr_link : undefined;

  const { data: qrImageUrl } = useQuery({
    queryKey: ["lhdn-document-qr", lhdnInternalId, qrLink],
    enabled: !!invoice && !!lhdnInternalId && !!qrLink,
    queryFn: async () => {
      const tenantId = localStorage.getItem("ops_active_workspace_id");
      const res = await fetch(
        `${API_URL}/lhdn/documents/${encodeURIComponent(lhdnInternalId!)}/qr`,
        {
          credentials: "include",
          headers: tenantId ? { "X-Tenant-Id": tenantId } : undefined,
        }
      );
      if (!res.ok) throw new Error("QR unavailable");
      return URL.createObjectURL(await res.blob());
    },
  });

  useEffect(() => {
    return () => {
      if (qrImageUrl) URL.revokeObjectURL(qrImageUrl);
    };
  }, [qrImageUrl]);

  if (!invoice) return null;

  const displayId = invoice.customer_document_number || invoice.tax_invoice_id || invoice.id.substring(0, 8).toUpperCase();
  const isLhdnValidated = liveStatus === "VALID";
  const validatedAtMs = lhdnDoc?.validated_at ? new Date(lhdnDoc.validated_at).getTime() : NaN;
  const hoursSinceValid = Number.isFinite(validatedAtMs)
    ? (Date.now() - validatedAtMs) / (1000 * 60 * 60)
    : Number.POSITIVE_INFINITY;
  const documentKind = classifySalesDocument(invoice);
  const isTaxInvoice = documentKind === "Tax Invoice";
  const isCancelable = isTaxInvoice && isLhdnValidated && hoursSinceValid < 72;

  const getLhdnBadgeClasses = (status?: string) => {
    switch (status) {
      case "VALID": return "bg-emerald-50 text-emerald-700 border-emerald-200";
      case "SUBMITTED":
      case "PENDING": return "bg-amber-50 text-amber-700 border-amber-200 animate-pulse";
      case "B2C_RECEIPT":
      case "CONSOLIDATED_PENDING": return "bg-blue-50 text-blue-700 border-blue-200";
      case "INVALID":
      case "NEEDS_BUYER_TIN":
      case "REJECTED":
      case "CANCELLED": return "bg-rose-50 text-rose-700 border-rose-200";
      default: return "bg-zinc-100 text-zinc-600 border-zinc-200";
    }
  };

  return (
    <>
      <SidePanel
        isOpen={!!invoice}
        onClose={onClose}
        title={`${documentKind} details`}
        disableOutsideClick={cancelMutation.isPending || isDownloading}
      >
        <div className="space-y-8 animate-in fade-in duration-200">
          
          <div className="flex items-start justify-between border-b border-[#f4f4f5] pb-6">
            <div>
              <h3 className="text-2xl font-bold tracking-tight font-mono text-[#09090b]">
                {math.currency} {math.total.toFixed(2)}
              </h3>
              <div className="flex items-center gap-2 mt-1.5">
                <span className={cn(
                  "text-[10px] px-2 py-0.5 border font-bold uppercase tracking-widest whitespace-nowrap",
                  getLhdnBadgeClasses(liveStatus)
                )}>
                  {liveStatus?.replace("_", " ") || "NOT REQUIRED"}
                </span>
                <span className="text-[11px] text-[#71717a] font-mono">
                  {new Date(invoice.timestamp).toLocaleString('en-GB')}
                </span>
              </div>
            </div>
            <div className="h-12 w-12 bg-[#f4f4f5] border border-[#e5e5e5] flex items-center justify-center rounded-none shrink-0">
               <FileText size={20} className="text-[#09090b]" />
            </div>
          </div>

          <div className="space-y-4">
            <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Ledger Breakdown</h4>
            <div className="border border-[#e5e5e5] rounded-sm overflow-hidden">
              <table className="w-full text-left text-[12px]">
                <thead className="bg-[#fafafa] border-b border-[#e5e5e5]">
                  <tr>
                    <th className="px-3 py-2 font-semibold text-[#71717a]">Item / Account</th>
                    <th className="px-3 py-2 font-semibold text-[#71717a] text-right">Amount</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[#f4f4f5]">
                  {math.lines.map((item, idx) => (
                    <tr key={idx} className="bg-white">
                      <td className="px-3 py-2.5 font-medium text-[#09090b]">{item.description}</td>
                      <td className={cn("px-3 py-2.5 font-mono text-right", item.type === 'discount' ? 'text-rose-600' : 'text-[#52525b]')}>
                        {item.amount.toFixed(2)}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              <div className="bg-[#fafafa] p-4 border-t border-[#e5e5e5] space-y-2">
                <div className="flex justify-between text-[12px] text-[#71717a]">
                  <span>Subtotal</span>
                  <span className="font-mono">RM {math.subtotal.toFixed(2)}</span>
                </div>
                {math.discount > 0 && (
                  <div className="flex justify-between text-[12px] text-rose-600">
                    <span>Discount</span>
                    <span className="font-mono">- RM {math.discount.toFixed(2)}</span>
                  </div>
                )}
                <div className="flex justify-between text-[12px] text-[#71717a]">
                  <span>{math.tax > 0 ? "SST" : "Tax"} ({invoice.customer_type === 'B2C' ? 'Inclusive' : 'Added'})</span>
                  <span className="font-mono">RM {math.tax.toFixed(2)}</span>
                </div>
                <div className="flex justify-between text-[13px] font-bold text-[#09090b] pt-2 border-t border-[#e5e5e5]">
                  <span>Total</span>
                  <span className="font-mono">RM {math.total.toFixed(2)}</span>
                </div>
              </div>
            </div>
          </div>

          <div className="space-y-4">
            <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">Compliance & Metadata</h4>
            <div className="bg-[#fafafa]/50 border border-[#e5e5e5] p-4 rounded-sm space-y-3">
              <div className="grid grid-cols-2 gap-4 text-[12px]">
                <div>
                  <span className="text-[#a1a1aa] block mb-1">Customer Type</span>
                  <span className="font-semibold text-[#09090b]">{invoice.customer_type || "B2C"}</span>
                </div>
                <div>
                  <span className="text-[#a1a1aa] block mb-1">Internal Reference</span>
                  <div className="flex items-center gap-1.5">
                    <span className="font-mono text-[#52525b] truncate max-w-[120px]" title={invoice.id}>{invoice.id.substring(0,8)}...</span>
                    <QuickCopy text={invoice.id} iconSize={10} className="hover:bg-white" />
                  </div>
                </div>
              </div>
              
              {(invoice.customer_document_number || invoice.tax_invoice_id) && (
                <div className="pt-3 border-t border-[#e5e5e5]">
                  <span className="text-[#a1a1aa] block mb-1 text-[12px]">
                    {invoice.customer_document_number ? "Document number" : "Official Document ID / LHDN UUID"}
                  </span>
                  <div className="flex items-center gap-2">
                    <span className="font-mono font-bold text-[#09090b] text-[13px] truncate" title={displayId}>
                      {displayId}
                    </span>
                    <QuickCopy text={invoice.customer_document_number || invoice.tax_invoice_id || ""} iconSize={12} className="hover:bg-white" />
                  </div>
                </div>
              )}
              {lhdnDoc?.error_message && liveStatus === "INVALID" && (
                <p className="text-[12px] text-rose-700 border-t border-[#e5e5e5] pt-3">{lhdnDoc.error_message}</p>
              )}
              {qrLink && (
                <div className="pt-3 border-t border-[#e5e5e5] space-y-2">
                  <span className="text-[#a1a1aa] block text-[12px]">MyInvois share QR</span>
                  {qrImageUrl ? (
                    <img
                      alt="MyInvois QR"
                      className="h-28 w-28 border border-[#e5e5e5] bg-white"
                      src={qrImageUrl}
                    />
                  ) : (
                    <div className="h-28 w-28 border border-[#e5e5e5] bg-[#fafafa]" />
                  )}
                  <a href={qrLink} target="_blank" rel="noreferrer" className="text-[11px] font-mono text-blue-700 break-all underline">
                    {qrLink}
                  </a>
                </div>
              )}
            </div>
          </div>

          <div className="space-y-4 pt-4 border-t border-[#f4f4f5]">
            <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] pb-1">Operations</h4>
            <div className="grid grid-cols-1 gap-3">
              <button 
                onClick={handleDownload}
                disabled={isDownloading || cancelMutation.isPending}
                className="h-9 w-full border border-[#e5e5e5] bg-white text-[11px] font-bold uppercase tracking-widest text-[#09090b] hover:bg-[#f4f4f5] transition-colors flex items-center justify-center gap-1.5 rounded-sm disabled:opacity-50"
              >
                {isDownloading ? <Loader2 size={14} className="animate-spin" /> : <Download size={14} />} Download PDF Document
              </button>
              
              {isTaxInvoice && (
                <>
                {isCancelable ? (
                  <button 
                    onClick={() => setIsCancelModalOpen(true)}
                    className="h-9 w-full border border-rose-200 bg-rose-50 text-[11px] font-bold uppercase tracking-widest text-rose-700 hover:bg-rose-100 transition-colors flex items-center justify-center gap-1.5 rounded-sm"
                  >
                    <AlertTriangle size={14} /> Cancel e-Invoice (LHDN)
                  </button>
                ) : (
                  <div className="h-9 w-full border border-zinc-200 bg-zinc-50 text-[11px] font-bold uppercase tracking-widest text-zinc-400 flex items-center justify-center rounded-sm cursor-not-allowed" title="The 72-hour cancellation window has expired. Issue a Credit Note instead.">
                    Cancel window closed — issue a credit note
                  </div>
                )}
              <p className="text-[11px] text-[#71717a] leading-relaxed">
                Supplier cancel only, within 72 hours of MyInvois VALID. Buyer reject is not implemented.
              </p>
                </>
              )}
            </div>
          </div>

        </div>
      </SidePanel>

      {isCancelModalOpen && (
        <div className="fixed inset-0 z-[60] flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/20 backdrop-blur-sm" onClick={() => !cancelMutation.isPending && setIsCancelModalOpen(false)} />
          <form onSubmit={(e) => { e.preventDefault(); cancelMutation.mutate(); }} className="relative bg-white border border-rose-200 shadow-2xl w-full max-w-sm flex flex-col animate-in zoom-in-95 duration-200">
            <div className="p-4 border-b border-rose-200 bg-rose-50 flex items-center justify-between">
              <h3 className="text-[13px] font-bold uppercase tracking-widest text-rose-700">Cancel e-Invoice</h3>
              <button type="button" onClick={() => setIsCancelModalOpen(false)} disabled={cancelMutation.isPending} className="text-rose-400 hover:text-rose-700 disabled:opacity-50 p-1"><X size={16} /></button>
            </div>
            <div className="p-5 space-y-4">
              <div className="flex items-start gap-2 p-3 bg-amber-50 border border-amber-200 rounded-sm">
                <AlertTriangle size={14} className="text-amber-600 mt-0.5 shrink-0" />
                <p className="text-[11px] text-amber-800 leading-relaxed">
                  You are about to permanently cancel this document with LHDN. This action is irreversible and will immediately generate contra-entries in your ledger.
                </p>
              </div>
              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Reason for Cancellation *</label>
                <input required type="text" value={cancelReason} onChange={e => setCancelReason(e.target.value)} disabled={cancelMutation.isPending} placeholder="e.g. Incorrect buyer TIN provided" className="w-full h-9 border border-[#e5e5e5] px-3 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50" />
                <p className="text-[10px] text-[#a1a1aa] mt-1">LHDN requires a justification to void the UUID.</p>
              </div>
            </div>
            <div className="p-4 border-t border-rose-100 bg-rose-50/50 flex justify-end gap-2">
              <button type="button" onClick={() => setIsCancelModalOpen(false)} disabled={cancelMutation.isPending} className="px-4 h-8 text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:bg-[#e5e5e5] hover:text-[#09090b] border border-[#e5e5e5] bg-white transition-colors disabled:opacity-50 rounded-sm">Abort</button>
              <button type="submit" disabled={cancelMutation.isPending} className="px-5 h-8 bg-rose-600 text-white text-[11px] font-bold uppercase tracking-widest hover:bg-rose-700 disabled:opacity-50 flex items-center gap-1.5 rounded-sm">
                {cancelMutation.isPending && <Loader2 size={13} className="animate-spin" />} Confirm Cancellation
              </button>
            </div>
          </form>
        </div>
      )}
    </>
  );
}
