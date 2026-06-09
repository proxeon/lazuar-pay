import { useState, useMemo, useEffect } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { client } from "../lib/api-client";
import type { MessageTemplate, ReminderSchedule } from "../lib/api-client";
import { Menu, Mail, ChevronDown, ChevronUp, Loader2, RotateCcw, Send, Plus, Trash2 } from "lucide-react";
import { toast } from "sonner";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Badge } from "@/components/ui/badge";

const COMMUNITY_TEMPLATES = [
  "Community Welcome",
  "Community Payment Success",
  "Community Payment Failed",
  "Community Renewal (3 Days)",
  "Community Renewal Due Today",
  "Community Renewal Overdue",
  "Community Subscription Cancelled",
  "Abandoned Cart (12h)",
  "Abandoned Cart (24h)",
];

export default function Communications({ isMobile, toggleSidebar }: any) {
  const queryClient = useQueryClient();
  const [activeTab, setActiveTab] = useState<"TEMPLATES" | "SCHEDULE">("TEMPLATES");
  const [editingSchedule, setEditingSchedule] = useState<ReminderSchedule | Partial<ReminderSchedule> | null>(null);

  const { data: templates, isLoading: isLoadingTemplates } = useQuery<MessageTemplate[]>({
    queryKey: ["messaging-templates"],
    queryFn: async () => {
        const { data, error } = await client.GET("/admin/community/templates");
        if (error) throw new Error(error.detail || "Failed to fetch templates");
        return data ?? [];
    },
  });

  const { data: schedules, isLoading: isLoadingSchedules } = useQuery<ReminderSchedule[]>({
    queryKey: ["reminder-schedule"],
    queryFn: async () => {
        const { data, error } = await client.GET("/admin/community/reminder-schedules");
        if (error) throw new Error(error.detail || "Failed to fetch reminder schedule");
        return data ?? [];
    },
  });

  const sortedTemplates = useMemo(() => {
    const filtered = templates?.filter((t) => COMMUNITY_TEMPLATES.includes(t.name)) || [];
    return filtered.sort((a, b) => COMMUNITY_TEMPLATES.indexOf(a.name) - COMMUNITY_TEMPLATES.indexOf(b.name));
  }, [templates]);

  const createScheduleMutation = useMutation({
    mutationFn: async (payload: any) => {
        const { data, error } = await client.POST("/admin/community/reminder-schedules", { body: payload });
        if (error) throw new Error(error.detail || "Failed to create schedule");
        return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["reminder-schedule"] });
      toast.success("Schedule created successfully.");
      setEditingSchedule(null);
    },
    onError: (err: any) => toast.error(err.message),
  });

  const updateScheduleMutation = useMutation({
    mutationFn: async ({ id, data: payload }: { id: string; data: any }) => {
        const { data, error } = await client.PUT("/admin/community/reminder-schedules/{id}", { params: { path: { id } }, body: payload });
        if (error) throw new Error(error.detail || "Failed to update schedule");
        return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["reminder-schedule"] });
      toast.success("Schedule updated.");
      setEditingSchedule(null);
    },
    onError: (err: any) => toast.error(err.message),
  });

  const deleteScheduleMutation = useMutation({
    mutationFn: async (id: string) => {
        const { data, error } = await client.DELETE("/admin/community/reminder-schedules/{id}", { params: { path: { id } } });
        if (error) throw new Error(error.detail || "Failed to delete schedule");
        return data;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["reminder-schedule"] });
      toast.success("Schedule deleted.");
    },
    onError: (err: any) => toast.error(err.message),
  });

  const toggleScheduleMutation = useMutation({
    mutationFn: async ({ id, is_enabled }: { id: string; is_enabled: boolean }) => {
        const { data, error } = await client.PUT("/admin/community/reminder-schedules/{id}", { params: { path: { id } }, body: { is_enabled } });
        if (error) throw new Error(error.detail || "Failed to toggle schedule");
        return data;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["reminder-schedule"] }),
    onError: (err: any) => toast.error(err.message),
  });

  if (isLoadingTemplates || isLoadingSchedules) {
    return (
      <div className="flex-1 w-full p-4 md:p-8 mx-auto max-w-[900px]">
        <p className="text-sm font-medium uppercase tracking-widest text-muted-foreground flex items-center gap-2"><Loader2 size={14} className="animate-spin" /> Loading...</p>
      </div>
    );
  }

  return (
    <div className="flex-1 w-full p-4 md:p-8 mx-auto max-w-[900px] flex flex-col gap-6">
      <header className="flex items-center gap-3 pb-2">
        {isMobile && <button onClick={toggleSidebar} className="p-1.5 hover:bg-secondary rounded-none transition-colors"><Menu size={20} /></button>}
        <div>
          <h1 className="text-[20px] font-semibold tracking-tight text-foreground flex items-center gap-2"><Mail size={20} /> Communications</h1>
          <p className="text-[11px] font-bold uppercase tracking-[0.2em] text-muted-foreground mt-1">Manage automated emails, WhatsApp templates, and reminder schedules.</p>
        </div>
      </header>

      <div className="flex border-b border-border/60 gap-2">
        <button onClick={() => setActiveTab("TEMPLATES")} className={`pb-2.5 px-3 text-xs font-bold uppercase tracking-widest border-b-2 transition-all duration-150 flex items-center gap-2 focus:outline-none ${activeTab === "TEMPLATES" ? "border-foreground text-foreground" : "border-transparent text-muted-foreground hover:text-foreground"}`}>Message Templates</button>
        <button onClick={() => setActiveTab("SCHEDULE")} className={`pb-2.5 px-3 text-xs font-bold uppercase tracking-widest border-b-2 transition-all duration-150 flex items-center gap-2 focus:outline-none ${activeTab === "SCHEDULE" ? "border-foreground text-foreground" : "border-transparent text-muted-foreground hover:text-foreground"}`}>Reminder Schedule</button>
      </div>

      {activeTab === "TEMPLATES" && <TemplatesList sortedTemplates={sortedTemplates} />}

      {activeTab === "SCHEDULE" && (
        <div className="space-y-4">
          <div className="flex justify-end">
             <button onClick={() => setEditingSchedule({ days_relative_to_due: 0, time_of_day: "09:00", channel: "BEST", is_enabled: true })} className="inline-flex items-center h-10 px-4 bg-foreground text-background text-sm font-bold tracking-wide uppercase rounded-none hover:bg-foreground/90 transition-colors"><Plus className="w-4 h-4 mr-2" /> Add Reminder</button>
          </div>

          <div className="bg-card border border-border/60 rounded-none shadow-sm overflow-hidden">
            <Table>
              <TableHeader className="bg-secondary/50">
                <TableRow>
                  <TableHead className="font-bold text-[10px] uppercase tracking-widest text-muted-foreground">Timing</TableHead>
                  <TableHead className="font-bold text-[10px] uppercase tracking-widest text-muted-foreground">Channel</TableHead>
                  <TableHead className="font-bold text-[10px] uppercase tracking-widest text-muted-foreground">Template</TableHead>
                  <TableHead className="w-[100px] font-bold text-[10px] uppercase tracking-widest text-muted-foreground text-center">Status</TableHead>
                  <TableHead className="w-[100px] text-right font-bold text-[10px] uppercase tracking-widest text-muted-foreground">Actions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {schedules?.length === 0 ? (
                  <TableRow><TableCell colSpan={5} className="text-center py-8 text-[11px] font-bold uppercase tracking-widest text-muted-foreground">No schedules active.</TableCell></TableRow>
                ) : (
                  schedules?.map((sch) => {
                    const daysStr = sch.days_relative_to_due < 0 ? `${Math.abs(sch.days_relative_to_due)} days before` : sch.days_relative_to_due > 0 ? `${sch.days_relative_to_due} days after` : "On due date";
                    return (
                      <TableRow key={sch.id} className="hover:bg-secondary/40">
                        <TableCell className="align-middle">
                          <div className="font-semibold text-xs text-foreground">{daysStr}</div>
                          <div className="text-[10px] font-mono text-muted-foreground mt-0.5">at {sch.time_of_day} UTC</div>
                        </TableCell>
                        <TableCell className="align-middle"><Badge variant="outline" className="text-[9px] uppercase tracking-widest rounded-none border px-1.5 py-0">{sch.channel === "BEST" ? "Auto (Best)" : sch.channel}</Badge></TableCell>
                        <TableCell className="align-middle">
                          <div className="text-xs font-medium text-foreground">{sch.template_name}</div>
                          {sch.plan_name && <div className="text-[10px] text-muted-foreground mt-0.5">Plan: {sch.plan_name}</div>}
                        </TableCell>
                        <TableCell className="align-middle text-center">
                           <input type="checkbox" checked={sch.is_enabled} onChange={(e) => toggleScheduleMutation.mutate({ id: sch.id, is_enabled: e.target.checked })} className="h-4 w-4 rounded-none border-border/60 focus:ring-foreground accent-foreground" />
                        </TableCell>
                        <TableCell className="text-right align-middle">
                          <div className="flex justify-end gap-1">
                            <button onClick={() => setEditingSchedule(sch)} className="text-[10px] font-bold uppercase tracking-widest text-muted-foreground hover:text-foreground px-2 py-1">Edit</button>
                            <button onClick={() => { if(window.confirm("Delete this reminder schedule?")) deleteScheduleMutation.mutate(sch.id); }} className="text-muted-foreground hover:text-red-500 p-1"><Trash2 size={14}/></button>
                          </div>
                        </TableCell>
                      </TableRow>
                    );
                  })
                )}
              </TableBody>
            </Table>
          </div>
        </div>
      )}

      {editingSchedule && (
        <ScheduleEditorModal 
          schedule={editingSchedule} templates={sortedTemplates} onClose={() => setEditingSchedule(null)}
          onSave={(data) => { if (editingSchedule.id) updateScheduleMutation.mutate({ id: editingSchedule.id, data }); else createScheduleMutation.mutate(data); }}
          isSaving={updateScheduleMutation.isPending || createScheduleMutation.isPending}
        />
      )}
    </div>
  );
}

