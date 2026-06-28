import { useMutation, useQueryClient } from "@tanstack/react-query";
import { X } from "lucide-react";
import { toast } from "sonner";
import { client, type components } from "../../../lib/api-client";
import ProductForm from "./ProductForm";

type CreateProductRequestDto = components["schemas"]["Commerce.CreateProductRequestDto"];

interface CreateProductModalProps {
  isOpen: boolean;
  onClose: () => void;
}

export default function CreateProductModal({ isOpen, onClose }: CreateProductModalProps) {
  const queryClient = useQueryClient();

  const createMutation = useMutation({
    mutationFn: async (payload: CreateProductRequestDto) => {
      const { data, error } = await client.POST("/admin/commerce/products", { body: payload });
      if (error || !data) throw new Error(error?.detail || "Failed to create link");
    },
    onSuccess: () => {
      toast.success("Checkout link created successfully");
      queryClient.invalidateQueries({ queryKey: ["commerce-products"] });
      onClose();
    },
    onError: (err: any) => {
      toast.error("Failed to create link", { description: err.message });
    }
  });

  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-[60] flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/20 backdrop-blur-sm transition-opacity" onClick={() => !createMutation.isPending && onClose()} />
      <div className="relative bg-white border border-[#e5e5e5] shadow-2xl w-full max-w-2xl flex flex-col animate-in fade-in zoom-in-95 duration-200 max-h-[90vh]">
        <div className="flex items-center justify-between p-4 border-b border-[#e5e5e5] bg-[#fafafa] shrink-0">
          <h3 className="text-[13px] font-bold uppercase tracking-widest text-[#09090b]">Create Checkout Link</h3>
          <button onClick={() => !createMutation.isPending && onClose()} disabled={createMutation.isPending} className="text-[#a1a1aa] hover:bg-[#e5e5e5] hover:text-[#09090b] transition-colors p-1 disabled:opacity-50">
            <X size={16} />
          </button>
        </div>
        
        <div className="flex-1 flex flex-col overflow-hidden bg-white min-h-0">
          <ProductForm 
            onSubmit={(data: CreateProductRequestDto) => createMutation.mutate(data)} 
            onCancel={onClose} 
            isPending={createMutation.isPending}
            submitLabel="Create Link"
          />
        </div>
      </div>
    </div>
  );
}
