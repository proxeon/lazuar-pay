// apps/ops-page/src/modules/community/pages/TemplatesPage.tsx
import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Edit2, Loader2, Mail, Plus, BookOpen, X, Copy } from "lucide-react";
import { toast } from "sonner";
import { client, type components } from "../../../lib/api-client";
import PageLayout from "../../core/components/PageLayout";
import MessageTemplateEditor from "../components/MessageTemplateEditor";
import { cn } from "../../../lib/utils";

type MessageTemplateDto = components["schemas"]["Community.MessageTemplateDto"];

const DICTIONARY_GROUPS = [
  {
    title: "Customer Profile Context",
    items: [
      { tag: "{{customer_name}}", desc: "The full display name of the member." },
      { tag: "{{customer_email}}", desc: "The registered email address of the member." },
      { tag: "{{customer_phone}}", desc: "The phone number of the member." }
    ]
  },
  {
    title: "Billing & Subscriptions",
    items: [
      { tag: "{{plan_name}}", desc: "The subscription name (e.g. Premium Tier)." },
      { tag: "{{plan_price}}", desc: "The cost formatted in MYR." },
      { tag: "{{renewal_link}}", desc: "Direct, secure checkout billing link." },
      { tag: "{{total_price}}", desc: "Final charge total (factoring fees and tax overlays)." }
    ]
  },
  {
    title: "Community Assets",
    items: [
      { tag: "{{meeting_link}}", desc: "Zoom or private scheduling access links." },
      { tag: "{{group_link}}", desc: "Direct invitation link for Telegram or WhatsApp." }
    ]
  }
];

