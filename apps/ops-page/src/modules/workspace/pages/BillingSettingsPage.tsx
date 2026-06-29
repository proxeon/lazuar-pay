// apps/ops-page/src/modules/workspace/pages/BillingSettingsPage.tsx
import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2, Coins, ArrowRight, ArrowDownLeft, ArrowUpRight } from "lucide-react";
import { toast } from "sonner";
import { client, type components } from "../../../lib/api-client";
import PageLayout from "../../core/components/PageLayout";
import { cn } from "../../../lib/utils";

type CreditBalanceDto = components["schemas"]["Billing.CreditBalanceDto"];

export default function BillingSettingsPage() {
  const queryClient = useQueryClient();
  const [topUpAmount, setTopUpAmount] = useState<number>(50);

  const { data: balanceData, isLoading } = useQuery({
    queryKey: ["tenant-credits"],
    queryFn: async () => {
      const { data, error } = await client.GET("/admin/billing/credits");
      if (error) throw new Error(error.detail);
      return data as CreditBalanceDto;
    }
  });

  const topUpMutation = useMutation({
    mutationFn: async () => {
      const returnUrl = window.location.href;
      const { data, error } = await client.POST("/admin/billing/credits/top-up", {
        body: { amount_myr: topUpAmount, return_url: returnUrl }
      });
      if (error) throw new Error(error.detail);
      return data.checkout_url;
    },
    onSuccess: (url) => {
      window.location.href = url;
    },
    onError: (err: any) => toast.error(err.message || "Failed to initiate top-up.")
  });

  const handleTopUp = (e: React.FormEvent) => {
    e.preventDefault();
    topUpMutation.mutate();
  };

  return (
    <PageLayout
      title="Platform Billing"
      description="Manage your utility credits for automated LHDN tax submissions and WhatsApp dunning messages."
      breadcrumbs={[{ label: "Workspace" }, { label: "Platform Billing" }]}
    >
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 items-start">
        
        <div className="lg:col-span-1 space-y-6">
          <div className="bg-white border border-[#e5e5e5] p-6 flex flex-col items-center justify-center text-center">
            <div className="h-12 w-12 rounded-full bg-emerald-50 flex items-center justify-center mb-4">
              <Coins size={24} className="text-emerald-600" />
            </div>
            <h3 className="text-[11px] font-bold uppercase tracking-widest text-[#71717a] mb-1">Available Balance</h3>
            <div className="text-4xl font-mono font-bold text-[#09090b] mb-6">
              {isLoading ? <Loader2 size={24} className="animate-spin mx-auto text-[#a1a1aa]" /> : balanceData?.available_credits || 0}
            </div>
            <p className="text-[12px] text-[#71717a] leading-relaxed mb-6">
              Credits are consumed automatically when the system performs high-value background tasks.
            </p>
            <ul className="text-left w-full text-[12px] text-[#52525b] space-y-3 mb-8">
              <li className="flex justify-between border-b border-[#f4f4f5] pb-2"><span>LHDN e-Invoice</span><span className="font-mono font-bold">1 Credit</span></li>
              <li className="flex justify-between border-b border-[#f4f4f5] pb-2"><span>WhatsApp Automation</span><span className="font-mono font-bold">1 Credit</span></li>
              <li className="flex justify-between border-b border-[#f4f4f5] pb-2"><span>Email Broadcast</span><span className="font-mono font-bold">1 Credit</span></li>
            </ul>
            
            <form onSubmit={handleTopUp} className="w-full space-y-3">
              <div className="grid grid-cols-3 gap-2">
                <button type="button" onClick={() => setTopUpAmount(50)} className={cn("h-10 border text-[12px] font-mono font-bold transition-colors", topUpAmount === 50 ? "border-[#09090b] bg-[#f4f4f5] text-[#09090b]" : "border-[#e5e5e5] bg-white text-[#71717a] hover:border-[#a1a1aa]")}>RM 50</button>
                <button type="button" onClick={() => setTopUpAmount(100)} className={cn("h-10 border text-[12px] font-mono font-bold transition-colors", topUpAmount === 100 ? "border-[#09090b] bg-[#f4f4f5] text-[#09090b]" : "border-[#e5e5e5] bg-white text-[#71717a] hover:border-[#a1a1aa]")}>RM 100</button>
                <button type="button" onClick={() => setTopUpAmount(200)} className={cn("h-10 border text-[12px] font-mono font-bold transition-colors", topUpAmount === 200 ? "border-[#09090b] bg-[#f4f4f5] text-[#09090b]" : "border-[#e5e5e5] bg-white text-[#71717a] hover:border-[#a1a1aa]")}>RM 200</button>
              </div>
              <button type="submit" disabled={topUpMutation.isPending} className="w-full h-11 bg-[#09090b] text-white text-[11px] font-bold tracking-widest uppercase hover:bg-[#27272a] transition-colors flex items-center justify-center gap-2 disabled:opacity-50">
                {topUpMutation.isPending ? <Loader2 size={14} className="animate-spin" /> : "Top Up Balance"} <ArrowRight size={14} />
              </button>
            </form>
          </div>
        </div>

        <div className="lg:col-span-2 bg-white border border-[#e5e5e5] flex flex-col min-h-[500px]">
          <div className="px-5 py-4 border-b border-[#f4f4f5] bg-[#fafafa]/50">
            <h3 className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">Utility Ledger</h3>
          </div>
          <div className="flex-1 overflow-y-auto">
            {isLoading ? (
              <div className="flex justify-center p-12"><Loader2 className="animate-spin text-[#a1a1aa]" /></div>
            ) : !balanceData?.recent_transactions || balanceData.recent_transactions.length === 0 ? (
              <div className="flex flex-col items-center justify-center p-16 text-center text-[#71717a]">
                <Coins size={24} className="mb-4 opacity-50" />
                <p className="text-[13px]">No credit transactions found.</p>
              </div>
            ) : (
              <table className="w-full text-left text-[12px]">
                <thead className="bg-white border-b border-[#f4f4f5]">
                  <tr>
                    <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[20%]">Date</th>
                    <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[60%]">Description</th>
                    <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] text-right w-[20%]">Amount</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-[#f4f4f5]">
                  {balanceData.recent_transactions.map((tx, idx) => {
                    const isAddition = tx.amount > 0;
                    return (
                      <tr key={idx} className="hover:bg-[#fafafa] transition-colors group">
                        <td className="px-5 py-3.5 whitespace-nowrap text-[#52525b] font-mono">
                          {new Date(tx.created_at).toLocaleString('en-GB', { dateStyle: 'short', timeStyle: 'short' })}
                        </td>
                        <td className="px-5 py-3.5 text-[#09090b] font-medium truncate max-w-[300px]">
                          {tx.reference}
                        </td>
                        <td className={cn("px-5 py-3.5 font-mono font-bold text-right whitespace-nowrap flex items-center justify-end gap-1.5", isAddition ? "text-emerald-600" : "text-[#09090b]")}>
                          {isAddition ? <ArrowDownLeft size={12} /> : <ArrowUpRight size={12} className="text-[#a1a1aa]" />}
                          {isAddition ? '+' : ''}{tx.amount}
                        </td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            )}
          </div>
        </div>

      </div>
    </PageLayout>
  );
}
