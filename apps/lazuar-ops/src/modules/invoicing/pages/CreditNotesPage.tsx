import { useState } from "react";
import { useSearchParams } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { Loader2, ArrowLeft, ArrowRight, FileMinus, Info, Search } from "lucide-react";
import { client, type components } from "../../../lib/api-client";
import { cn } from "../../../lib/utils";
import PageLayout from "../../core/components/PageLayout";
import TaxInvoiceDetailPanel from "../components/TaxInvoiceDetailPanel";
import { useDebounce } from "../../../hooks/use-debounce";

type BaseLedgerEntryDto = components["schemas"]["Billing.LedgerEntryDto"];

interface LedgerEntryExtended extends BaseLedgerEntryDto {
  customer_type?: string;
  tax_invoice_id?: string;
  customer_document_number?: string;
  lhdn_validation_status?: string;
}

export default function CreditNotesPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const initialSearch = searchParams.get("search") || "";

  const [page, setPage] = useState(1);
  const [searchTerm, setSearchTerm] = useState(initialSearch);
  const debouncedSearchTerm = useDebounce(searchTerm, 300);
  const [selectedNote, setSelectedNote] = useState<LedgerEntryExtended | null>(null);
  
  const limit = 50;

  const { data: response, isLoading } = useQuery({
    queryKey: ["billing-ledger-credit-notes", page, debouncedSearchTerm],
    queryFn: async () => {
      const { data, error } = await client.GET("/admin/billing/ledger", {
        params: { 
          query: { 
            page, 
            limit,
            search: debouncedSearchTerm || undefined,
            type_filter: "reversals" 
          } 
        }
      });
      if (error) throw new Error(error.detail);
      return data;
    }
  });

  const creditNotes = (response?.data as LedgerEntryExtended[] | undefined) || [];

  const handlePrev = () => setPage((p) => Math.max(1, p - 1));
  const handleNext = () => {
    if (response && page < response.total_pages) {
      setPage((p) => p + 1);
    }
  };

  const handleSearchChange = (val: string) => {
    setSearchTerm(val);
    setPage(1);
    
    const newParams = new URLSearchParams(searchParams);
    if (val) newParams.set("search", val);
    else newParams.delete("search");
    setSearchParams(newParams, { replace: true });
  };

  const getNoteMath = (lines: any[]) => {
    let refundAmount = 0;
    let taxAmount = 0;
    
    lines.forEach((line) => {
      if (line.account_type === "CONTRA_REVENUE_REFUNDS" || line.account_type === "REVENUE_GROSS") {
        refundAmount += Math.abs(line.base_currency_amount);
      }
      if (line.account_type === "LIABILITY_TAX_PAYABLE") {
        taxAmount += Math.abs(line.base_currency_amount);
      }
    });
    
    return { refundAmount, taxAmount };
  };

  const getLhdnBadgeClasses = (status?: string) => {
    switch (status) {
      case "VALID":
        return "bg-emerald-50 text-emerald-700 border-emerald-200";
      case "SUBMITTED":
      case "PENDING":
        return "bg-amber-50 text-amber-700 border-amber-200 animate-pulse";
      case "REJECTED":
        return "bg-rose-50 text-rose-700 border-rose-200";
      default:
        return "bg-zinc-100 text-zinc-600 border-zinc-200";
    }
  };

  return (
    <PageLayout 
      title="Credit Notes" 
      description="Audit contra-revenue records, refunds, and e-Invoice cancellations. Debit notes are not issued."
      breadcrumbs={[{ label: "Invoicing" }, { label: "Credit Notes" }]}
    >
      <div className="flex flex-col gap-6">
        
        <div className="flex items-start gap-3 p-4 bg-blue-50 border border-blue-200 rounded-sm">
          <Info size={16} className="text-blue-600 mt-0.5 shrink-0" />
          <div className="text-[12px] text-blue-800 leading-relaxed">
            <strong className="block mb-1 text-[11px] uppercase tracking-widest text-blue-700">Automated Integrity</strong>
            Credit Notes are generated automatically when a refund is issued or an e-Invoice is cancelled. Manual creation of Credit Notes is restricted to preserve double-entry ledger integrity.
          </div>
        </div>

        <div className="bg-white border border-[#e5e5e5] rounded-none flex flex-col h-full min-h-[500px]">
          <div className="px-5 py-4 border-b border-[#f4f4f5] flex items-center justify-between bg-[#fafafa]/50">
            <div className="flex items-center gap-3">
              <div className="relative w-64">
                <Search size={14} className="absolute left-3 top-2 text-[#a1a1aa]" />
                <input 
                  type="text" 
                  placeholder="Search reference ID or UUID..." 
                  value={searchTerm}
                  onChange={e => handleSearchChange(e.target.value)}
                  className="w-full h-8 pl-9 pr-3 text-[12px] bg-white border border-[#e5e5e5] focus:outline-none focus:border-[#09090b]" 
                />
              </div>
              <div className="flex items-center gap-2 text-[11px] text-[#71717a] font-mono hidden sm:flex">
                <FileMinus size={14} /> Contra-Revenue Ledger
              </div>
            </div>
            <div className="text-[11px] text-[#71717a] font-mono">
              {response ? `Total Notes: ${response.total_count}` : "..."}
            </div>
          </div>

          <div className="w-full overflow-x-auto flex-1">
            <table className="w-full text-left text-[13px] min-w-[900px]">
              <thead className="bg-white border-b border-[#f4f4f5] select-none">
                <tr>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[15%]">Date</th>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[20%]">Reference ID</th>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[15%]">Trigger</th>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[15%] text-right">Refund Amount</th>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[15%] text-right">Tax Reversal</th>
                  <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[20%]">LHDN Status</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#f4f4f5]">
                {isLoading ? (
                  <tr><td colSpan={6} className="py-12 text-center text-[#a1a1aa]"><Loader2 size={20} className="animate-spin mx-auto" /></td></tr>
                ) : creditNotes.length === 0 ? (
                  <tr><td colSpan={6} className="py-12 text-center text-[12px] text-[#71717a]">No credit notes found.</td></tr>
                ) : (
                  creditNotes.map((entry) => {
                    const { refundAmount, taxAmount } = getNoteMath(entry.lines);
                    const displayId = entry.customer_document_number || entry.tax_invoice_id || entry.id.substring(0, 8).toUpperCase();
                    const triggerName = entry.reference_type === "GATEWAY_REFUND" ? "Refund" : "Cancellation";

                    return (
                      <tr 
                        key={entry.id} 
                        onClick={() => setSelectedNote(entry)}
                        className="hover:bg-[#fafafa] transition-colors cursor-pointer group"
                      >
                        <td className="px-5 py-4 whitespace-nowrap">
                          <p className="text-[12px] font-medium text-[#09090b]">
                            {new Date(entry.timestamp).toLocaleDateString('en-GB')}
                          </p>
                          <p className="text-[10px] font-mono text-[#71717a] mt-0.5">
                            {new Date(entry.timestamp).toLocaleTimeString('en-GB')}
                          </p>
                        </td>
                        <td className="px-5 py-4">
                          <span className="font-mono font-bold text-[#09090b] text-[12px] group-hover:text-blue-600 transition-colors">
                            {displayId}
                          </span>
                        </td>
                        <td className="px-5 py-4">
                          <span className="text-[11px] font-semibold text-[#52525b] uppercase tracking-wider">
                            {triggerName}
                          </span>
                        </td>
                        <td className="px-5 py-4 text-right">
                          <span className="font-mono font-bold text-amber-600">- RM {refundAmount.toFixed(2)}</span>
                        </td>
                        <td className="px-5 py-4 text-right">
                          <span className="font-mono text-amber-600">- RM {taxAmount.toFixed(2)}</span>
                        </td>
                        <td className="px-5 py-4">
                          <span className={cn(
                            "text-[9px] px-2 py-0.5 border font-bold uppercase tracking-widest whitespace-nowrap",
                            getLhdnBadgeClasses(entry.lhdn_validation_status)
                          )}>
                            {entry.lhdn_validation_status?.replace("_", " ") || "NOT REQUIRED"}
                          </span>
                        </td>
                      </tr>
                    );
                  })
                )}
              </tbody>
            </table>
          </div>

          <div className="px-5 py-3 border-t border-[#e5e5e5] bg-[#fafafa]/50 flex items-center justify-between shrink-0">
            <span className="text-[11px] text-[#71717a]">
              Page {page} of {response?.total_pages || 1}
            </span>
            <div className="flex items-center gap-2">
              <button 
                onClick={handlePrev} 
                disabled={page <= 1 || isLoading}
                className="h-8 px-3 border border-[#e5e5e5] bg-white text-[#09090b] text-[11px] font-bold uppercase tracking-widest hover:bg-[#f4f4f5] transition-colors disabled:opacity-50 flex items-center gap-1.5 rounded-sm"
              >
                <ArrowLeft size={14} /> Prev
              </button>
              <button 
                onClick={handleNext} 
                disabled={!response || page >= response.total_pages || isLoading}
                className="h-8 px-3 border border-[#e5e5e5] bg-white text-[#09090b] text-[11px] font-bold uppercase tracking-widest hover:bg-[#f4f4f5] transition-colors disabled:opacity-50 flex items-center gap-1.5 rounded-sm"
              >
                Next <ArrowRight size={14} />
              </button>
            </div>
          </div>
        </div>
      </div>

      {selectedNote && (
        <TaxInvoiceDetailPanel 
          invoice={selectedNote} 
          onClose={() => setSelectedNote(null)} 
        />
      )}
    </PageLayout>
  );
}
