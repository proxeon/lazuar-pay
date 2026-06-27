import { useState, useRef, useEffect } from "react";
import { ArrowLeft, Loader2, Save, RotateCcw, Mail, Smartphone } from "lucide-react";
import { toast } from "sonner";
import { client, type components } from "../../../lib/api-client";
import { useDebounce } from "../../../hooks/use-debounce";
import { cn } from "../../../lib/utils";

type MessageTemplateDto = components["schemas"]["Communications.MessageTemplateDto"];

interface MessageTemplateEditorProps {
  template: MessageTemplateDto;
  onSave: (subject: string, emailBody: string, whatsappBody: string) => void;
  onReset: () => void;
  onCancel: () => void;
  isSaving: boolean;
  isResetting: boolean;
}

export default function MessageTemplateEditor({ 
  template, onSave, onReset, onCancel, isSaving, isResetting 
}: MessageTemplateEditorProps) {
  const [activeTab, setActiveTab] = useState<"EMAIL" | "WHATSAPP">("EMAIL");

  const [subject, setSubject] = useState(template.subject);
  const [emailBody, setEmailBody] = useState(template.email_body);
  const [whatsappBody, setWhatsappBody] = useState(template.whatsapp_body);
  
  const [previewEmailHtml, setPreviewEmailHtml] = useState("");
  const [previewWhatsappText, setPreviewWhatsappText] = useState("");
  const [previewSubject, setPreviewSubject] = useState("");
  const [isPreviewLoading, setIsPreviewLoading] = useState(false);

  const debouncedSubject = useDebounce(subject, 500);
  const debouncedEmailBody = useDebounce(emailBody, 500);
  const debouncedWhatsappBody = useDebounce(whatsappBody, 500);

  const emailTextareaRef = useRef<HTMLTextAreaElement>(null);
  const whatsappTextareaRef = useRef<HTMLTextAreaElement>(null);
  const subjectRef = useRef<HTMLInputElement>(null);
  const [lastFocused, setLastFocused] = useState<"subject" | "emailBody" | "whatsappBody">("emailBody");

  useEffect(() => {
    async function fetchPreview() {
      setIsPreviewLoading(true);
      try {
        const { data, error } = await client.POST("/admin/communications/templates/preview", {
          body: { 
            subject: debouncedSubject, 
            email_body: debouncedEmailBody, 
            whatsapp_body: debouncedWhatsappBody 
          }
        });
        if (data && !error) {
          setPreviewEmailHtml(data.html_email_preview);
          setPreviewWhatsappText(data.text_whatsapp_preview);
          setPreviewSubject(data.subject_content);
        }
      } catch (err) {
        toast.error("Failed to fetch live preview");
      } finally {
        setIsPreviewLoading(false);
      }
    }
    fetchPreview();
  }, [debouncedSubject, debouncedEmailBody, debouncedWhatsappBody]);

  const insertVariable = (variable: string) => {
    const el = lastFocused === "subject" ? subjectRef.current : 
               lastFocused === "emailBody" ? emailTextareaRef.current : 
               whatsappTextareaRef.current;
    
    if (!el) return;

    const start = el.selectionStart ?? 0;
    const end = el.selectionEnd ?? 0;

    if (lastFocused === "subject") {
      setSubject(subject.substring(0, start) + variable + subject.substring(end));
    } else if (lastFocused === "emailBody") {
      setEmailBody(emailBody.substring(0, start) + variable + emailBody.substring(end));
    } else {
      setWhatsappBody(whatsappBody.substring(0, start) + variable + whatsappBody.substring(end));
    }

    setTimeout(() => {
      el.selectionStart = el.selectionEnd = start + variable.length;
      el.focus();
    }, 0);
  };

  const hasChanges = subject !== template.subject || emailBody !== template.email_body || whatsappBody !== template.whatsapp_body;

  return (
    <div className="flex flex-col h-full bg-white animate-in fade-in duration-300">
      <div className="flex items-center justify-between px-6 py-4 border-b border-[#e5e5e5] bg-[#fafafa]">
        <div className="flex items-center gap-4">
          <button onClick={onCancel} className="p-1.5 text-[#71717a] hover:text-[#09090b] transition-colors rounded-sm hover:bg-[#e5e5e5]">
            <ArrowLeft size={16} />
          </button>
          <div>
            <h2 className="text-[14px] font-bold text-[#09090b]">{template.name}</h2>
            <p className="text-[11px] text-[#71717a] uppercase tracking-widest font-mono mt-0.5">Channel: {template.channel}</p>
          </div>
        </div>
        <div className="flex items-center gap-2">
          {!template.is_default && (
            <button 
              onClick={() => { if (window.confirm("Reset this template to system defaults?")) onReset(); }}
              disabled={isResetting || isSaving}
              className="h-8 px-3 flex items-center gap-1.5 text-[11px] font-bold uppercase tracking-widest text-rose-600 hover:bg-rose-50 border border-transparent transition-colors disabled:opacity-50"
            >
              {isResetting ? <Loader2 size={13} className="animate-spin" /> : <RotateCcw size={13} />} Reset
            </button>
          )}
          <button 
            onClick={() => onSave(subject, emailBody, whatsappBody)}
            disabled={!hasChanges || isSaving || isResetting}
            className="h-8 px-4 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest rounded-none hover:bg-[#27272a] disabled:opacity-50 transition-colors flex items-center gap-1.5"
          >
            {isSaving ? <Loader2 size={13} className="animate-spin" /> : <Save size={13} />} Save Changes
          </button>
        </div>
      </div>

      <div className="flex-1 grid grid-cols-1 lg:grid-cols-2 min-h-0">
        <div className="flex flex-col border-r border-[#e5e5e5] bg-white">
          <div className="flex items-center border-b border-[#e5e5e5] bg-[#fafafa]">
            <button
              onClick={() => { setActiveTab("EMAIL"); setLastFocused("emailBody"); }}
              className={cn("flex-1 py-3 text-[11px] font-bold uppercase tracking-widest flex items-center justify-center gap-2 border-b-2 transition-colors", activeTab === "EMAIL" ? "border-[#09090b] text-[#09090b] bg-white" : "border-transparent text-[#71717a] hover:text-[#09090b]")}
            >
              <Mail size={14} /> Email Version
            </button>
            <button
              onClick={() => { setActiveTab("WHATSAPP"); setLastFocused("whatsappBody"); }}
              className={cn("flex-1 py-3 text-[11px] font-bold uppercase tracking-widest flex items-center justify-center gap-2 border-b-2 transition-colors", activeTab === "WHATSAPP" ? "border-[#09090b] text-[#09090b] bg-white" : "border-transparent text-[#71717a] hover:text-[#09090b]")}
            >
              <Smartphone size={14} /> WhatsApp Version
            </button>
          </div>

          <div className="flex-1 overflow-y-auto p-6 flex flex-col">
            <div className="mb-6 space-y-2 shrink-0">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Variable Dictionary</label>
              <div className="flex flex-wrap gap-1.5">
                {template.required_variables?.map((v: string) => (
                  <button key={v} onClick={() => insertVariable(v)} className="px-2 py-1 bg-zinc-100 border border-zinc-200 text-[#09090b] text-[10px] font-mono hover:bg-zinc-200 transition-colors">
                    {v} *
                  </button>
                ))}
                {template.optional_variables?.map((v: string) => (
                  <button key={v} onClick={() => insertVariable(v)} className="px-2 py-1 bg-white border border-[#e5e5e5] text-[#71717a] text-[10px] font-mono hover:bg-[#fafafa] transition-colors">
                    {v}
                  </button>
                ))}
              </div>
            </div>

            {activeTab === "EMAIL" && (
              <div className="space-y-4 flex-1 flex flex-col">
                <div className="space-y-1.5 shrink-0">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Subject Line</label>
                  <input 
                    ref={subjectRef}
                    type="text" 
                    value={subject} 
                    onChange={e => setSubject(e.target.value)}
                    onFocus={() => setLastFocused("subject")}
                    className="w-full h-10 px-3 border border-[#e5e5e5] bg-white text-[13px] focus:outline-none focus:border-[#09090b] font-sans"
                  />
                </div>
                <div className="space-y-1.5 flex-1 flex flex-col">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Email Body (HTML/Markdown)</label>
                  <textarea 
                    ref={emailTextareaRef}
                    value={emailBody}
                    onChange={e => setEmailBody(e.target.value)}
                    onFocus={() => setLastFocused("emailBody")}
                    className="w-full flex-1 p-3 border border-[#e5e5e5] bg-white text-[13px] leading-relaxed focus:outline-none focus:border-[#09090b] resize-none font-mono"
                  />
                </div>
              </div>
            )}

            {activeTab === "WHATSAPP" && (
              <div className="space-y-4 flex-1 flex flex-col">
                <div className="space-y-1.5 flex-1 flex flex-col">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">WhatsApp Body (Plain Text)</label>
                  <textarea 
                    ref={whatsappTextareaRef}
                    value={whatsappBody}
                    onChange={e => setWhatsappBody(e.target.value)}
                    onFocus={() => setLastFocused("whatsappBody")}
                    className="w-full flex-1 p-3 border border-[#e5e5e5] bg-white text-[13px] leading-relaxed focus:outline-none focus:border-[#09090b] resize-none font-sans"
                  />
                </div>
              </div>
            )}
          </div>
        </div>

        <div className="bg-[#f4f4f5] overflow-y-auto flex flex-col relative">
          <div className="px-4 py-2 border-b border-[#e5e5e5] bg-[#e4e4e7] flex items-center justify-between sticky top-0 z-10">
            <span className="text-[10px] font-bold uppercase tracking-widest text-[#71717a]">Live Preview</span>
            {activeTab === "EMAIL" && (
              <span className="text-[11px] text-[#52525b] truncate max-w-[250px] font-medium">Subject: {previewSubject}</span>
            )}
          </div>
          
          <div className="p-8 flex-1 flex items-start justify-center relative">
            {isPreviewLoading && (
              <div className="absolute inset-0 bg-[#f4f4f5]/50 flex items-center justify-center z-20 backdrop-blur-[1px]">
                <Loader2 className="animate-spin text-[#a1a1aa]" />
              </div>
            )}
            
            {activeTab === "EMAIL" ? (
              <div className="w-full max-w-[600px] bg-white border border-[#e5e5e5] rounded-lg overflow-hidden shadow-sm">
                <div 
                  className="p-10 text-[#09090b] text-[14px] font-sans leading-[1.6]"
                  dangerouslySetInnerHTML={{ __html: previewEmailHtml }}
                />
                <div className="bg-[#fafafa] border-t border-[#e5e5e5] px-10 py-5 text-center">
                  <p className="m-0 text-[12px] text-[#71717a] font-sans">Powered by <strong className="text-[#09090b]">Lazuar</strong></p>
                </div>
              </div>
            ) : (
              <div className="w-full max-w-[320px] bg-[#e5ddd5] border border-[#d1d1d6] rounded-3xl overflow-hidden shadow-md flex flex-col h-[500px]">
                <div className="bg-[#075e54] text-white p-4 font-semibold text-[13px] flex items-center gap-3 shrink-0">
                  <ArrowLeft size={16} />
                  <span>Business Account</span>
                </div>
                <div className="flex-1 p-4 overflow-y-auto flex flex-col justify-end">
                  <div className="bg-[#dcf8c6] text-[#303030] p-3 rounded-xl rounded-tr-none shadow-sm text-[14px] whitespace-pre-wrap font-sans max-w-[90%] self-end leading-relaxed">
                    {previewWhatsappText}
                  </div>
                </div>
              </div>
            )}
          </div>
        </div>
      </div>
    </div>
  );
}
