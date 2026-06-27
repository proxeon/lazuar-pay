import { useState } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Edit2, Loader2, Mail, Plus, BookOpen, X, Copy } from "lucide-react";
import { toast } from "sonner";
import { client, type components } from "../../../lib/api-client";
import PageLayout from "../../core/components/PageLayout";
import MessageTemplateEditor from "../components/MessageTemplateEditor";
import { cn } from "../../../lib/utils";

// Note: Using any to bypass legacy Community.MessageTemplateDto type mapping temporarily
// since it was moved to the Communications module in the backend.
type MessageTemplateDto = any; 

export default function TemplatesPage() {
  const queryClient = useQueryClient();
  const [selectedTemplate, setSelectedTemplate] = useState<MessageTemplateDto | null>(null);
  const [isWikiOpen, setIsWikiOpen] = useState(false);
  const [isCreateModalOpen, setIsCreateModalOpen] = useState(false);

  const [newName, setNewName] = useState("");
  const [newSubject, setNewSubject] = useState("");
  const [newEmailBody, setNewEmailBody] = useState("");
  const [newWhatsappBody, setNewWhatsappBody] = useState("");

  const { data: rawTemplates, isLoading } = useQuery<MessageTemplateDto[]>({
    queryKey: ["message-templates"],
    queryFn: async () => {
      // Stubbed catch since this endpoint was removed in Phase 1
      try {
        const { data, error } = await client.GET("/admin/community/templates");
        if (error) throw new Error(error.detail);
        return data || [];
      } catch {
        return [];
      }
    }
  });

  const { data: dictionaryGroups } = useQuery({
    queryKey: ["template-variables"],
    queryFn: async () => {
      // Stubbed catch since this endpoint was removed in Phase 1
      try {
        const { data, error } = await client.GET("/admin/community/templates/variables");
        if (error) throw new Error(error.detail);
        return data || [];
      } catch {
        return [];
      }
    },
    enabled: isWikiOpen
  });

  const templates = rawTemplates?.filter(t => t.channel === "EMAIL" || t.channel === "ALL") || [];

  const createMutation = useMutation({
    mutationFn: async () => {
      const { error } = await client.POST("/admin/community/templates", {
        body: {
          name: newName,
          subject: newSubject,
          email_body: newEmailBody,
          whatsapp_body: newWhatsappBody,
          channel: "ALL",
          required_variables: ["{{customer_name}}"],
          optional_variables: ["{{plan_name}}", "{{renewal_link}}"]
        }
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("Template created successfully");
      queryClient.invalidateQueries({ queryKey: ["message-templates"] });
      setIsCreateModalOpen(false);
      resetCreateForm();
    },
    onError: (err: any) => toast.error("Failed to create template", { description: err.message })
  });

  const updateMutation = useMutation({
    mutationFn: async ({ id, subject, email_body, whatsapp_body }: { id: string, subject: string, email_body: string, whatsapp_body: string }) => {
      const { error } = await client.PUT("/admin/community/templates/{id}", {
        params: { path: { id } },
        body: { subject, email_body, whatsapp_body }
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

  const resetCreateForm = () => {
    setNewName("");
    setNewSubject("");
    setNewEmailBody("");
    setNewWhatsappBody("");
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
          onSave={(subject, emailBody, whatsappBody) => updateMutation.mutate({ id: selectedTemplate.id, subject, email_body: emailBody, whatsapp_body: whatsappBody })}
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
      title="Communication Templates" 
      description="Manage the content and wording of your automated multi-channel notifications."
      breadcrumbs={[{ label: "Community", href: "/community/dashboard" }, { label: "Templates" }]}
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
          <h2 className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">Template Index</h2>
        </div>

        <div className="w-full overflow-x-auto min-h-[300px]">
          <table className="w-full text-left text-[13px] min-w-[750px]">
            <thead className="bg-[#fafafa] border-b border-[#e5e5e5]">
              <tr>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[30%]">Notification Template</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px] w-[20%]">Type</th>
                <th className="px-5 py-3 font-bold uppercase tracking-widest text-[#71717a] text-[9px]">Subject Line</th>
                <th className="px-5 py-3 w-[5%]"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#f4f4f5]">
              {isLoading ? (
                <tr><td colSpan={4} className="py-12 text-center"><Loader2 className="animate-spin text-[#a1a1aa] mx-auto" /></td></tr>
              ) : templates.length === 0 ? (
                <tr><td colSpan={4} className="py-12 text-center text-[#71717a] text-[12px]">No templates found. (Backend API retired during CaaS migration).</td></tr>
              ) : (
                templates.map((template) => (
                  <tr key={template.id} className="hover:bg-[#fafafa]/50 transition-colors group">
                    <td className="px-5 py-3.5 font-bold text-[#09090b]">
                      <div className="flex items-center gap-2">
                        <span>{template.name}</span>
                      </div>
                    </td>
                    <td className="px-5 py-3.5">
                      <span className={cn(
                        "text-[9px] px-1.5 py-0.5 border font-bold uppercase tracking-widest whitespace-nowrap",
                        template.is_default ? "bg-zinc-100 text-zinc-600 border-zinc-200" : "bg-amber-50 text-amber-700 border-amber-200"
                      )}>
                        {template.is_default ? "System Default" : "Custom"}
                      </span>
                    </td>
                    <td className="px-5 py-3.5 text-[#71717a] truncate max-w-sm">{template.subject}</td>
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
                <p className="text-[11px] text-[#71717a] mt-0.5">Click tags below to copy into your editor.</p>
              </div>
              <button onClick={() => setIsWikiOpen(false)} className="p-1 text-[#a1a1aa] hover:bg-[#e5e5e5] hover:text-[#09090b] transition-colors rounded-sm"><X size={16} /></button>
            </div>
            <div className="flex-1 overflow-y-auto p-6 space-y-6">
              {!dictionaryGroups || dictionaryGroups.length === 0 ? (
                <div className="text-center p-8 text-[#71717a] text-[12px]">No variables available.</div>
              ) : (
                dictionaryGroups.map((group: any) => (
                  <div key={group.title} className="space-y-3">
                    <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-1">{group.title}</h4>
                    <div className="space-y-2">
                      {group.items.map((item: any) => (
                        <div key={item.tag} className="flex flex-col gap-1 p-2 bg-[#fafafa] border border-[#e5e5e5]">
                          <div className="flex items-center justify-between">
                            <span className="font-mono text-[11px] font-bold text-[#09090b] bg-white border border-zinc-200 px-1.5 py-0.5">{item.tag}</span>
                            <button onClick={() => copyVariable(item.tag)} className="text-[#a1a1aa] hover:text-[#09090b] p-0.5 rounded-sm"><Copy size={12} /></button>
                          </div>
                          <p className="text-[11px] text-[#71717a]">{item.description}</p>
                        </div>
                      ))}
                    </div>
                  </div>
                ))
              )}
            </div>
          </div>
        </div>
      )}

      {isCreateModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/20 backdrop-blur-sm transition-opacity" onClick={() => !createMutation.isPending && setIsCreateModalOpen(false)} />
          <div className="relative bg-white border border-[#e5e5e5] shadow-xl w-full max-w-lg flex flex-col animate-in fade-in zoom-in-95 duration-200">
            <div className="flex items-center justify-between p-4 border-b border-[#e5e5e5] bg-[#fafafa]/50 shrink-0">
              <h3 className="text-[13px] font-bold uppercase tracking-widest text-[#09090b]">Create Template</h3>
              <button onClick={() => setIsCreateModalOpen(false)} disabled={createMutation.isPending} className="text-[#a1a1aa] hover:bg-[#e5e5e5] hover:text-[#09090b] p-1"><X size={16} /></button>
            </div>
            <form onSubmit={(e) => { e.preventDefault(); createMutation.mutate(); }}>
              <div className="p-5 space-y-4 max-h-[65vh] overflow-y-auto">
                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Template Title *</label>
                  <input required value={newName} onChange={e => setNewName(e.target.value)} disabled={createMutation.isPending} placeholder="e.g. Custom Event Receipt" className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b]" />
                </div>
                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Subject Line *</label>
                  <input required value={newSubject} onChange={e => setNewSubject(e.target.value)} disabled={createMutation.isPending} placeholder="e.g. Welcome onboard!" className="flex h-9 w-full rounded-sm border border-[#e5e5e5] bg-white px-3 py-1 text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b]" />
                </div>
                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Email Body *</label>
                  <textarea required value={newEmailBody} onChange={e => setNewEmailBody(e.target.value)} rows={4} disabled={createMutation.isPending} placeholder="Write HTML/Markdown..." className="w-full p-3 border border-[#e5e5e5] bg-white text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] resize-y font-mono" />
                </div>
                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">WhatsApp Body *</label>
                  <textarea required value={newWhatsappBody} onChange={e => setNewWhatsappBody(e.target.value)} rows={3} disabled={createMutation.isPending} placeholder="Write plain text..." className="w-full p-3 border border-[#e5e5e5] bg-white text-[13px] focus:outline-none focus:ring-1 focus:ring-[#09090b] resize-y font-sans" />
                </div>
              </div>
              <div className="px-5 py-3 border-t border-[#f4f4f5] bg-[#fafafa]/50 flex justify-end gap-2">
                <button type="button" onClick={() => setIsCreateModalOpen(false)} disabled={createMutation.isPending} className="h-8 px-4 rounded-sm border border-[#e5e5e5] bg-white text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:bg-[#f4f4f5] hover:text-[#09090b] transition-colors disabled:opacity-50">Cancel</button>
                <button type="submit" disabled={createMutation.isPending} className="h-8 px-6 rounded-sm bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest flex items-center justify-center gap-2 hover:bg-[#27272a] transition-colors disabled:opacity-50">
                  {createMutation.isPending && <Loader2 size={13} className="animate-spin" />} Save
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </PageLayout>
  );
}
