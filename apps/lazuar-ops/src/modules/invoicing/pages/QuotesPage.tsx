import { useState } from "react";
import { useOutletContext } from "react-router-dom";
import { useQuery } from "@tanstack/react-query";
import { Loader2, Plus, ArrowLeft, ArrowRight, FileText } from "lucide-react";
import { client, type components } from "../../../lib/api-client";
import { cn } from "../../../lib/utils";
import PageLayout from "../../core/components/PageLayout";
import QuoteDetailPanel from "../components/QuoteDetailPanel";
import CreateQuoteModal from "../components/CreateQuoteModal";

type CustomCheckoutDto = components["schemas"]["Commerce.CustomCheckoutDto"];

export default function QuotesPage() {
  const { activeWorkspaceId } = useOutletContext<{ activeWorkspaceId: string | null }>();
  const [page, setPage] = useState(1);
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);
  const [selectedRequest, setSelectedRequest] = useState<CustomCheckoutDto | null>(null);
  
  const limit = 50;

  const { data: response, isLoading } = useQuery({
    queryKey: ["custom-checkouts", activeWorkspaceId, page],
    queryFn: async () => {
      const { data, error } = await client.GET("/admin/commerce/custom-checkouts", {
        params: { query: { page, limit } }
      });
      if (error) throw new Error(error.detail);
      return data;
    },
    enabled: !!activeWorkspaceId
  });

  const handlePrev = () => setPage((p) => Math.max(1, p - 1));
  const handleNext = () => {
    if (response && page < response.total_pages) {
      setPage((p) => p + 1);
    }
  };

  return (
    <PageLayout 
      title="Quotes & Proforma Invoices" 
      description="Create custom, one-off quotes and proforma invoices for ad-hoc services or B2B clients."
      breadcrumbs={[{ label: "Invoicing" }, { label: "Quotes & Requests" }]}
      actionButton={
        <button 
          onClick={() => setIsCreateModalOpen(true)}
          className="h-9 px-4 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest flex items-center gap-2 hover:bg-[#27272a] transition-colors"
        >
          <Plus size={14} /> Create Quote
        </button>
      }
    >
      <div className="bg-white border border-[#e5e5e5] rounded-none flex flex-col h-full min-h-[600px]">
        <div className="px-5 py-4 border-b border-[#f4f4f5] flex items-center justify-between bg-[#fafafa]/50">
          <div className="flex items-center gap-2 text-[11px] text-[#71717a] font-mono">
            <FileText size={14} /> Tracking quotes &amp; proforma
          </div>
          <div className="text-[11px] text-[#71717a] font-mono">
            {response ? `Total: ${response.total_count}` : "..."}
          </div>
        </div>

        <div className="w-full overflow-x-auto flex-1">
          <table className="w-full text-left text-[13px] min-w-[800px]">
            <thead className="bg-white border-b border-[#f4f4f5] select-none">
              <tr>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[22%]">Client</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[13%]">Quote No.</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[15%]">Amount (MYR)</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[20%]">Status</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[20%]">Created</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[15%]">Expires</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#f4f4f5]">
              {isLoading ? (
                <tr><td colSpan={6} className="py-12 text-center text-[#a1a1aa]"><Loader2 size={20} className="animate-spin mx-auto" /></td></tr>
              ) : response?.data.length === 0 ? (
                <tr><td colSpan={6} className="py-12 text-center text-[12px] text-[#71717a]">No quotes found.</td></tr>
              ) : (
                response?.data.map((req) => (
                  <tr 
                    key={req.id} 
                    onClick={() => setSelectedRequest(req)}
                    className="hover:bg-[#fafafa] transition-colors cursor-pointer group"
                  >
                    <td className="px-5 py-4">
                      <p className="font-medium text-[#09090b] text-[13px] group-hover:text-blue-600 transition-colors">{req.client_name || "Unknown"}</p>
                      <p className="text-[11px] text-[#71717a] mt-0.5">{req.client_email}</p>
                    </td>
                    <td className="px-5 py-4">
                      <span className="font-mono text-[12px] text-[#09090b]">{req.document_number || "—"}</span>
                    </td>
                    <td className="px-5 py-4">
                      <span className="font-mono font-bold text-[#09090b]">RM {req.total_amount.toFixed(2)}</span>
                    </td>
                    <td className="px-5 py-4">
                      <span className={cn(
                        "text-[9px] px-1.5 py-0.5 border font-bold uppercase tracking-widest",
                        req.status === "COMPLETED" ? "bg-emerald-50 text-emerald-700 border-emerald-200" :
                        req.status === "EXPIRED" ? "bg-rose-50 text-rose-700 border-rose-200" :
                        "bg-amber-50 text-amber-700 border-amber-200"
                      )}>
                        {req.status}
                      </span>
                    </td>
                    <td className="px-5 py-4 text-[11px] font-mono text-[#52525b]">
                      {new Date(req.created_at).toLocaleDateString('en-GB')}
                    </td>
                    <td className="px-5 py-4 text-[11px] font-mono text-[#52525b]">
                      {new Date(req.expires_at).toLocaleDateString('en-GB')}
                    </td>
                  </tr>
                ))
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

      <CreateQuoteModal 
        isOpen={isCreateModalOpen} 
        onClose={() => setIsCreateModalOpen(false)} 
      />

      <QuoteDetailPanel 
        request={selectedRequest} 
        onClose={() => setSelectedRequest(null)} 
        onUpdate={setSelectedRequest}
      />
    </PageLayout>
  );
}
