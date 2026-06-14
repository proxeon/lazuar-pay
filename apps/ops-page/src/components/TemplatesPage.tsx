import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Mail, Edit2, Loader2, ArrowRight } from "lucide-react";
import { toast } from "sonner";
import { client, type components } from "../lib/api-client";
import MessageTemplateEditor from "./MessageTemplateEditor";
import { cn } from "../lib/utils";

type MessageTemplateDto = components["schemas"]["Community.MessageTemplateDto"];

export default function TemplatesPage() {
  const queryClient = useQueryClient();
  const [selectedTemplate, setSelectedTemplate] = useState<MessageTemplateDto | null>(null);

  const { data: templates, isLoading } = useQuery({
    queryKey: ["message-templates"],
    queryFn: async () => {
      const { data, error } = await client.GET("/admin/community/templates");
      if (error) throw new Error(error.detail);
      return data;
    }
  });

  const updateMutation = useMutation({
    mutationFn: async ({ id, subject, body }: { id: string, subject: string, body: string }) => {
      const { error } = await client.PUT("/admin/community/templates/{id}", {
        params: { path: { id } },
        body: { subject, body }
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("Template saved successfully.");
      queryClient.invalidateQueries({ queryKey: ["message-templates"] });
      setSelectedTemplate(null);
    },
    onError: (err: any) => toast.error(err.message)
  });

  const resetMutation = useMutation({
    mutationFn: async (id: string) => {
      const { error } = await client.DELETE("/admin/community/templates/{id}", {
        params: { path: { id } }
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("Template reset to system defaults.");
      queryClient.invalidateQueries({ queryKey: ["message-templates"] });
      setSelectedTemplate(null);
    },
    onError: (err: any) => toast.error(err.message)
  });

  if (selectedTemplate) {
    return (
      <MessageTemplateEditor 
        template={selectedTemplate}
        onSave={(subject, body) => updateMutation.mutate({ id: selectedTemplate.id, subject, body })}
        onReset={() => resetMutation.mutate(selectedTemplate.id)}
        onCancel={() => setSelectedTemplate(null)}
        isSaving={updateMutation.isPending}
        isResetting={resetMutation.isPending}
      />
    );
  }

  return (
    <div className="flex-1 w-full p-6 md:p-12 overflow-y-auto bg-[#fafafa]">
      <div className="max-w-4xl mx-auto space-y-6">
        
        <div>
          <h1 className="text-xl font-bold text-[#09090b]">Message Templates</h1>
          <p className="text-xs text-[#71717a] mt-1">Customize automated emails and WhatsApp notifications.</p>
        </div>

        <div className="bg-white border border-[#e5e5e5] rounded-none overflow-hidden">
          <div className="px-5 py-4 border-b border-[#f4f4f5] bg-[#fafafa]/50 flex items-center gap-2">
            <Mail size={16} className="text-[#a1a1aa]" />
            <h2 className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">System Templates</h2>
          </div>

          <div className="divide-y divide-[#f4f4f5]">
            {isLoading ? (
              <div className="p-12 flex justify-center"><Loader2 className="animate-spin text-[#a1a1aa]" /></div>
            ) : (
              templates?.map((template) => (
                <div key={template.id} className="p-5 flex items-center justify-between hover:bg-[#fafafa]/50 transition-colors group">
                  <div className="flex-1 min-w-0 pr-4">
                    <div className="flex items-center gap-3 mb-1">
                      <h3 className="text-[13px] font-bold text-[#09090b] truncate">{template.name}</h3>
                      {!template.is_default && (
                        <span className="px-1.5 py-0.5 bg-amber-50 border border-amber-200 text-amber-700 text-[9px] font-bold uppercase tracking-widest">Customized</span>
                      )}
                    </div>
                    <p className="text-[12px] text-[#71717a] truncate font-mono">{template.subject}</p>
                  </div>
                  
                  <button 
                    onClick={() => setSelectedTemplate(template)}
                    className="flex items-center gap-2 px-3 py-1.5 bg-white border border-[#e5e5e5] text-[11px] font-bold uppercase tracking-widest text-[#09090b] hover:bg-[#f4f4f5] hover:border-[#09090b] transition-all shrink-0"
                  >
                    <Edit2 size={12} /> Edit
                  </button>
                </div>
              ))
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
