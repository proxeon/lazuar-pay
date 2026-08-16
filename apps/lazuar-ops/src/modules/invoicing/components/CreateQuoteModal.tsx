import { useState, useMemo } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2, X, Plus, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { client, type components } from "../../../lib/api-client";

type CustomLineItemDto = components["schemas"]["Commerce.CustomLineItemDto"];
type CreateCustomCheckoutRequestDto = components["schemas"]["Commerce.CreateCustomCheckoutRequestDto"];

interface CreateQuoteModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export default function CreateQuoteModal({ isOpen, onClose }: CreateQuoteModalProps) {
  const queryClient = useQueryClient();

  const [clientName, setClientName] = useState("");
  const [clientEmail, setClientEmail] = useState("");
  const [expiresAt, setExpiresAt] = useState("");
  const [isB2bRequired, setIsB2bRequired] = useState(false);
  const [lineItems, setLineItems] = useState<CustomLineItemDto[]>([
    { description: "", quantity: 1, unit_price: 0 }
  ]);

  const totalAmount = useMemo(() => {
    return lineItems.reduce((sum, item) => sum + (item.quantity * item.unit_price), 0);
  }, [lineItems]);

  const resetFormStates = () => {
    setClientName("");
    setClientEmail("");
    setExpiresAt("");
    setIsB2bRequired(false);
    setLineItems([{ description: "", quantity: 1, unit_price: 0 }]);
  };

