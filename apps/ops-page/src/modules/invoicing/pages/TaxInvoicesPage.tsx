// apps/ops-page/src/modules/invoicing/pages/TaxInvoicesPage.tsx
import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { Loader2, ArrowLeft, ArrowRight, Receipt } from "lucide-react";
import { client, type components } from "../../../lib/api-client";
import { cn } from "../../../lib/utils";
import PageLayout from "../../core/components/PageLayout";
import TaxInvoiceDetailPanel from "../components/TaxInvoiceDetailPanel";

type BaseLedgerEntryDto = components["schemas"]["Billing.LedgerEntryDto"];

interface LedgerEntryExtended extends BaseLedgerEntryDto {
  customer_type?: string;
  tax_invoice_id?: string;
  lhdn_validation_status?: string;
}

export default function TaxInvoicesPage() {
  const [page, setPage] = useState(1);
  const [selectedInvoice, setSelectedInvoice] = useState<LedgerEntryExtended | null>(null);
  
  const limit = 50;

  const { data: response, isLoading } = useQuery({
    queryKey: ["billing-ledger-invoices", page],
    queryFn: async () => {
      const { data, error } = await client.GET("/admin/billing/ledger", {
        params: { query: { page, limit } }
      });
      if (error) throw new Error(error.detail);
      return data;
    }
  });

  const validInvoices = (response?.data as LedgerEntryExtended[] | undefined)?.filter(
    (entry) => entry.reference_type !== "GATEWAY_REFUND" && entry.reference_type !== "LHDN_CANCELLATION"
  ) || [];

  const handlePrev = () => setPage((p) => Math.max(1, p - 1));
  const handleNext = () => {
    if (response && page < response.total_pages) {
      setPage((p) => p + 1);
    }
  };

  const getInvoiceMath = (lines: any[]) => {
    let netAmount = 0;
    let taxAmount = 0;
    
    lines.forEach((line) => {
      if (line.account_type === "REVENUE_GROSS" || line.account_type === "REVENUE_RECOGNIZED") {
        netAmount += Math.abs(line.base_currency_amount);
      }
      if (line.account_type === "LIABILITY_TAX_PAYABLE") {
        taxAmount += Math.abs(line.base_currency_amount);
      }
    });
    
    return { netAmount, taxAmount };
  };

  const getLhdnBadgeClasses = (status?: string) => {
    switch (status) {
      case "VALIDATED":
        return "bg-emerald-50 text-emerald-700 border-emerald-200";
      case "SUBMITTED":
      case "PENDING":
        return "bg-amber-50 text-amber-700 border-amber-200 animate-pulse";
      case "B2C_RECEIPT":
      case "CONSOLIDATED_PENDING":
        return "bg-blue-50 text-blue-700 border-blue-200";
      case "REJECTED":
      case "CANCELLED":
        return "bg-rose-50 text-rose-700 border-rose-200";
      default:
        return "bg-zinc-100 text-zinc-600 border-zinc-200";
    }
  };

  return (
    <PageLayout 
      title="Tax Invoices & Receipts" 
      description="View and manage official tax documents and their LHDN e-Invoicing compliance status."
      breadcrumbs={[{ label: "Invoicing" }, { label: "Tax Invoices" }]}
    >
      <div className="bg-white border border-[#e5e5e5] rounded-none flex flex-col h-full min-h-[600px]">
        <div className="px-5 py-4 border-b border-[#f4f4f5] flex items-center justify-between bg-[#fafafa]/50">
          <div className="flex items-center gap-2 text-[11px] text-[#71717a] font-mono">
            <Receipt size={14} /> Official Ledger Records
          </div>
          <div className="text-[11px] text-[#71717a] font-mono">
            {response ? `Total Ledgers: ${response.total_count}` : "..."}
          </div>
        </div>

        <div className="w-full overflow-x-auto flex-1">
          <table className="w-full text-left text-[13px] min-w-[900px]">
            <thead className="bg-white border-b border-[#f4f4f5] select-none">
              <tr>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[15%]">Date</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[20%]">Reference ID</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[15%]">Type</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[15%] text-right">Net Amount</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[15%] text-right">Tax</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[20%]">LHDN Status</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#f4f4f5]">
              {isLoading ? (
                <tr><td colSpan={6} className="py-12 text-center text-[#a1a1aa]"><Loader2 size={20} className="animate-spin mx-auto" /></td></tr>
              ) : validInvoices.length === 0 ? (
                <tr><td colSpan={6} className="py-12 text-center text-[12px] text-[#71717a]">No tax invoices found.</td></tr>
              ) : (
                validInvoices.map((entry) => {
                  const { netAmount, taxAmount } = getInvoiceMath(entry.lines);
                  const displayId = entry.tax_invoice_id || entry.id.substring(0, 8).toUpperCase();

                  return (
                    <tr 
                      key={entry.id} 
                      onClick={() => setSelectedInvoice(entry)}
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
                          {entry.customer_type || "B2C"}
                        </span>
                      </td>
                      <td className="px-5 py-4 text-right">
                        <span className="font-mono font-bold text-[#09090b]">RM {netAmount.toFixed(2)}</span>
                      </td>
                      <td className="px-5 py-4 text-right">
                        <span className="font-mono text-[#52525b]">RM {taxAmount.toFixed(2)}</span>
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

      {selectedInvoice && (
        <TaxInvoiceDetailPanel 
          invoice={selectedInvoice} 
          onClose={() => setSelectedInvoice(null)} 
        />
      )}
    </PageLayout>
  );
}
