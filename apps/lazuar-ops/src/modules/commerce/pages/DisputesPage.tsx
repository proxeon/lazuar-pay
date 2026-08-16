import { useQuery } from "@tanstack/react-query";
import { Loader2 } from "lucide-react";
import { API_URL } from "../../../lib/api-client";
import { useOutletContext } from "react-router-dom";
import PageLayout from "../../core/components/PageLayout";

type CommerceDispute = {
  id: string;
  gateway_transaction_id: string;
  amount: number;
  currency: string;
  status: string;
  subscription_id?: string | null;
  created_at: string;
};

export default function DisputesPage() {
  const { activeWorkspaceId } = useOutletContext<{ activeWorkspaceId: string }>();

  const { data, isLoading } = useQuery({
    queryKey: ["commerce-disputes", activeWorkspaceId],
    queryFn: async () => {
      const res = await fetch(`${API_URL}/admin/commerce/disputes?page=1&limit=50`, {
        credentials: "include",
        headers: { "X-Tenant-Id": activeWorkspaceId },
      });
      if (!res.ok) throw new Error("Failed to load disputes");
      return (await res.json()) as { data: CommerceDispute[] };
    },
    enabled: !!activeWorkspaceId,
  });

  return (
    <PageLayout
      title="Disputes"
      description="Open card chargebacks on Commerce payments. Access stays active until you cancel."
      breadcrumbs={[{ label: "Commerce", href: "/commerce/dashboard" }, { label: "Disputes" }]}
    >
      <div className="bg-white border border-[#e5e5e5]">
        <table className="w-full text-left text-[13px]">
          <thead className="bg-[#fafafa] text-[10px] font-bold uppercase tracking-widest text-[#71717a]">
            <tr>
              <th className="px-4 py-2">Date</th>
              <th className="px-4 py-2">Amount</th>
              <th className="px-4 py-2">Subscription</th>
              <th className="px-4 py-2">Status</th>
            </tr>
          </thead>
          <tbody>
            {isLoading && (
              <tr>
                <td colSpan={4} className="px-4 py-8 text-center text-[#71717a]">
                  <Loader2 size={16} className="inline animate-spin" />
                </td>
              </tr>
            )}
            {data?.data.map((row) => (
              <tr key={row.id} className="border-t border-[#f4f4f5]">
                <td className="px-4 py-2 font-mono text-[12px]">{new Date(row.created_at).toLocaleString()}</td>
                <td className="px-4 py-2 font-mono">
                  {row.currency} {Number(row.amount).toFixed(2)}
                </td>
                <td className="px-4 py-2 font-mono text-[12px] text-[#71717a]">
                  {row.subscription_id ? row.subscription_id.slice(0, 8) : "—"}
                </td>
                <td className="px-4 py-2">
                  <span className="text-[10px] font-bold uppercase tracking-widest text-amber-700">{row.status}</span>
                </td>
              </tr>
            ))}
            {!isLoading && (data?.data.length ?? 0) === 0 && (
              <tr>
                <td colSpan={4} className="px-4 py-8 text-center text-[12px] text-[#71717a]">
                  No open disputes.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </PageLayout>
  );
}