function TemplatesList({ sortedTemplates }: { sortedTemplates: MessageTemplate[] }) {
  const [expandedId, setExpandedId] = useState<string | null>(null);
  const queryClient = useQueryClient();

  const updateMutation = useMutation({
    mutationFn: async ({ id, data: payload }: { id: string; data: { subject: string; body: string } }) => {
        const { data, error } = await client.PUT("/admin/community/templates/{id}", { params: { path: { id } }, body: payload });
        if (error) throw new Error(error.detail || "Failed to update template");
        return data;
    },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ["messaging-templates"] }); toast.success("Template saved successfully."); setExpandedId(null); },
    onError: (err: any) => toast.error(err.message),
  });

  const deleteMutation = useMutation({
    mutationFn: async (id: string) => {
        const { data, error } = await client.DELETE("/admin/community/templates/{id}", { params: { path: { id } } });
        if (error) throw new Error(error.detail || "Failed to reset template");
        return data;
    },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ["messaging-templates"] }); toast.success("Template reset to default."); setExpandedId(null); },
    onError: (err: any) => toast.error(err.message),
  });

  const testMutation = useMutation({
    mutationFn: async (templateName: string) => {
        const { data, error } = await client.POST("/admin/community/reminders/test", { body: { template_name: templateName, channel: "EMAIL" } });
        if (error) throw new Error(error.detail || "Failed to send test reminder");
        return data;
    },
    onSuccess: (data) => toast.success(`Test sent successfully to ${data.sent_to}`),
    onError: (err: any) => toast.error(err.message),
  });

  return (
    <div className="space-y-4 pb-12">
      {sortedTemplates.map((template) => {
        const isExpanded = expandedId === template.id;
        return (
          <div key={template.id} className="bg-card border border-border/60 rounded-none shadow-sm overflow-hidden transition-colors">
            <button onClick={() => setExpandedId(isExpanded ? null : template.id)} className="w-full flex items-center justify-between p-4 bg-secondary/30 hover:bg-secondary/60 transition-colors focus:outline-none">
              <div className="flex flex-col items-start gap-1.5">
                <div className="flex items-center gap-2">
                  <span className="text-sm font-bold text-foreground">{template.name}</span>
                  {/* NEW: Explicitly show the Channel configuration from the DTO */}
                  <Badge variant="outline" className="text-[9px] uppercase tracking-widest px-1.5 py-0 border-border/60 bg-background">{template.channel}</Badge>
                </div>
                <span className="text-[10px] uppercase tracking-widest text-muted-foreground font-mono">Subject: {template.subject || "(No Subject)"}</span>
              </div>
              <div className="text-muted-foreground p-1 border border-transparent hover:border-border/60 hover:bg-background transition-all">{isExpanded ? <ChevronUp size={16} /> : <ChevronDown size={16} />}</div>
            </button>
            {isExpanded && (
              <div className="p-6 border-t border-border/60 bg-card">
                <TemplateEditor template={template} onSave={(data) => updateMutation.mutate({ id: template.id, data })} isSaving={updateMutation.isPending} onCancel={() => setExpandedId(null)} onReset={() => { if (window.confirm("Are you sure you want to reset this template to its default?")) deleteMutation.mutate(template.id); }} isResetting={deleteMutation.isPending} onTest={() => testMutation.mutate(template.name)} isTesting={testMutation.isPending} />
              </div>
            )}
          </div>
        );
      })}
    </div>
  );
}

