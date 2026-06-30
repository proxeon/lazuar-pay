import { useState } from "react";
import { useQuery, useMutation } from "@tanstack/react-query";
import { Loader2, Coins, ArrowRight } from "lucide-react";
import { toast } from "sonner";
import { client, type components } from "../../../lib/api-client";
import PageLayout from "../../core/components/PageLayout";
import { cn } from "../../../lib/utils";

type CreditBalanceDto = components["schemas"]["Billing.CreditBalanceDto"];

export default function BillingSettingsPage() {
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
      <div className="max-w-xl bg-white border border-[#e5e5e5] p-8 md:p-12 flex flex-col items-center justify-center text-center">
        <div className="h-16 w-16 rounded-full bg-emerald-50 flex items-center justify-center mb-6">
          <Coins size={32} className="text-emerald-600" />
        </div>
        <h3 className="text-[12px] font-bold uppercase tracking-widest text-[#71717a] mb-2">Available Balance</h3>
        <div className="text-5xl font-mono font-bold text-[#09090b] mb-8">
          {isLoading ? <Loader2 size={32} className="animate-spin mx-auto text-[#a1a1aa]" /> : balanceData?.available_credits || 0}
        </div>
        <p className="text-[13px] text-[#71717a] leading-relaxed mb-8 max-w-sm">
          Credits are consumed automatically when the system performs high-value background tasks like tax e-Invoicing or sending WhatsApp recovery messages.
        </p>
        
        <form onSubmit={handleTopUp} className="w-full max-w-sm space-y-4">
          <div className="grid grid-cols-3 gap-2">
            <button type="button" onClick={() => setTopUpAmount(50)} className={cn("h-10 border text-[13px] font-mono font-bold transition-colors", topUpAmount === 50 ? "border-[#09090b] bg-[#f4f4f5] text-[#09090b]" : "border-[#e5e5e5] bg-white text-[#71717a] hover:border-[#a1a1aa]")}>RM 50</button>
            <button type="button" onClick={() => setTopUpAmount(100)} className={cn("h-10 border text-[13px] font-mono font-bold transition-colors", topUpAmount === 100 ? "border-[#09090b] bg-[#f4f4f5] text-[#09090b]" : "border-[#e5e5e5] bg-white text-[#71717a] hover:border-[#a1a1aa]")}>RM 100</button>
            <button type="button" onClick={() => setTopUpAmount(200)} className={cn("h-10 border text-[13px] font-mono font-bold transition-colors", topUpAmount === 200 ? "border-[#09090b] bg-[#f4f4f5] text-[#09090b]" : "border-[#e5e5e5] bg-white text-[#71717a] hover:border-[#a1a1aa]")}>RM 200</button>
          </div>
          <button type="submit" disabled={topUpMutation.isPending} className="w-full h-12 bg-[#09090b] text-white text-[12px] font-bold tracking-widest uppercase hover:bg-[#27272a] transition-colors flex items-center justify-center gap-2 disabled:opacity-50">
            {topUpMutation.isPending ? <Loader2 size={16} className="animate-spin" /> : "Purchase Credits"} <ArrowRight size={16} />
          </button>
        </form>
      </div>
    </PageLayout>
  );
}
