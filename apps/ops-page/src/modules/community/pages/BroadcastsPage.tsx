import { useState } from "react";
import { useQuery, useMutation } from "@tanstack/react-query";
import { Loader2, Megaphone } from "lucide-react";
import { toast } from "sonner";
import { client } from "../../../lib/api-client";
import PageLayout from "../../core/components/PageLayout";

export default function BroadcastsPage() {
  const [subject, setSubject] = useState("");
  const [emailBody, setEmailBody] = useState("");
  const [whatsappBody, setWhatsappBody] = useState("");
  const [channel, setChannel] = useState("ALL");
  const [targetProductId, setTargetProductId] = useState("");

  const { data: products } = useQuery({
    queryKey: ["commerce-products"],
    queryFn: async () => {
      const { data } = await client.GET("/admin/commerce/products");
      return data || [];
    }
  });

  const broadcastMutation = useMutation({
    mutationFn: async () => {
      const { error } = await client.POST("/admin/communications/broadcasts", {
        body: {
          subject,
          email_body: emailBody,
          whatsapp_body: whatsappBody,
          channel,
          target_plan_id: targetProductId || undefined,
          target_status: "ACTIVE"
        }
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("Broadcast dispatched to the background queue.");
      setSubject("");
      setEmailBody("");
      setWhatsappBody("");
    },
    onError: (err: any) => toast.error(err.message)
  });

  return (
    <PageLayout 
      title="Bulk Broadcast" 
      description="Send manual mass announcements to your active subscribers."
      breadcrumbs={[{ label: "Community", href: "/community/dashboard" }, { label: "Bulk Broadcast" }]}
    >
      <div className="bg-white border border-[#e5e5e5] rounded-none flex flex-col min-h-[600px] relative">
        <div className="p-6 md:p-8 flex-1 bg-[#fafafa]/30">
          <form onSubmit={(e) => { e.preventDefault(); broadcastMutation.mutate(); }} className="max-w-3xl space-y-6">
            
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Target Audience</label>
                <select value={targetProductId} onChange={e => setTargetProductId(e.target.value)} className="w-full h-10 border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b]">
                  <option value="">All Active Subscribers (Global)</option>
                  {products?.map((p: any) => <option key={p.id} value={p.id}>Purchased: {p.name}</option>)}
                </select>
              </div>
              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Delivery Channel</label>
                <select value={channel} onChange={e => setChannel(e.target.value)} className="w-full h-10 border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b]">
                  <option value="ALL">Email & WhatsApp</option>
                  <option value="EMAIL">Email Only</option>
                  <option value="WHATSAPP">WhatsApp Only</option>
                </select>
              </div>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              {(channel === "EMAIL" || channel === "ALL") && (
                <div className="space-y-4">
                  <h3 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1.5">Email Message</h3>
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Subject Line</label>
                    <input required={channel === "EMAIL" || channel === "ALL"} value={subject} onChange={e => setSubject(e.target.value)} className="w-full h-10 border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b]" placeholder="e.g. Important update regarding next week's session" />
                  </div>
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Body (HTML/Markdown)</label>
                    <textarea required={channel === "EMAIL" || channel === "ALL"} value={emailBody} onChange={e => setEmailBody(e.target.value)} rows={6} className="w-full p-3 border border-[#e5e5e5] bg-white text-[13px] focus:outline-none focus:border-[#09090b] resize-y font-mono" placeholder="Write your email here..." />
                  </div>
                </div>
              )}

              {(channel === "WHATSAPP" || channel === "ALL") && (
                <div className="space-y-4">
                  <h3 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1.5">WhatsApp Message</h3>
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Body (Plain Text)</label>
                    <textarea required={channel === "WHATSAPP" || channel === "ALL"} value={whatsappBody} onChange={e => setWhatsappBody(e.target.value)} rows={8} className="w-full p-3 border border-[#e5e5e5] bg-white text-[13px] focus:outline-none focus:border-[#09090b] resize-y font-sans" placeholder="Write your WhatsApp message here..." />
                  </div>
                </div>
              )}
            </div>

            <div className="pt-2 border-t border-[#f4f4f5]">
              <button type="submit" disabled={broadcastMutation.isPending} className="mt-4 h-10 px-8 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest hover:bg-[#27272a] transition-colors flex items-center gap-2 disabled:opacity-50">
                {broadcastMutation.isPending ? <Loader2 size={14} className="animate-spin" /> : <Megaphone size={14} />} 
                Dispatch Broadcast
              </button>
            </div>

          </form>
        </div>
      </div>
    </PageLayout>
  );
}