interface TemplateEditorProps {
  template: MessageTemplate;
  onSave: (data: { subject: string; body: string }) => void;
  isSaving: boolean;
  onCancel: () => void;
  onReset: () => void;
  isResetting: boolean;
  onTest: () => void;
  isTesting: boolean;
}

function TemplateEditor({ template, onSave, isSaving, onCancel, onReset, isResetting, onTest, isTesting }: TemplateEditorProps) {
  const [subject, setSubject] = useState(template.subject);
  const [body, setBody] = useState(template.body);

  const requiredVars = template.required_variables || [];
  const optionalVars = template.optional_variables || [];

  useEffect(() => { setSubject(template.subject); setBody(template.body); }, [template]);

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    for (const reqVar of requiredVars) {
      if (!body.includes(reqVar)) {
        toast.error(`Error: You must include ${reqVar} in the message body for the system to function.`);
        return; 
      }
    }
    onSave({ subject, body });
  };

  return (
    <form onSubmit={handleSubmit} className="space-y-5">
      <div className="bg-secondary/30 p-4 border border-border/60 mb-2">
        <h4 className="text-[10px] font-bold uppercase tracking-widest text-muted-foreground mb-3 flex items-center justify-between">
          <span>Available Variables</span>
          {requiredVars.length > 0 && (
            <span className="text-[9px] text-muted-foreground/70 normal-case tracking-normal font-normal">
              * Indicates a required system variable
            </span>
          )}
        </h4>
        <div className="flex flex-wrap gap-2">
          {requiredVars.map(v => (
            <span key={v} className="text-[11px] font-mono font-bold text-red-600 dark:text-red-400 bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-900 px-1.5 py-0.5" title="Required Variable">
              {v} *
            </span>
          ))}
          {optionalVars.map(v => (
            <span key={v} className="text-[11px] font-mono text-foreground bg-background border border-border/60 px-1.5 py-0.5">
              {v}
            </span>
          ))}
          {requiredVars.length === 0 && optionalVars.length === 0 && (
             <span className="text-[11px] font-mono text-muted-foreground italic">No variables available for this template.</span>
          )}
        </div>
      </div>
      <div className="space-y-2">
        <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Subject Line</label>
        <input type="text" required value={subject} onChange={(e) => setSubject(e.target.value)} className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring font-mono" />
      </div>
      <div className="space-y-2">
        <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Message Body</label>
        <textarea required value={body} onChange={(e) => setBody(e.target.value)} rows={12} className="flex w-full rounded-none border border-border/60 bg-background px-3 py-3 text-[13px] shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring font-mono resize-y leading-relaxed" />
        <p className="text-[11px] text-muted-foreground mt-2">Note: This body text is shared between Email and WhatsApp.</p>
      </div>
      <div className="flex flex-wrap items-center justify-between pt-4 gap-4">
        <div className="flex items-center gap-2"><button type="button" onClick={onCancel} className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground hover:text-foreground transition-colors px-2 py-1">Cancel</button></div>
        <div className="flex items-center flex-wrap gap-3">
          <button type="button" onClick={onTest} disabled={isTesting || isSaving} className="h-10 px-4 flex items-center gap-2 border border-border/60 hover:border-foreground/40 bg-background text-[11px] font-bold tracking-widest uppercase text-foreground rounded-none transition-colors disabled:opacity-50">{isTesting ? <Loader2 size={14} className="animate-spin" /> : <Send size={14} />}<span className="hidden sm:inline">Send Test</span></button>
          <button type="button" onClick={onReset} disabled={isResetting || isSaving} className="h-10 px-4 flex items-center gap-2 border border-transparent hover:border-red-200 dark:hover:border-red-900 bg-transparent hover:bg-red-50 dark:hover:bg-red-950/30 text-[11px] font-bold tracking-widest uppercase text-red-600 dark:text-red-500 rounded-none transition-colors disabled:opacity-50">{isResetting ? <Loader2 size={14} className="animate-spin" /> : <RotateCcw size={14} />}<span className="hidden sm:inline">Reset to Default</span></button>
          <button type="submit" disabled={isSaving || (!subject && !body)} className="h-10 px-6 bg-foreground text-background text-xs font-bold tracking-wide uppercase rounded-none hover:bg-foreground/90 disabled:opacity-50 transition-colors shadow-[4px_4px_0px_0px_rgba(0,0,0,0.1)] hover:shadow-none hover:translate-y-[2px] hover:translate-x-[2px] active:scale-95 flex items-center gap-2">{isSaving && <Loader2 size={14} className="animate-spin" />}{isSaving ? "Saving..." : "Save Changes"}</button>
        </div>
      </div>
    </form>
  );
}

