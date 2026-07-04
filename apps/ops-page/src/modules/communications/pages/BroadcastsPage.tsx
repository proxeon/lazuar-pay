import { useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { Loader2, Megaphone, Calculator } from "lucide-react";
import { toast } from "sonner";
import { client, API_URL } from "../../../lib/api-client";
import PageLayout from "../../core/components/PageLayout";

type CostPreview = {
  recipient_count: number;
  credits_per_recipient: number;
  total_credits: number;
  sufficient_credits: boolean;
  available_credits: number;
};

type BroadcastStatus = {
  id: string;
  status: string;
  total_recipients: number;
  sent_count: number;
  suppressed_count: number;
  failed_count: number;
  credits_used: number;
  failure_reason?: string | null;
};

function tenantHeaders(): HeadersInit {
  const tenantId = localStorage.getItem("ops_active_workspace_id");
  return tenantId ? { "X-Tenant-Id": tenantId } : {};
}

export default function BroadcastsPage() {
  const [subject, setSubject] = useState("");
  const [emailBody, setEmailBody] = useState("");
  const [preview, setPreview] = useState<CostPreview | null>(null);
  const [status, setStatus] = useState<BroadcastStatus | null>(null);
  const [polling, setPolling] = useState(false);

  const fetchPreview = async () => {
    const res = await fetch(`${API_URL}/admin/communications/broadcasts/preview`, {
      credentials: "include",
      headers: tenantHeaders()
    });
    if (!res.ok) throw new Error("Failed to estimate cost");
    setPreview((await res.json()) as CostPreview);
  };

  const broadcastMutation = useMutation({
    mutationFn: async () => {
      const { data, error } = await client.POST("/admin/communications/broadcasts", {
        body: { subject, email_body: emailBody, whatsapp_body: "", channel: "EMAIL" }
      });
      if (error) throw new Error(error.detail);
      return data!.id as string;
    },
    onSuccess: (id) => {
      toast.success("Broadcast queued. Credits reserved.");
      setSubject("");
      setEmailBody("");
      setPreview(null);
      pollStatus(id);
    },
    onError: (err: any) => toast.error(err.message || "Failed to queue broadcast.")
  });

  const pollStatus = (id: string) => {
    setPolling(true);
    const interval = setInterval(async () => {
      try {
        const res = await fetch(`${API_URL}/admin/communications/broadcasts/${id}`, {
          credentials: "include",
          headers: tenantHeaders()
        });
        if (!res.ok) return;
        const s = (await res.json()) as BroadcastStatus;
        setStatus(s);
        if (s.status === "COMPLETED" || s.status === "FAILED") {
          clearInterval(interval);
          setPolling(false);
          if (s.status === "FAILED") toast.error(`Broadcast failed: ${s.failure_reason ?? "unknown"}`);
          else toast.success(`Broadcast complete: ${s.sent_count} sent, ${s.suppressed_count} suppressed.`);
        }
      } catch {
        /* keep polling */
      }
    }, 2000);
  };

  const canSend = subject.trim() !== "" && emailBody.trim() !== "" && (preview?.sufficient_credits ?? false);

  return (
    <PageLayout
      title="Bulk Broadcast"
      description="Send a marketing email to all your active subscribers. Credits are reserved up front and consumed per recipient."
      breadcrumbs={[{ label: "Communications" }, { label: "Bulk Broadcast" }]}
    >
      <div className="bg-white border border-[#e5e5e5] rounded-none flex flex-col min-h-[600px]">
        <div className="p-6 md:p-8 flex-1 bg-[#fafafa]/30">
          <form onSubmit={(e) => { e.preventDefault(); if (canSend) broadcastMutation.mutate(); }} className="max-w-3xl space-y-6">
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Audience</label>
              <div className="h-10 border border-[#e5e5e5] bg-white px-3 flex items-center text-[13px] text-[#71717a]">
                All Active Subscribers (Email Only)
              </div>
            </div>

            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Subject Line</label>
              <input required value={subject} onChange={e => setSubject(e.target.value)} className="w-full h-10 border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b]" placeholder="e.g. Important update regarding next week's session" />
            </div>

            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Body (HTML)</label>
              <textarea required value={emailBody} onChange={e => setEmailBody(e.target.value)} rows={8} className="w-full p-3 border border-[#e5e5e5] bg-white text-[13px] focus:outline-none focus:border-[#09090b] resize-y font-mono" placeholder="Write your email HTML here..." />
            </div>

            <div className="pt-2 border-t border-[#f4f4f5] flex items-center justify-between flex-wrap gap-4">
              <button type="button" onClick={fetchPreview} className="h-10 px-5 border border-[#e5e5e5] bg-white text-[11px] font-bold uppercase tracking-widest hover:border-[#09090b] flex items-center gap-2">
                <Calculator size={14} /> Estimate Cost
              </button>
              <button type="submit" disabled={!canSend || broadcastMutation.isPending} className="h-10 px-8 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest hover:bg-[#27272a] transition-colors flex items-center gap-2 disabled:opacity-50">
                {broadcastMutation.isPending ? <Loader2 size={14} className="animate-spin" /> : <Megaphone size={14} />}
                Dispatch Broadcast
              </button>
            </div>

            {preview && (
              <div className="border border-[#e5e5e5] bg-white p-4 text-[12px] font-mono grid grid-cols-2 gap-2">
                <span>Recipients:</span><span className="text-right">{preview.recipient_count}</span>
                <span>Credits per recipient:</span><span className="text-right">{preview.credits_per_recipient}</span>
                <span>Total credits:</span><span className="text-right">{preview.total_credits}</span>
                <span>Available:</span><span className="text-right">{preview.available_credits}</span>
                <span>Sufficient:</span>
                <span className={`text-right ${preview.sufficient_credits ? "text-emerald-600" : "text-red-600"}`}>
                  {preview.sufficient_credits ? "Yes" : "No — top up first"}
                </span>
              </div>
            )}

            {status && (
              <div className="border border-[#e5e5e5] bg-white p-4 text-[12px] font-mono">
                <div className="flex items-center gap-2 mb-2">
                  {polling && <Loader2 size={12} className="animate-spin" />}
                  <span className="font-bold uppercase tracking-widest text-[11px]">{status.status}</span>
                </div>
                <div className="grid grid-cols-3 gap-2">
                  <span>Sent: {status.sent_count}</span>
                  <span>Suppressed: {status.suppressed_count}</span>
                  <span>Failed: {status.failed_count}</span>
                  <span>Total: {status.total_recipients}</span>
                  <span>Credits used: {status.credits_used}</span>
                </div>
              </div>
            )}
          </form>
        </div>
      </div>
    </PageLayout>
  );
}