export default function TemplatesPage() {
  const queryClient = useQueryClient();
  const [selectedTemplate, setSelectedTemplate] = useState<MessageTemplateDto | null>(null);
  const [isWikiOpen, setIsWikiOpen] = useState(false);
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);

  const [newName, setNewName] = useState("");
  const [newSubject, setNewSubject] = useState("");
  const [newBody, setNewBody] = useState("");

  const { data: templates, isLoading } = useQuery<MessageTemplateDto[]>({
    queryKey: ["message-templates"],
    queryFn: async () => {
      const { data, error } = await client.GET("/admin/community/templates");
      if (error) throw new Error(error.detail);
      return data;
    }
  });

  const createMutation = useMutation({
    mutationFn: async () => {
      // Create a template with no routing logic attached
      const { error } = await client.POST("/admin/community/templates" as any, {
        body: {
          name: newName,
          subject: newSubject,
          body: newBody,
          channel: "EMAIL",
          required_variables: ["{{customer_name}}"],
          optional_variables: ["{{plan_name}}", "{{renewal_link}}"]
        }
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("Email template created successfully");
      queryClient.invalidateQueries({ queryKey: ["message-templates"] });
      setIsCreateModalOpen(false);
      resetCreateForm();
    },
    onError: (err: any) => toast.error("Failed to create template", { description: err.message })
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
      const { error } = await client.DELETE("/admin/community/templates/{id}");
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("Template reset to system defaults.");
      queryClient.invalidateQueries({ queryKey: ["message-templates"] });
      setSelectedTemplate(null);
    },
    onError: (err: any) => toast.error(err.message)
  });

  const resetCreateForm = () => {
    setNewName("");
    setNewSubject("");
    setNewBody("");
  };

  const copyVariable = (tag: string) => {
    navigator.clipboard.writeText(tag);
    toast.success(`Copied "${tag}" to clipboard`);
  };

  if (selectedTemplate) {
    return (
      <div className="fixed inset-0 z-50 bg-white flex flex-col animate-in fade-in zoom-in-95 duration-200">
        <MessageTemplateEditor 
          template={selectedTemplate}
          onSave={(subject, body) => updateMutation.mutate({ id: selectedTemplate.id, subject, body })}
          onReset={() => resetMutation.mutate(selectedTemplate.id)}
          onCancel={() => setSelectedTemplate(null)}
          isSaving={updateMutation.isPending}
          isResetting={resetMutation.isPending}
        />
      </div>
    );
  }

  return (
    <PageLayout 
      title="Email Templates" 
      description="Configure automated notification schedules and email templates."
      breadcrumbs={[{ label: "Community", href: "/community/dashboard" }, { label: "Email Templates" }]}
      actionButton={
        <div className="flex gap-2">
          <button onClick={() => setIsWikiOpen(true)} className="h-9 px-4 bg-white border border-[#e5e5e5] text-[#09090b] text-[11px] font-bold uppercase tracking-widest flex items-center gap-2 hover:bg-[#fafafa] transition-colors"><BookOpen size={13} /> Variable Wiki</button>
          <button onClick={() => setIsCreateModalOpen(true)} className="h-9 px-4 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest flex items-center gap-2 hover:bg-[#27272a] transition-colors"><Plus size={14} /> Create Template</button>
        </div>
      }
    >
      <div className="bg-white border border-[#e5e5e5] rounded-none overflow-hidden">
        <div className="px-5 py-4 border-b border-[#f4f4f5] bg-[#fafafa]/50 flex items-center gap-2">
          <Mail size={15} className="text-[#a1a1aa]" />
          <h2 className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">Email Notifications Index</h2>
        </div>

        <div className="w-full overflow-x-auto">
          <table className="w-full text-left text-[13px] min-w-[750px]">
            <thead className="bg-[#fafafa] border-b border-[#e5e5e5]">
              <tr>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Notification Template</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Subject Line</th>
                <th className="px-5 py-3 w-[5%]"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#f4f4f5]">
              {isLoading ? (
                <tr><td colSpan={3} className="py-12 text-center"><Loader2 className="animate-spin text-[#a1a1aa] mx-auto" /></td></tr>
              ) : (
                templates?.map((template) => (
                  <tr key={template.id} className="hover:bg-[#fafafa]/50 transition-colors group">
                    <td className="px-5 py-3.5 font-bold text-[#09090b]">
                      <div className="flex items-center gap-2">
                        <span>{template.name}</span>
                        {!template.is_default && (
                          <span className="px-1.5 py-0.5 bg-amber-50 border border-amber-200 text-amber-700 text-[8px] font-bold uppercase tracking-wider">Customized</span>
                        )}
                      </div>
                    </td>
                    <td className="px-5 py-3.5 text-[#71717a] truncate max-w-xs">{template.subject}</td>
                    <td className="px-5 py-3.5 text-right">
                      <button onClick={() => setSelectedTemplate(template)} className="flex items-center gap-1.5 px-2.5 py-1 bg-white border border-[#e5e5e5] text-[10px] font-bold uppercase tracking-widest text-[#09090b] hover:bg-[#f4f4f5] transition-all">
                        <Edit2 size={10} /> Edit
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {isWikiOpen && (
        <div className="fixed inset-0 z-50 flex justify-end">
          <div className="absolute inset-0 bg-black/20 backdrop-blur-sm transition-opacity" onClick={() => setIsWikiOpen(false)} />
          <div className="relative w-full max-w-md bg-white border-l border-[#e5e5e5] h-full shadow-2xl flex flex-col animate-in slide-in-from-right duration-300">
            <div className="flex items-center justify-between p-5 border-b border-[#e5e5e5] bg-[#fafafa] shrink-0">
              <div>
                <h3 className="text-[14px] font-bold uppercase tracking-widest text-[#09090b]">Variable Directory</h3>
                <p className="text-[11px] text-[#71717a] mt-0.5">Click tags below to copy into your email editor.</p>
              </div>
              <button onClick={() => setIsWikiOpen(false)} className="p-1 text-[#a1a1aa] hover:bg-[#e5e5e5] hover:text-[#09090b] transition-colors rounded-sm"><X size={16} /></button>
            </div>
            <div className="flex-1 overflow-y-auto p-6 space-y-6">
              {DICTIONARY_GROUPS.map((group) => (
                <div key={group.title} className="space-y-3">
                  <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">{group.title}</h4>
                  <div className="space-y-2">
                    {group.items.map((item) => (
                      <div key={item.tag} className="flex flex-col gap-1 p-2 bg-[#fafafa] border border-[#e5e5e5]">
                        <div className="flex items-center justify-between">
                          <span className="font-mono text-[11px] font-bold text-[#09090b] bg-white border border-zinc-200 px-1.5 py-0.5">{item.tag}</span>
                          <button onClick={() => copyVariable(item.tag)} className="text-[#a1a1aa] hover:text-[#09090b] p-0.5 rounded-sm"><Copy size={12} /></button>
                        </div>
                        <p className="text-[11px] text-[#71717a]">{item.desc}</p>
                      </div>
                    ))}
                  </div>
                </div>
              ))}
            </div>
          </div>
        </div>
      )}

      {isCreateModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/20 backdrop-blur-sm transition-opacity" onClick={() => !createMutation.isPending && setIsCreateModalOpen(false)} />
          <div className="relative bg-white border border-[#e5e5e5] shadow-xl w-full max-w-lg flex flex-col animate-in fade-in zoom-in-95 duration-200">
            <div className="flex items-center justify-between p-4 border-b border-[#e5e5e5] bg-[#fafafa]/50 shrink-0">
              <h3 className="text-[13px] font-bold uppercase tracking-widest text-[#09090b]">Create Email Template</h3>
              <button onClick={() => setIsCreateModalOpen(false)} disabled={createMutation.isPending} className="text-[#a1a1aa] hover:bg-[#e5e5e5] hover:text-[#09090b] p-1"><X size={16} /></button>
            </div>
            <form onSubmit={(e) => { e.preventDefault(); createMutation.mutate(); }}>
              <div className="p-5 space-y-4">
                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Template Title *</label>
                  <input required value={newName} onChange={e => setNewName(e.target.value)} disabled={createMutation.isPending} placeholder="e.g. Signup Receipt" className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b]" />
                </div>
                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Subject Line *</label>
                  <input required value={newSubject} onChange={e => setNewSubject(e.target.value)} disabled={createMutation.isPending} placeholder="e.g. Welcome onboard {{customer_name}}!" className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b]" />
                </div>
                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Message Body *</label>
                  <textarea required value={newBody} onChange={e => setNewBody(e.target.value)} rows={6} disabled={createMutation.isPending} placeholder="Write copy... (Markdown supported)" className="w-full p-3 border border-[#e5e5e5] bg-white text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] resize-y font-mono" />
                </div>
              </div>
              <div className="px-5 py-3 border-t border-[#f4f4f5] bg-[#fafafa]/50 flex justify-end gap-2">
                <button type="button" onClick={() => setIsCreateModalOpen(false)} disabled={createMutation.isPending} className="h-8 px-4 rounded-sm border border-[#e5e5e5] bg-white text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:bg-[#f4f4f5] hover:text-[#09090b] transition-colors disabled:opacity-50">Cancel</button>
                <button type="submit" disabled={createMutation.isPending} className="h-8 px-6 rounded-sm bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest flex items-center justify-center gap-2 hover:bg-[#27272a] transition-colors disabled:opacity-50">
                  {createMutation.isPending && <Loader2 size={13} className="animate-spin" />} Create Template
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </PageLayout>
  );
}
