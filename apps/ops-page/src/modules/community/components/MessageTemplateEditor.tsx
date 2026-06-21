// apps/ops-page/src/modules/community/components/MessageTemplateEditor.tsx
import { useState, useRef } from "react";
import { ArrowLeft, Loader2, Save, RotateCcw } from "lucide-react";
import type { components } from "../../../lib/api-client";

type MessageTemplateDto = components["schemas"]["Community.MessageTemplateDto"];

interface MessageTemplateEditorProps {
  template: MessageTemplateDto;
  onSave: (subject: string, body: string) => void;
  onReset: () => void;
  onCancel: () => void;
  isSaving: boolean;
  isResetting: boolean;
}

export default function MessageTemplateEditor({ 
  template, onSave, onReset, onCancel, isSaving, isResetting 
}: MessageTemplateEditorProps) {
  const [subject, setSubject] = useState(template.subject);
  const [body, setBody] = useState(template.body);

  const textareaRef = useRef<HTMLTextAreaElement>(null);
  const subjectRef = useRef<HTMLInputElement>(null);
  const [lastFocused, setLastFocused] = useState<"subject" | "body">("body");

  const insertVariable = (variable: string) => {
    const el = lastFocused === "subject" ? subjectRef.current : textareaRef.current;
    if (!el) return;

    const start = el.selectionStart ?? 0;
    const end = el.selectionEnd ?? 0;

    if (lastFocused === "subject") {
      const newText = subject.substring(0, start) + variable + subject.substring(end);
      setSubject(newText);
    } else {
      const newText = body.substring(0, start) + variable + body.substring(end);
      setBody(newText);
    }

    setTimeout(() => {
      el.selectionStart = el.selectionEnd = start + variable.length;
      el.focus();
    }, 0);
  };

  const hasChanges = subject !== template.subject || body !== template.body;

  // Placeholder for Live Preview rendering (Replaced in Phase 5)
  const renderedPreview = body.replace(/\n/g, "<br/>");
  const renderedSubject = subject;

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
            onClick={() => onSave(subject, body)}
            disabled={!hasChanges || isSaving || isResetting}
            className="h-8 px-4 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest rounded-none hover:bg-[#27272a] disabled:opacity-50 transition-colors flex items-center gap-1.5"
          >
            {isSaving ? <Loader2 size={13} className="animate-spin" /> : <Save size={13} />} Save Changes
          </button>
        </div>
      </div>

      <div className="flex-1 grid grid-cols-1 lg:grid-cols-2 min-h-0">
        <div className="flex flex-col border-r border-[#e5e5e5] overflow-y-auto p-6 bg-white">
          <div className="mb-6 space-y-2">
            <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Variable Dictionary</label>
            <div className="flex flex-wrap gap-1.5">
              {template.required_variables.map(v => (
                <button key={v} onClick={() => insertVariable(v)} className="px-2 py-1 bg-zinc-100 border border-zinc-200 text-[#09090b] text-[10px] font-mono hover:bg-zinc-200 transition-colors">
                  {v} *
                </button>
              ))}
              {template.optional_variables.map(v => (
                <button key={v} onClick={() => insertVariable(v)} className="px-2 py-1 bg-white border border-[#e5e5e5] text-[#71717a] text-[10px] font-mono hover:bg-[#fafafa] transition-colors">
                  {v}
                </button>
              ))}
            </div>
          </div>

          <div className="space-y-4 flex-1 flex flex-col">
            <div className="space-y-1.5">
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
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Message Body</label>
              <textarea 
                ref={textareaRef}
                value={body}
                onChange={e => setBody(e.target.value)}
                onFocus={() => setLastFocused("body")}
                className="w-full flex-1 p-3 border border-[#e5e5e5] bg-white text-[13px] leading-relaxed focus:outline-none focus:border-[#09090b] resize-none font-mono"
              />
            </div>
          </div>
        </div>

        <div className="bg-[#f4f4f5] overflow-y-auto flex flex-col">
          <div className="px-4 py-2 border-b border-[#e5e5e5] bg-[#e4e4e7] flex items-center justify-between sticky top-0 z-10">
            <span className="text-[10px] font-bold uppercase tracking-widest text-[#71717a]">Live Preview</span>
            <span className="text-[11px] text-[#52525b] truncate max-w-[250px] font-medium">Subject: {renderedSubject}</span>
          </div>
          
          <div className="p-8 flex-1 flex items-start justify-center">
            <div className="w-full max-w-[600px] bg-white border border-[#e5e5e5] rounded-lg overflow-hidden shadow-sm">
              <div 
                className="p-10 text-[#09090b] text-[15px] font-sans leading-[1.6]"
                dangerouslySetInnerHTML={{ __html: renderedPreview }}
              />
              <div className="bg-[#fafafa] border-t border-[#e5e5e5] px-10 py-5 text-center">
                <p className="m-0 text-[12px] text-[#71717a] font-sans">Powered by <strong className="text-[#09090b]">Lazuar</strong></p>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
