import { useState } from "react";
import { Loader2, Trash2, CreditCard, Mail, Smartphone, Eye, Code, ChevronRight } from "lucide-react";
import { toast } from "sonner";
import { client } from "../../../../lib/api-client";
import { cn } from "../../../../lib/utils";
import { MarkdownContent } from "../../../../components/chat/MarkdownContent";
import type { LocalStepState } from "./types";

interface DunningStepEditorProps {
  step: LocalStepState;
  index: number;
  isExpanded: boolean;
  onToggleExpand: () => void;
  onUpdate: (index: number, field: keyof LocalStepState, value: any) => void;
  onRemove: (index: number) => void;
  isActionLoading: boolean;
  allowAutoCharge?: boolean;
}

export default function DunningStepEditor({
  step,
  index,
  isExpanded,
  onToggleExpand,
  onUpdate,
  onRemove,
  isActionLoading,
  allowAutoCharge = true
}: DunningStepEditorProps) {
  const [showPreview, setShowPreview] = useState(false);
  const [previewHtml, setPreviewHtml] = useState("");
  const [previewText, setPreviewText] = useState("");
  const [previewSubject, setPreviewSubject] = useState("");
  const [isPreviewLoading, setIsPreviewLoading] = useState(false);

  const fetchLivePreview = async () => {
    setIsPreviewLoading(true);
    try {
      const { data, error } = await client.POST("/admin/communications/templates/preview", {
        body: { 
          subject: step.subject, 
          email_body: step.email_body, 
          whatsapp_body: step.whatsapp_body 
        }
      });
      if (data && !error) {
        setPreviewHtml(data.html_email_preview);
        setPreviewText(data.text_whatsapp_preview);
        setPreviewSubject(data.subject_content);
      }
    } catch (err) {
      toast.error("Failed to generate live preview.");
    } finally {
      setIsPreviewLoading(false);
    }
  };

  const handleTogglePreview = () => {
    if (!showPreview) {
      fetchLivePreview();
    }
    setShowPreview(!showPreview);
  };

  const getStepDayLabel = (offset: string) => {
    const day = parseInt(offset, 10);
    if (day < 0) return `Day ${day}`;
    if (day === 0) return "Day 0";
    return `Day +${day}`;
  };

  const getStepIcon = (actionType: string) => {
    if (actionType === "AUTO_CHARGE") return <CreditCard size={14} />;
    if (actionType === "WHATSAPP") return <Smartphone size={14} />;
    return <Mail size={14} />;
  };

  return (
    <div className="relative ml-4">
      <div className="absolute top-4 -left-[38px] bg-white border-2 border-[#e5e5e5] h-6 w-6 rounded-full flex items-center justify-center shadow-sm z-10">
        <div className={cn("text-[#09090b]", step.action_type === "AUTO_CHARGE" ? "text-blue-600" : "")}>
          {getStepIcon(step.action_type)}
        </div>
      </div>

      <div className={cn("bg-white border transition-all rounded-sm shadow-sm overflow-hidden", isExpanded ? "border-[#09090b]" : "border-[#e5e5e5] hover:border-[#a1a1aa] cursor-pointer")}>
        <div 
          className={cn("flex items-center justify-between p-4", isExpanded && "bg-[#fafafa] border-b border-[#e5e5e5]")}
          onClick={onToggleExpand}
        >
          <div className="flex items-center gap-4">
            <span className="text-[12px] font-mono font-bold bg-[#f4f4f5] border border-[#e5e5e5] px-2 py-1 rounded-sm w-[70px] text-center">
              {getStepDayLabel(step.day_offset)}
            </span>
            <div className="flex flex-col">
              <span className="text-[13px] font-bold text-[#09090b]">
                {step.action_type === "EMAIL" ? "Send Email Reminder" : step.action_type === "WHATSAPP" ? "Send WhatsApp Alert" : "Attempt Auto-Charge"}
              </span>
              {!isExpanded && step.action_type === "EMAIL" && step.subject && (
                <span className="text-[11px] text-[#71717a] truncate max-w-sm mt-0.5">Subject: {step.subject}</span>
              )}
            </div>
          </div>
          <div className="flex items-center gap-2">
            <button 
              type="button" 
              onClick={(e) => { e.stopPropagation(); onRemove(index); }} 
              className="p-1.5 text-rose-400 hover:text-rose-600 hover:bg-rose-50 transition-colors rounded-sm"
            >
              <Trash2 size={14} />
            </button>
            <ChevronRight size={16} className={cn("text-[#a1a1aa] transition-transform", isExpanded && "rotate-90")} />
          </div>
        </div>

        {isExpanded && (
          <div className="p-5 space-y-6">
            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-1.5">
                <label className="text-[10px] uppercase tracking-wider text-[#71717a] font-bold">Timing Offset</label>
                <select 
                  value={step.day_offset} 
                  onChange={e => onUpdate(index, "day_offset", e.target.value)} 
                  disabled={isActionLoading} 
                  className="w-full h-9 px-3 border border-[#e5e5e5] text-[13px] rounded-sm focus:outline-none focus:border-[#09090b]"
                >
                  <option value="-14">14 Days Before Due</option>
                  <option value="-7">7 Days Before Due</option>
                  <option value="-3">3 Days Before Due</option>
                  <option value="-1">1 Day Before Due</option>
                  <option value="0">On Due Date</option>
                  <option value="1">1 Day After Due</option>
                  <option value="3">3 Days After Due</option>
                  <option value="5">5 Days After Due</option>
                  <option value="7">7 Days After Due</option>
                  <option value="14">14 Days After Due</option>
                  <option value="30">30 Days After Due</option>
                </select>
              </div>
              <div className="space-y-1.5">
                <label className="text-[10px] uppercase tracking-wider text-[#71717a] font-bold">Action Type</label>
                <select 
                  value={step.action_type} 
                  onChange={e => {
                    onUpdate(index, "action_type", e.target.value);
                    setShowPreview(false);
                  }} 
                  disabled={isActionLoading} 
                  className="w-full h-9 px-3 border border-[#e5e5e5] text-[13px] rounded-sm focus:outline-none focus:border-[#09090b]"
                >
                  <option value="EMAIL">Send Email</option>
                  <option value="WHATSAPP">Send WhatsApp (not connected)</option>
                  {allowAutoCharge && <option value="AUTO_CHARGE">Auto-Retry Card</option>}
                  {!allowAutoCharge && step.action_type === "AUTO_CHARGE" && (
                    <option value="AUTO_CHARGE">Auto-Retry Card (not available)</option>
                  )}
                </select>
              </div>
            </div>

            {step.action_type === "WHATSAPP" && (
              <div className="p-4 bg-amber-50 border border-amber-200 flex items-start gap-3 rounded-sm">
                <Smartphone size={18} className="text-amber-700 mt-0.5 shrink-0" />
                <div className="text-[12px] text-amber-900 leading-relaxed space-y-1">
                  <p className="font-bold">Email only until WhatsApp connected</p>
                  <p>
                    WhatsApp Business delivery is not connected yet. While disabled, WHATSAPP steps run as email when an email body is present; otherwise the step is skipped. Prefer <strong>Send Email</strong> for recovery until Meta Cloud is wired.
                  </p>
                </div>
              </div>
            )}

            {step.action_type === "AUTO_CHARGE" && !allowAutoCharge && (
              <div className="p-4 bg-amber-50 border border-amber-200 flex items-start gap-3 rounded-sm">
                <CreditCard size={18} className="text-amber-700 mt-0.5 shrink-0" />
                <div className="text-[12px] text-amber-900 leading-relaxed space-y-1">
                  <p className="font-bold">Auto-Retry Card is not available</p>
                  <p>
                    Selected products are reminder-only (Billplz / offline). Change this step to email, or target a Stripe/CHIP product.
                  </p>
                </div>
              </div>
            )}

            {step.action_type === "AUTO_CHARGE" && allowAutoCharge && (
              <div className="p-4 bg-blue-50 border border-blue-200 flex items-start gap-3 rounded-sm">
                <CreditCard size={18} className="text-blue-600 mt-0.5 shrink-0" />
                <div className="text-[12px] text-blue-800 leading-relaxed space-y-1.5">
                  <p>
                    The system will silently request the Payment Gateway (Stripe/CHIP) to charge the customer's vaulted card.
                    Lazuar limits retries to a maximum of 4 attempts per billing cycle to prevent gateway fraud flags.
                  </p>
                  <p className="text-amber-800 bg-amber-50/80 border border-amber-200 rounded-sm px-2 py-1.5">
                    <strong>Billplz limitation:</strong> Billplz does not support off-session auto-charge. Products on Billplz will skip silent retries — use email/WhatsApp steps with an update-payment link, or switch the product gateway to Stripe/CHIP for vaulted renewals.
                  </p>
                </div>
              </div>
            )}

            {(step.action_type === "EMAIL" || step.action_type === "WHATSAPP") && (
              <div className="space-y-4">
                <div className="flex items-center justify-between border-b border-[#f4f4f5] pb-2">
                  <span className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">Message Copy</span>
                  <button 
                    type="button" 
                    onClick={handleTogglePreview}
                    className="h-7 px-3 bg-[#f4f4f5] hover:bg-[#e5e5e5] text-[#09090b] text-[10px] font-bold uppercase tracking-widest rounded-sm transition-colors flex items-center gap-1.5"
                  >
                    {showPreview ? <Code size={12} /> : <Eye size={12} />} {showPreview ? "Edit Mode" : "Live Preview"}
                  </button>
                </div>

                {showPreview ? (
                  <div className="p-6 bg-[#fafafa] border border-[#e5e5e5] rounded-sm min-h-[200px] flex items-center justify-center relative">
                    {isPreviewLoading && (
                      <div className="absolute inset-0 bg-[#fafafa]/50 flex items-center justify-center z-10 backdrop-blur-[1px]">
                        <Loader2 className="animate-spin text-[#a1a1aa]" />
                      </div>
                    )}
                    
                    {step.action_type === "EMAIL" ? (
                      <div className="w-full bg-white border border-[#e5e5e5] shadow-sm rounded-sm overflow-hidden">
                        <div className="px-6 py-3 border-b border-[#e5e5e5] bg-[#fafafa] text-[12px] font-medium text-[#09090b]">
                          Subj: {previewSubject || "No Subject"}
                        </div>
                        <div 
                          className="p-6 text-[13px] text-[#09090b] font-sans leading-relaxed"
                          dangerouslySetInnerHTML={{ __html: previewHtml || "<p class='text-[#a1a1aa] italic'>No content</p>" }}
                        />
                      </div>
                    ) : (
                      <div className="w-full max-w-[320px] bg-[#e5ddd5] border border-[#d1d1d6] rounded-xl overflow-hidden shadow-sm flex flex-col h-[400px]">
                        <div className="bg-[#075e54] text-white p-3 font-semibold text-[12px] flex items-center gap-2 shrink-0">
                          WhatsApp Business
                        </div>
                        <div className="flex-1 p-3 overflow-y-auto flex flex-col justify-end">
                          <div className="bg-[#dcf8c6] text-[#303030] p-3 rounded-lg rounded-tr-none shadow-sm text-[13px] whitespace-pre-wrap font-sans max-w-[90%] self-end leading-relaxed">
                            {previewText || "No content"}
                          </div>
                        </div>
                      </div>
                    )}
                  </div>
                ) : (
                  <div className="space-y-4">
                    {step.action_type === "EMAIL" && (
                      <div className="space-y-1.5">
                        <label className="text-[11px] font-semibold text-[#52525b]">Subject Line</label>
                        <input 
                          type="text" 
                          value={step.subject} 
                          onChange={e => onUpdate(index, "subject", e.target.value)} 
                          disabled={isActionLoading}
                          placeholder="e.g. Action Required: Your payment failed"
                          className="w-full h-10 px-3 rounded-sm border border-[#e5e5e5] text-[13px] focus:outline-none focus:border-[#09090b]"
                        />
                      </div>
                    )}
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-semibold text-[#52525b]">Message Body ({step.action_type === "EMAIL" ? "HTML/Markdown" : "Plain Text"})</label>
                      <textarea 
                        value={step.action_type === "EMAIL" ? step.email_body : step.whatsapp_body} 
                        onChange={e => onUpdate(index, step.action_type === "EMAIL" ? "email_body" : "whatsapp_body", e.target.value)} 
                        disabled={isActionLoading}
                        rows={6}
                        placeholder="Available variables: {{customer_name}}, {{plan_name}}, {{update_payment_link}}"
                        className={cn("w-full p-3 rounded-sm border border-[#e5e5e5] text-[13px] focus:outline-none focus:border-[#09090b] resize-y", step.action_type === "EMAIL" ? "font-mono" : "font-sans")}
                      />
                    </div>
                  </div>
                )}
              </div>
            )}

            <div className="pt-4 border-t border-[#f4f4f5] flex justify-end">
              <button type="button" onClick={() => { onToggleExpand(); setShowPreview(false); }} className="text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:text-[#09090b] transition-colors">
                Done Editing
              </button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
