// apps/ops-page/src/components/chat/UiRequestCard.tsx
import { useState } from "react";
import { Check, FileText, X } from "lucide-react";
import { FormRegistry } from "./FormRegistry";
import AutoForm from "./AutoForm";
import type { components } from "../../lib/api-client";

type UiRequestDto = components["schemas"]["Ops.UiRequestDto"];

interface UiRequestCardProps {
  uiRequest: UiRequestDto;
  onSubmit: (data: Record<string, any>) => void;
  onCancel: () => void;
}

export default function UiRequestCard({ uiRequest, onSubmit, onCancel }: UiRequestCardProps) {
  const [isModalOpen, setIsModalOpen] = useState(false);

  const humanReadableToolName = uiRequest.tool_name
    .replace("Command", "")
    .replace(/([A-Z])/g, " $1")
    .trim();

  if (uiRequest.is_resolved) {
    return (
      <div className="w-full max-w-[540px] mt-2 mb-4 bg-emerald-50 border border-emerald-200 rounded-lg p-3 flex items-center gap-2.5">
        <div className="h-6 w-6 rounded-full bg-emerald-100 flex items-center justify-center shrink-0">
          <Check size={14} className="text-emerald-600" />
        </div>
        <div>
          <p className="text-[11px] font-bold text-emerald-800 uppercase tracking-widest">Data Submitted</p>
          <p className="text-[12px] text-emerald-700">{humanReadableToolName} payload confirmed.</p>
        </div>
      </div>
    );
  }

  const CustomComponent = FormRegistry[uiRequest.tool_name];
  
  const properties = (uiRequest.schema_json as any)?.properties || {};
  const propertyCount = Object.keys(properties).filter(k => k !== "_meta").length;
  const isComplex = propertyCount > 4;

  const handleModalSubmit = (data: Record<string, any>) => {
    setIsModalOpen(false);
    onSubmit(data);
  };

  const handleModalCancel = () => {
    setIsModalOpen(false);
    onCancel();
  };

  const renderForm = (submitFn: (data: any) => void, cancelFn: () => void) => {
    if (CustomComponent) {
      return <CustomComponent prefillData={uiRequest.prefill_data as any} onSubmit={submitFn} onCancel={cancelFn} />;
    }
    return <AutoForm schema={uiRequest.schema_json} prefillData={uiRequest.prefill_data} onSubmit={submitFn} onCancel={cancelFn} />;
  };

  return (
    <>
      <div className="w-full max-w-[540px] mt-2 mb-4 bg-white border border-[#e5e5e5] rounded-lg overflow-hidden flex flex-col font-sans animate-in fade-in slide-in-from-bottom-2 duration-300">
        <div className="px-4 py-3 border-b border-[#f4f4f5] bg-[#fafafa]/50 flex items-center justify-between">
          <div className="flex items-center gap-2.5">
            <FileText size={15} className="text-blue-600" />
            <h4 className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">
              Action Requires Input
            </h4>
          </div>
          <span className="text-[10px] font-mono text-[#a1a1aa] uppercase tracking-wider">UI Request</span>
        </div>

        <div className="bg-white">
          <div className="px-4 pt-4 pb-2">
            <p className="text-[13px] text-[#52525b] leading-relaxed">
              Please provide the missing details to continue executing <strong>{humanReadableToolName}</strong>.
            </p>
          </div>

          {isComplex ? (
            <div className="px-4 pb-4 pt-2">
              <button
                onClick={() => setIsModalOpen(true)}
                className="w-full h-10 border border-[#e5e5e5] bg-[#fafafa] text-[#09090b] text-[12px] font-bold uppercase tracking-widest hover:border-[#09090b] hover:bg-white transition-colors rounded-sm"
              >
                Open Form Wizard
              </button>
            </div>
          ) : (
            renderForm(onSubmit, onCancel)
          )}
        </div>
      </div>

      {isComplex && isModalOpen && (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
          <div className="absolute inset-0 bg-black/20 backdrop-blur-sm transition-opacity" onClick={() => setIsModalOpen(false)} />
          
          <div className="relative bg-white border border-[#e5e5e5] shadow-lg w-full max-w-lg max-h-[90vh] overflow-hidden flex flex-col animate-in fade-in zoom-in-95 duration-200">
            <div className="flex items-center justify-between p-4 border-b border-[#e5e5e5] bg-[#fafafa]/50 shrink-0">
              <h3 className="text-[13px] font-bold uppercase tracking-widest text-[#09090b]">{humanReadableToolName}</h3>
              <button onClick={() => setIsModalOpen(false)} className="text-[#a1a1aa] hover:bg-[#e5e5e5] hover:text-[#09090b] transition-colors p-1"><X size={16} /></button>
            </div>
            
            <div className="overflow-y-auto flex-1">
              {renderForm(handleModalSubmit, handleModalCancel)}
            </div>
          </div>
        </div>
      )}
    </>
  );
}
