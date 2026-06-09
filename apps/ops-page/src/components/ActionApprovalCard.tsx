// apps/ops-page/src/components/ActionApprovalCard.tsx
import { useState } from "react";
import { Check, X, Loader2, AlertTriangle, Info, AlertCircle } from "lucide-react";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { cn } from "../lib/utils";
import { client, type ProposedActionDto } from "../lib/api-client";

interface ActionApprovalCardProps {
  action: ProposedActionDto;
  onResolved: (success: boolean, message?: string) => void;
}

export default function ActionApprovalCard({ action, onResolved }: ActionApprovalCardProps) {
  const [isExecuting, setIsExecuting] = useState(false);
  const queryClient = useQueryClient();

  const handleApprove = async () => {
    setIsExecuting(true);
    try {
      const { error } = await client.POST("/ops/execute-action", {
        body: action
      });

      // Passes the exact BusinessRuleValidationException message (error.detail) back to the LLM context loop
      if (error) {
        toast.error("Execution Failed", { description: error.detail || "An error occurred." });
        onResolved(false, error.detail);
      } else {
        toast.success("Action Executed Successfully");
        queryClient.invalidateQueries();
        onResolved(true);
      }
    } catch (err) {
      toast.error("Network Error", { description: "Failed to reach the server." });
      onResolved(false, "Network error");
    } finally {
      setIsExecuting(false);
    }
  };

  const severityConfig = {
    low: { bg: "bg-blue-50/50", border: "border-blue-200", text: "text-blue-700", icon: Info },
    medium: { bg: "bg-amber-50/50", border: "border-amber-200", text: "text-amber-700", icon: AlertTriangle },
    high: { bg: "bg-rose-50/50", border: "border-rose-200", text: "text-rose-700", icon: AlertCircle },
  };

  const config = severityConfig[action.severity as keyof typeof severityConfig] || severityConfig.low;
  const Icon = config.icon;

  return (
    <div className={cn("rounded-md border p-4 shadow-sm w-full max-w-sm my-3 animate-in fade-in zoom-in-95 duration-200", config.bg, config.border)}>
      <div className="flex items-start gap-3 mb-4">
        <Icon className={cn("h-5 w-5 shrink-0 mt-0.5", config.text)} />
        <div className="min-w-0 w-full">
          <h4 className="text-[13px] font-bold uppercase tracking-widest text-[#09090b] leading-none mb-1.5">{action.intent_title}</h4>
          <p className="text-[13px] text-[#52525b] leading-snug break-words">{action.human_readable_summary}</p>
          
          {/* Dynamically renders untyped MediatR payload arguments proposed by the LLM before execution */}
          <pre className="mt-3 p-2 bg-black/5 text-[10px] font-mono text-[#52525b] overflow-x-auto rounded-sm">
            {JSON.stringify(action.command_payload, null, 2)}
          </pre>
        </div>
      </div>

      <div className="flex items-center gap-2 pt-3 border-t border-black/5">
        <button
          onClick={() => onResolved(false, "Action cancelled by user.")}
          disabled={isExecuting}
          className="flex-1 h-9 rounded-sm border border-[#e5e5e5] bg-white text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:bg-[#f4f4f5] hover:text-[#09090b] transition-colors disabled:opacity-50"
        >
          Cancel
        </button>
        <button
          onClick={handleApprove}
          disabled={isExecuting}
          className="flex-1 h-9 rounded-sm bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest flex items-center justify-center gap-1.5 hover:bg-[#27272a] transition-colors disabled:opacity-50 shadow-sm"
        >
          {isExecuting ? <Loader2 size={14} className="animate-spin" /> : <><Check size={14} /> Approve</>}
        </button>
      </div>
    </div>
  );
}
