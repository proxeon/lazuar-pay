import { useState } from "react";
import { Loader2, X, AlertTriangle } from "lucide-react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { client, type components } from "../../../lib/api-client";

type ProductDto = components["schemas"]["Commerce.ProductDto"];

interface CreateSubscriberModalProps {
  onClose: () => void;
}

export default function CreateSubscriberModal({ onClose }: CreateSubscriberModalProps) {
  const queryClient = useQueryClient();

  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [phone, setPhone] = useState("");
  const [productId, setProductId] = useState("");

  const [paymentMethod, setPaymentMethod] = useState("BANK_TRANSFER");
  const [amountPaid, setAmountPaid] = useState("");
  const [referenceNumber, setReferenceNumber] = useState("");

  const [sendWelcomeEmail, setSendWelcomeEmail] = useState(true);
  const [startDate, setStartDate] = useState("");
  const [nextBillingDate, setNextBillingDate] = useState("");

  const { data: products, isLoading: isProductsLoading } = useQuery({
    queryKey: ["commerce-products-lookup"],
    queryFn: async () => {
      const { data } = await client.GET("/admin/commerce/products");
      return data || [];
    }
  });

  const createMutation = useMutation({
    mutationFn: async () => {
      if (!productId) throw new Error("Please select a commerce product.");

      const isComped = paymentMethod === "COMPED";
      const finalAmount = isComped ? 0 : parseFloat(amountPaid);

      if (!isComped && (isNaN(finalAmount) || finalAmount <= 0)) {
        throw new Error("Amount paid must be greater than RM 0 unless Comped is selected.");
      }

      // Note: Endpoint expects "plan_id" under the hood due to backwards compatibility in the DTO
      // but we bind it logically to "productId" in the frontend.
      const { error } = await client.POST("/admin/community/subscribers", {
        body: {
          name: name.trim(),
          email: email.trim().toLowerCase(),
          phone: phone.trim(),
          plan_id: productId, 
          source: "MANUAL_ENTRY",
          is_reminder_only: true,
          amount_paid: finalAmount,
          payment_method: paymentMethod,
          reference_number: referenceNumber.trim() || undefined,
          send_welcome_email: sendWelcomeEmail,
          start_date: startDate ? new Date(startDate).toISOString() : undefined,
          next_billing_date: nextBillingDate ? new Date(nextBillingDate).toISOString() : undefined
        } as any // Bypassing Strict typing temporarily as this endpoint will be fully migrated to commerce soon
      });

      if (error) throw new Error(error.detail || "Failed to create subscriber.");
    },
    onSuccess: () => {
      toast.success("Subscriber successfully enrolled.");
      queryClient.invalidateQueries({ queryKey: ["commerce-subscribers"] });
      queryClient.invalidateQueries({ queryKey: ["commerce-stats"] });
      onClose();
    },
    onError: (err: any) => toast.error(err.message)
  });

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/20 backdrop-blur-sm transition-opacity" onClick={() => !createMutation.isPending && onClose()} />
      <div className="relative bg-white border border-[#e5e5e5] shadow-2xl w-full max-w-xl flex flex-col animate-in fade-in zoom-in-95 duration-200 max-h-[90vh]">
        
        <div className="flex items-center justify-between p-4 border-b border-[#e5e5e5] bg-[#fafafa]/50 shrink-0">
          <h3 className="text-[13px] font-bold uppercase tracking-widest text-[#09090b]">Manually Add Subscriber</h3>
          <button onClick={onClose} disabled={createMutation.isPending} className="text-[#a1a1aa] hover:bg-[#e5e5e5] hover:text-[#09090b] transition-colors p-1 disabled:opacity-50"><X size={16} /></button>
        </div>

        <div className="overflow-y-auto flex-1 bg-[#fafafa]/30">
          <form onSubmit={(e) => { e.preventDefault(); createMutation.mutate(); }}>
            <div className="p-6 space-y-8">

              <div className="space-y-4">
                <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">1. Profile & Product</label>
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-1.5 col-span-2 sm:col-span-1">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Full Name *</label>
                    <input required type="text" value={name} onChange={e => setName(e.target.value)} disabled={createMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" placeholder="e.g. Ahmad Ali" />
                  </div>
                  <div className="space-y-1.5 col-span-2 sm:col-span-1">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Email Address *</label>
                    <input required type="email" value={email} onChange={e => setEmail(e.target.value)} disabled={createMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" placeholder="name@example.com" />
                  </div>
                  <div className="space-y-1.5 col-span-2 sm:col-span-1">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Phone Number *</label>
                    <input required type="tel" value={phone} onChange={e => setPhone(e.target.value)} disabled={createMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" placeholder="+60123456789" />
                  </div>
                  <div className="space-y-1.5 col-span-2 sm:col-span-1">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Target Product *</label>
                    <select required value={productId} onChange={e => setProductId(e.target.value)} disabled={createMutation.isPending || isProductsLoading} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50">
                      <option value="" disabled>Select a product...</option>
                      {products?.map((p: ProductDto) => <option key={p.id} value={p.id}>{p.name} (RM {p.price})</option>)}
                    </select>
                  </div>
                </div>
              </div>

              <div className="space-y-4">
                <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">2. Financials</label>
                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Payment Method *</label>
                  <select value={paymentMethod} onChange={e => { setPaymentMethod(e.target.value); if (e.target.value === "COMPED") setAmountPaid("0"); }} disabled={createMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50">
                    <option value="BANK_TRANSFER">Bank Transfer (Manual)</option>
                    <option value="CASH">Cash</option>
                    <option value="COMPED">Complimentary (Free Access)</option>
                  </select>
                </div>
                
                {paymentMethod !== "COMPED" && (
                  <div className="grid grid-cols-2 gap-4">
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Amount Paid (MYR) *</label>
                      <input required type="number" step="0.01" value={amountPaid} onChange={e => setAmountPaid(e.target.value)} disabled={createMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" placeholder="0.00" />
                    </div>
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Reference ID (Optional)</label>
                      <input type="text" value={referenceNumber} onChange={e => setReferenceNumber(e.target.value)} disabled={createMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 font-mono text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" placeholder="e.g. TR-99812" />
                    </div>
                  </div>
                )}
                <div className="flex items-start gap-2 p-3 bg-amber-50 border border-amber-200 rounded-sm">
                  <AlertTriangle size={14} className="text-amber-600 mt-0.5 shrink-0" />
                  <p className="text-[11px] text-amber-800 leading-relaxed">
                    This user will not have an auto-debit card on file. They will automatically be flagged as <strong>Reminder Only</strong> and will receive manual payment links upon renewal.
                  </p>
                </div>
              </div>

              <div className="space-y-4">
                <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">3. Advanced Overrides (Optional)</label>
                
                <label className="flex items-center gap-2 cursor-pointer mt-2 w-fit">
                  <input type="checkbox" checked={sendWelcomeEmail} onChange={e => setSendWelcomeEmail(e.target.checked)} disabled={createMutation.isPending} className="rounded-sm border-[#e5e5e5] text-[#09090b] focus:ring-[#09090b]" />
                  <span className="text-[12px] font-medium text-[#09090b]">Send automated Welcome Email & Access Links</span>
                </label>

                <div className="grid grid-cols-2 gap-4 pt-2">
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Override Start Date</label>
                    <input type="datetime-local" value={startDate} onChange={e => setStartDate(e.target.value)} disabled={createMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
                  </div>
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Override Next Billing Date</label>
                    <input type="datetime-local" value={nextBillingDate} onChange={e => setNextBillingDate(e.target.value)} disabled={createMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
                  </div>
                  <p className="text-[10px] text-[#a1a1aa] col-span-2">Leave blank to use current time and auto-calculate billing intervals.</p>
                </div>
              </div>

            </div>

            <div className="px-5 py-4 border-t border-[#e5e5e5] bg-[#fafafa]/50 flex items-center justify-end gap-2.5 shrink-0">
              <button type="button" onClick={onClose} disabled={createMutation.isPending} className="h-8 px-4 rounded-sm border border-[#e5e5e5] bg-white text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:bg-[#f4f4f5] hover:text-[#09090b] transition-colors disabled:opacity-50">Cancel</button>
              <button type="submit" disabled={createMutation.isPending} className="h-8 px-6 rounded-sm bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest flex items-center justify-center gap-2 hover:bg-[#27272a] transition-colors disabled:opacity-50">
                {createMutation.isPending && <Loader2 size={13} className="animate-spin" />} Enroll Member
              </button>
            </div>
          </form>
        </div>
      </div>
    </div>
  );
}
