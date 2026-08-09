import { useQuery, useQueryClient } from "@tanstack/react-query";
import { Loader2, RefreshCw, ArrowUpRight, ArrowDownLeft } from "lucide-react";
import { client, type components } from "../../../lib/api-client";
import PageLayout from "../../core/components/PageLayout";
import { cn } from "../../../lib/utils";

type CreditBalanceDto = components["schemas"]["Billing.CreditBalanceDto"];

export default function UtilityLedgerPage() {
  const queryClient = useQueryClient();

  const { data: balanceData, isLoading } = useQuery({
    queryKey: ["tenant-credits"],
    queryFn: async () => {
      const { data, error } = await client.GET("/admin/billing/credits");
      if (error) throw new Error(error.detail);
      return data as CreditBalanceDto;
    }
  });

  return (
    <PageLayout
      title="Utility Ledger"
      description="Audit your credit consumption history and top-ups."
      breadcrumbs={[{ label: "Workspace" }, { label: "Utility Ledger" }]}
      actionButton={
        <button 
          onClick={() => queryClient.invalidateQueries({ queryKey: ["tenant-credits"] })}
          disabled={isLoading}
          className="h-9 px-4 bg-white border border-[#e5e5e5] text-[#09090b] text-[11px] font-bold uppercase tracking-widest flex items-center gap-2 hover:bg-[#fafafa] transition-colors disabled:opacity-50"
        >
          <RefreshCw size={14} className={cn(isLoading && "animate-spin")} /> Refresh
        </button>
      }
    >
      <div className="bg-white border border-[#e5e5e5] rounded-none flex flex-col h-full min-h-[600px]">
        <div className="w-full overflow-x-auto">
          <table className="w-full text-left text-[13px] min-w-[700px]">
            <thead className="bg-[#fafafa] border-b border-[#e5e5e5] select-none">
              <tr>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[20%]">Date & Time</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[60%]">Description / Reference</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] text-right w-[20%]">Amount</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#f4f4f5]">
              {isLoading ? (
                <tr>
                  <td colSpan={3} className="py-12 text-center text-[#a1a1aa]">
                    <Loader2 className="animate-spin mx-auto" size={20} />
                  </td>
                </tr>
              ) : !balanceData?.recent_transactions || balanceData.recent_transactions.length === 0 ? (
                <tr>
                  <td colSpan={3} className="py-12 text-center text-[#71717a] text-[13px]">
                    No credit transactions found.
                  </td>
                </tr>
              ) : (
                balanceData.recent_transactions.map((tx, idx) => {
                  const isAddition = tx.amount > 0;
                  return (
                    <tr key={idx} className="hover:bg-[#fafafa] transition-colors">
                      <td className="px-5 py-4 whitespace-nowrap">
                        <span className="text-[12px] text-[#52525b] font-mono">
                          {new Date(tx.created_at).toLocaleString('en-GB', { dateStyle: 'short', timeStyle: 'short' })}
                        </span>
                      </td>
                      <td className="px-5 py-4">
                        <span className="text-[13px] text-[#09090b] font-medium">
                          {tx.reference}
                        </span>
                      </td>
                      <td className={cn("px-5 py-4 font-mono font-bold text-right whitespace-nowrap flex items-center justify-end gap-1.5", isAddition ? "text-emerald-600" : "text-[#09090b]")}>
                        {isAddition ? <ArrowDownLeft size={14} /> : <ArrowUpRight size={14} className="text-[#a1a1aa]" />}
                        {isAddition ? '+' : ''}{tx.amount}
                      </td>
                    </tr>
                  );
                })
              )}
            </tbody>
          </table>
        </div>
      </div>
    </PageLayout>
  );
}
