import { useState } from "react";
import { Plus, AlertTriangle } from "lucide-react";
import DunningStepEditor from "./DunningStepEditor";
import type { LocalStepState } from "./types";

interface CampaignTimelineProps {
  steps: LocalStepState[];
  setSteps: React.Dispatch<React.SetStateAction<LocalStepState[]>>;
  isActionLoading: boolean;
  allowAutoCharge?: boolean;
}

export default function CampaignTimeline({ steps, setSteps, isActionLoading, allowAutoCharge = true }: CampaignTimelineProps) {
  const [expandedStepIndex, setExpandedStepIndex] = useState<number | null>(null);

  const addStep = () => {
    setSteps(prev => {
      const newIndex = prev.length;
      setExpandedStepIndex(newIndex);
      return [...prev, { day_offset: "0", action_type: "EMAIL", subject: "", email_body: "", whatsapp_body: "" }];
    });
  };

  const removeStep = (index: number) => {
    setSteps(prev => prev.filter((_, i) => i !== index));
    if (expandedStepIndex === index) setExpandedStepIndex(null);
  };

  const updateStep = (index: number, field: keyof LocalStepState, value: any) => {
    setSteps(prev => prev.map((step, i) => i === index ? { ...step, [field]: value } : step));
  };

  return (
    <div className="lg:col-span-7 space-y-6">
      <div className="flex items-center justify-between border-b border-[#e5e5e5] pb-4 bg-white p-4 rounded-sm shadow-sm">
        <div>
          <h3 className="text-lg font-bold text-[#09090b] tracking-tight">Sequence Timeline</h3>
          <p className="text-[11px] text-[#71717a]">Visual map of the recovery operations.</p>
        </div>
        <button type="button" onClick={addStep} disabled={isActionLoading} className="h-8 px-4 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest hover:bg-[#27272a] rounded-sm transition-colors flex items-center gap-1.5">
          <Plus size={14}/> Add Step
        </button>
      </div>

      <div className="pl-6 border-l-2 border-[#e5e5e5] ml-4 relative space-y-8 py-4">
        <div className="absolute top-0 -left-[17px] bg-[#f4f4f5] border-2 border-[#e5e5e5] h-8 w-8 rounded-full flex items-center justify-center shadow-sm">
          <span className="text-[10px] font-bold text-[#71717a]">T0</span>
        </div>

        {steps.length === 0 ? (
          <div className="p-8 border border-dashed border-[#e5e5e5] bg-white rounded-sm text-center text-[#71717a] text-[13px] shadow-sm ml-4">
            No actions are configured. This campaign will wait until the Grace Period ends, then execute the Terminal Action.
          </div>
        ) : (
          steps.map((step, idx) => (
            <DunningStepEditor
              key={idx}
              index={idx}
              step={step}
              isExpanded={expandedStepIndex === idx}
              onToggleExpand={() => setExpandedStepIndex(expandedStepIndex === idx ? null : idx)}
              onUpdate={updateStep}
              onRemove={removeStep}
              isActionLoading={isActionLoading}
              allowAutoCharge={allowAutoCharge}
            />
          ))
        )}

        <div className="absolute bottom-0 -left-[17px] bg-rose-50 border-2 border-rose-200 h-8 w-8 rounded-full flex items-center justify-center shadow-sm z-10 mt-8">
          <AlertTriangle size={14} className="text-rose-600" />
        </div>
      </div>
    </div>
  );
}
