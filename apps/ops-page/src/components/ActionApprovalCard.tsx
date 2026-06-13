import { useState } from "react";
import { Check, Loader2, Terminal, AlertCircle } from "lucide-react";
import { useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { cn } from "../lib/utils";
import { client, type ProposedActionDto } from "../lib/api-client";

interface ActionApprovalCardProps {
  action: ProposedActionDto;
  onResolved: (success: boolean, message?: string, actionRef?: ProposedActionDto) => void;
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

      if (error) {
        toast.error("Execution Failed", { description: error.detail || "An error occurred." });
        onResolved(false, error.detail, action);
      } else {
        toast.success("Action Executed Successfully");
        queryClient.invalidateQueries();
        onResolved(true, undefined, action);
      }
    } catch (err) {
      toast.error("Network Error", { description: "Failed to reach the server." });
      onResolved(false, "Network error", action);
    } finally {
      setIsExecuting(false);
    }
  };

  const isHighSeverity = action.severity === "high";

  return (
    <div className="w-full max-w-[540px] mt-2 mb-4 bg-white border border-[#e5e5e5] rounded-lg overflow-hidden flex flex-col font-sans animate-in fade-in slide-in-from-bottom-2 duration-300">
      
      <div className="px-4 py-3 border-b border-[#f4f4f5] bg-[#fafafa]/50 flex items-center justify-between">
        <div className="flex items-center gap-2.5">
          {isHighSeverity ? (
            <AlertCircle size={15} className="text-rose-600" />
          ) : (
            <Terminal size={15} className="text-[#71717a]" />
          )}
          <h4 className={cn("text-[11px] font-bold uppercase tracking-widest", isHighSeverity ? "text-rose-600" : "text-[#09090b]")}>
            {action.intent_title}
          </h4>
        </div>
        <span className="text-[10px] font-mono text-[#a1a1aa] uppercase tracking-wider">System Proposal</span>
      </div>

      <div className="p-4 space-y-4">
        <p className="text-[13px] text-[#52525b] leading-relaxed">
          {action.human_readable_summary}
        </p>
        
        <div className="rounded-md border border-[#e5e5e5] overflow-hidden">
          <div className="bg-[#fafafa] border-b border-[#e5e5e5] px-3 py-1.5 flex items-center">
            <span className="text-[10px] font-bold text-[#71717a] uppercase tracking-widest">Payload Data</span>
          </div>
          <div className="bg-white p-0 m-0 overflow-x-auto max-h-[280px] overflow-y-auto">
            <pre className="p-3 m-0 text-[11.5px] font-mono text-[#09090b] bg-transparent border-0">
              {JSON.stringify(action.command_payload, null, 2)}
            </pre>
          </div>
        </div>
      </div>

      <div className="px-4 py-3 border-t border-[#f4f4f5] bg-[#fafafa]/50 flex items-center justify-end gap-2.5">
        <button
          onClick={() => onResolved(false, "Action cancelled by user.", action)}
          disabled={isExecuting}
          className="h-8 px-4 rounded-sm border border-[#e5e5e5] bg-white text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:bg-[#f4f4f5] hover:text-[#09090b] transition-colors disabled:opacity-50"
        >
          Cancel
        </button>
        <button
          onClick={handleApprove}
          disabled={isExecuting}
          className={cn(
            "h-8 px-6 rounded-sm text-white text-[11px] font-bold uppercase tracking-widest flex items-center justify-center gap-2 transition-colors disabled:opacity-50",
            isHighSeverity ? "bg-rose-600 hover:bg-rose-700" : "bg-[#09090b] hover:bg-[#27272a]"
          )}
        >
          {isExecuting ? <Loader2 size={13} className="animate-spin" /> : <Check size={13} />}
          Execute
        </button>
      </div>

    </div>
  );
}