function ScheduleEditorModal({ schedule, templates, onClose, onSave, isSaving }: { 
  schedule: Partial<ReminderSchedule>; 
  templates: MessageTemplate[];
  onClose: () => void; 
  onSave: (data: any) => void;
  isSaving: boolean;
}) {
  const [days, setDays] = useState(schedule.days_relative_to_due || 0);
  const [time, setTime] = useState(schedule.time_of_day || "09:00");
  const [channel, setChannel] = useState(schedule.channel || "BEST");
  const [templateId, setTemplateId] = useState(schedule.template_id || "");

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    onSave({ days_relative_to_due: Number(days), time_of_day: time, channel, template_id: templateId, is_enabled: schedule.is_enabled ?? true });
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/40 backdrop-blur-sm" onClick={onClose} />
      <div className="relative bg-card border border-border/60 rounded-none shadow-[8px_8px_0px_0px_rgba(0,0,0,0.1)] w-full max-w-sm overflow-hidden flex flex-col animate-in fade-in zoom-in-95 duration-200">
        <div className="flex items-center justify-between p-5 border-b border-border/60">
          <div><h3 className="text-sm font-bold uppercase tracking-widest text-foreground">{schedule.id ? "Edit Reminder" : "New Reminder"}</h3></div>
        </div>
        <form onSubmit={handleSubmit} className="p-5 space-y-4">
          <div className="space-y-1.5"><label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Days Relative to Due Date</label><input type="number" required value={days} onChange={e => setDays(Number(e.target.value))} className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm" /><p className="text-[10px] text-muted-foreground">e.g. -3 for 3 days before, 0 for on due date, 3 for 3 days after.</p></div>
          <div className="space-y-1.5"><label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Time of Day (UTC)</label><input type="time" required value={time} onChange={e => setTime(e.target.value)} className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm" /></div>
          <div className="space-y-1.5"><label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Delivery Channel</label><select required value={channel} onChange={e => setChannel(e.target.value)} className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm"><option value="BEST">Auto (Best Available)</option><option value="EMAIL">Email Only</option><option value="WHATSAPP">WhatsApp Only</option></select></div>
          <div className="space-y-1.5"><label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Message Template</label><select required value={templateId} onChange={e => setTemplateId(e.target.value)} className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm"><option value="" disabled>Select a template...</option>{templates.map(t => <option key={t.id} value={t.id}>{t.name}</option>)}</select></div>
          <div className="flex items-center justify-between pt-4"><button type="button" onClick={onClose} className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground hover:text-foreground transition-colors px-2 py-1">Cancel</button><button type="submit" disabled={isSaving || !templateId} className="h-10 px-6 bg-foreground text-background text-xs font-bold tracking-wide uppercase rounded-none hover:bg-foreground/90 disabled:opacity-50 transition-colors shadow-[4px_4px_0px_0px_rgba(0,0,0,0.1)] hover:shadow-none hover:translate-y-[2px] hover:translate-x-[2px] active:scale-95 flex items-center gap-2">{isSaving && <Loader2 size={14} className="animate-spin" />} Save</button></div>
        </form>
      </div>
    </div>
  );
}