  const createMutation = useMutation({
    mutationFn: async (payload: CreateCustomCheckoutRequestDto) => {
      const { error } = await client.POST("/admin/commerce/custom-checkouts", {
        body: payload
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("Custom quote created successfully");
      queryClient.invalidateQueries({ queryKey: ["custom-checkouts"] });
      resetFormStates();
      onClose();
    },
    onError: (err: any) => toast.error("Failed to create quote", { description: err.message })
  });

  const handleAddLineItem = () => {
    setLineItems(prev => [...prev, { description: "", quantity: 1, unit_price: 0 }]);
  };

  const handleRemoveLineItem = (index: number) => {
    if (lineItems.length <= 1) return;
    setLineItems(prev => prev.filter((_, i) => i !== index));
  };

  const handleLineItemChange = (index: number, field: keyof CustomLineItemDto, value: any) => {
    setLineItems(prev => prev.map((item, i) => i === index ? { ...item, [field]: value } : item));
  };

  const handleClose = () => {
    if (!createMutation.isPending) {
      resetFormStates();
      onClose();
    }
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();

    if (lineItems.some(item => !item.description.trim() || item.unit_price < 0 || item.quantity < 1)) {
      toast.error("Please ensure all line items have a valid description, price, and quantity.");
      return;
    }

    if (totalAmount <= 0) {
      toast.error("The total amount must be greater than RM 0.00.");
      return;
    }

    createMutation.mutate({
      client_name: clientName.trim(),
      client_email: clientEmail.trim().toLowerCase(),
      expires_at: expiresAt ? new Date(expiresAt).toISOString() : undefined,
      is_b2b_required: isB2bRequired,
      line_items: lineItems.map(li => ({
        description: li.description.trim(),
        quantity: Number(li.quantity),
        unit_price: Number(li.unit_price)
      }))
    });
  };

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/20 backdrop-blur-sm transition-opacity" onClick={handleClose} />
      <div className="relative bg-white border border-[#e5e5e5] shadow-2xl w-full max-w-2xl flex flex-col animate-in fade-in zoom-in-95 duration-200 max-h-[90vh]">
        <div className="flex items-center justify-between p-4 border-b border-[#e5e5e5] bg-[#fafafa]/50 shrink-0">
          <h3 className="text-[13px] font-bold uppercase tracking-widest text-[#09090b]">Create Proforma Quote</h3>
          <button onClick={handleClose} disabled={createMutation.isPending} className="text-[#a1a1aa] hover:bg-[#e5e5e5] hover:text-[#09090b] transition-colors p-1 disabled:opacity-50"><X size={16} /></button>
        </div>
        
        <div className="overflow-y-auto flex-1 bg-[#fafafa]/30">
          <form onSubmit={handleSubmit} className="flex flex-col min-h-[400px]">
            <div className="p-6 space-y-8 flex-1">
              
              <div className="space-y-4">
                <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">1. Client Details</label>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Client Name *</label>
                    <input required value={clientName} onChange={e => setClientName(e.target.value)} disabled={createMutation.isPending} placeholder="e.g. Acme Corp" className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
                  </div>
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Client Email *</label>
                    <input required type="email" value={clientEmail} onChange={e => setClientEmail(e.target.value)} disabled={createMutation.isPending} placeholder="billing@acme.com" className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
                  </div>
                </div>
              </div>

              <div className="space-y-4">
                <div className="flex items-center justify-between border-b border-[#f4f4f5] pb-1.5">
                  <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a]">2. Line Items</label>
                  <button type="button" onClick={handleAddLineItem} disabled={createMutation.isPending} className="text-[11px] font-bold uppercase tracking-widest text-[#09090b] hover:underline flex items-center gap-1">
                    <Plus size={12} /> Add Item
                  </button>
                </div>
                
                <div className="space-y-3">
                  {lineItems.map((item, idx) => (
                    <div key={idx} className="flex items-start gap-2 bg-white p-3 border border-[#e5e5e5] rounded-sm">
                      <div className="flex-1 grid grid-cols-12 gap-3">
                        <div className="col-span-12 sm:col-span-6 space-y-1.5">
                          <label className="text-[10px] uppercase tracking-wider text-[#71717a]">Description</label>
                          <input required value={item.description} onChange={e => handleLineItemChange(idx, "description", e.target.value)} disabled={createMutation.isPending} placeholder="e.g. Custom Web Design" className="flex h-8 w-full border border-[#e5e5e5] px-2 text-[12px] focus:outline-none focus:border-[#09090b]" />
                        </div>
                        <div className="col-span-6 sm:col-span-2 space-y-1.5">
                          <label className="text-[10px] uppercase tracking-wider text-[#71717a]">Qty</label>
                          <input required type="number" min="1" step="1" value={item.quantity} onChange={e => handleLineItemChange(idx, "quantity", Number(e.target.value))} disabled={createMutation.isPending} className="flex h-8 w-full border border-[#e5e5e5] px-2 text-[12px] focus:outline-none focus:border-[#09090b]" />
                        </div>
                        <div className="col-span-6 sm:col-span-4 space-y-1.5">
                          <label className="text-[10px] uppercase tracking-wider text-[#71717a]">Unit Price (MYR)</label>
                          <input required type="number" min="0" step="0.01" value={item.unit_price} onChange={e => handleLineItemChange(idx, "unit_price", Number(e.target.value))} disabled={createMutation.isPending} className="flex h-8 w-full border border-[#e5e5e5] px-2 text-[12px] focus:outline-none focus:border-[#09090b]" />
                        </div>
                      </div>
                      <button type="button" onClick={() => handleRemoveLineItem(idx)} disabled={createMutation.isPending || lineItems.length <= 1} className="p-1.5 mt-5 text-[#a1a1aa] hover:text-rose-600 transition-colors disabled:opacity-30">
                        <Trash2 size={14} />
                      </button>
                    </div>
                  ))}
                </div>
              </div>

              <div className="space-y-4">
                <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">3. Link Settings</label>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Expires At (Optional)</label>
                    <input type="datetime-local" value={expiresAt} onChange={e => setExpiresAt(e.target.value)} disabled={createMutation.isPending} className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] disabled:opacity-50" />
                  </div>
                  <div className="flex items-center pt-5">
                    <label className="flex items-center gap-2 cursor-pointer w-fit">
                      <input type="checkbox" checked={isB2bRequired} onChange={e => setIsB2bRequired(e.target.checked)} disabled={createMutation.isPending} className="rounded-sm border-[#e5e5e5] text-[#09090b] focus:ring-[#09090b]" />
                      <span className="text-[12px] font-medium text-[#09090b]">Require buyer tax ID (B2B tax invoice after payment)</span>
                    </label>
                  </div>
                </div>
              </div>

            </div>

            <div className="px-6 py-4 border-t border-[#e5e5e5] bg-[#fafafa]/80 flex items-center justify-between shrink-0">
              <div className="flex flex-col">
                <span className="text-[10px] font-bold uppercase tracking-widest text-[#71717a]">Total Amount</span>
                <span className="text-xl font-bold font-mono text-[#09090b]">RM {totalAmount.toFixed(2)}</span>
              </div>
              <div className="flex gap-2">
                <button type="button" onClick={handleClose} disabled={createMutation.isPending} className="h-10 px-4 rounded-sm border border-[#e5e5e5] bg-white text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:bg-[#f4f4f5] hover:text-[#09090b] transition-colors disabled:opacity-50">Cancel</button>
                <button type="submit" disabled={createMutation.isPending} className="h-10 px-6 rounded-sm bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest flex items-center justify-center gap-2 hover:bg-[#27272a] transition-colors disabled:opacity-50">
                  {createMutation.isPending && <Loader2 size={13} className="animate-spin" />} Generate Quote
                </button>
              </div>
            </div>
          </form>
        </div>
      </div>
    </div>
  );
}
