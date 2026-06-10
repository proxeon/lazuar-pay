import { useParams, useNavigate, Link } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { ArrowLeft, Menu, Check, X, Database, Loader2 } from "lucide-react";
import { toast } from "sonner";
import { client } from "../lib/api-client";

interface OnboardDetailsPageProps {
  isMobile?: boolean;
  toggleSidebar?: () => void;
}

export default function OnboardDetailsPage({ isMobile, toggleSidebar }: OnboardDetailsPageProps) {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const queryClient = useQueryClient();

  const { data: requests, isLoading } = useQuery({
    queryKey: ["access-requests"],
    queryFn: async () => {
      const { data, error } = await client.GET("/one/access-requests");
      if (error) throw new Error(error.detail || "Failed to fetch access requests.");
      return data ?? [];
    }
  });

  const approveMutation = useMutation({
    mutationFn: async () => {
      const { error } = await client.POST("/one/access-requests/{id}/approve", {
        params: { path: { id: id! } }
      });
      if (error) throw new Error(error.detail || "Failed to approve request.");
    },
    onSuccess: () => {
      toast.success("Workspace provisioned and applicant approved.");
      queryClient.invalidateQueries({ queryKey: ["access-requests"] });
      navigate("/onboard");
    },
    onError: (err: any) => toast.error(err.message)
  });

  const rejectMutation = useMutation({
    mutationFn: async () => {
      const { error } = await client.POST("/one/access-requests/{id}/reject", {
        params: { path: { id: id! } }
      });
      if (error) throw new Error(error.detail || "Failed to reject request.");
    },
    onSuccess: () => {
      toast.info("Registration request declined.");
      queryClient.invalidateQueries({ queryKey: ["access-requests"] });
      navigate("/onboard");
    },
    onError: (err: any) => toast.error(err.message)
  });

  if (isLoading) {
    return (
      <div className="flex-1 w-full flex items-center justify-center h-full text-[#71717a]">
        <Loader2 className="animate-spin h-8 w-8" />
      </div>
    );
  }

  const request = requests?.find((r) => r.id === id);

  if (!request) {
    return (
      <div className="flex-1 w-full p-8 mx-auto max-w-[1240px] text-center py-20">
        <h2 className="text-lg font-bold text-[#09090b]">Request Not Found</h2>
        <p className="text-sm text-[#71717a] mt-2">The requested onboarding registration does not exist inside our directory.</p>
        <Link to="/onboard" className="inline-flex h-10 px-6 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest rounded-none mt-6 items-center">
          ← Return to Queue
        </Link>
      </div>
    );
  }

  return (
    <div className="flex-1 w-full p-4 md:p-8 mx-auto max-w-[1240px] flex flex-col gap-6 animate-in fade-in duration-300">
      
      <header className="flex flex-col pb-2 border-b border-[#e5e5e5] gap-3">
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-3">
            {isMobile && (
              <button onClick={toggleSidebar} className="p-1.5 -ml-1.5 rounded-md text-[#71717a] hover:bg-[#e5e5e5] hover:text-[#09090b] focus:outline-none transition-colors">
                <Menu size={20} />
              </button>
            )}
            <Link to="/onboard" className="inline-flex items-center gap-1.5 text-[#71717a] hover:text-[#09090b] font-bold uppercase tracking-widest transition-colors text-[11px] select-none">
              <ArrowLeft size={14} /> Back to Onboarding
            </Link>
          </div>
        </div>

        <div className="flex flex-col md:flex-row md:items-end justify-between gap-4 mt-2">
          <div>
            <h1 className="text-[24px] font-semibold text-[#09090b] leading-tight">
              Review: {request.name}
            </h1>
            <p className="text-[12px] font-mono text-[#71717a] mt-1">{request.email}</p>
          </div>

          <div className="flex items-center gap-3">
            <span className="text-[11px] font-mono text-[#71717a] bg-white px-2 py-1 border border-[#e5e5e5]">
              Request ID: {request.id}
            </span>
            <span className="inline-flex items-center px-2 py-1 rounded-none border border-amber-200 bg-amber-50 text-[10px] font-bold uppercase tracking-widest text-amber-700 animate-pulse">
              Pending Approval
            </span>
          </div>
        </div>
      </header>

      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 items-start">
        
        <div className="lg:col-span-2 space-y-6">
          <div className="bg-white border border-[#e5e5e5] rounded-none shadow-[4px_4px_0px_0px_rgba(0,0,0,0.05)] overflow-hidden">
            <div className="px-5 py-4 border-b border-[#f4f4f5] bg-[#fafafa]/50 flex items-center gap-2">
              <Database size={16} className="text-[#a1a1aa]" />
              <h2 className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">Application Metadata</h2>
            </div>
            
            <div className="p-6 grid grid-cols-1 md:grid-cols-2 gap-4">
              <div className="space-y-1">
                <span className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block">Date Submitted</span>
                <span className="text-[13px] font-mono text-[#09090b]">{new Date(request.created_at).toLocaleString()}</span>
              </div>
              <div className="space-y-1">
                <span className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block">Requested Access Role</span>
                <span className="text-[13px] font-mono text-[#09090b]">CLIENT (Standard Client Portal Access)</span>
              </div>
            </div>
          </div>

          <div className="bg-white border border-[#e5e5e5] rounded-none shadow-[4px_4px_0px_0px_rgba(0,0,0,0.05)] overflow-hidden">
            <div className="px-5 py-4 border-b border-[#f4f4f5] bg-[#fafafa]/50">
              <h2 className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">Requested App Access Details</h2>
            </div>

            <div className="p-6 space-y-4">
              <p className="text-[13px] text-[#71717a] leading-relaxed">
                If approved, the client profile will be generated with credentials automatically entitled to these selected modules:
              </p>
              
              <div className="flex flex-wrap gap-2">
                {request.requested_apps.map((app) => (
                  <span key={app} className="px-3 py-1.5 bg-zinc-50 border border-zinc-200 text-[#52525b] text-[11px] font-bold uppercase tracking-wider font-mono">
                    {app}
                  </span>
                ))}
              </div>
            </div>
          </div>

        </div>

        <div className="lg:col-span-1 space-y-6">
          <div className="bg-white border border-[#e5e5e5] rounded-none shadow-[4px_4px_0px_0px_rgba(0,0,0,0.05)] overflow-hidden">
            <div className="px-5 py-4 border-b border-[#f4f4f5] bg-[#fafafa]/50">
              <h2 className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">Review Decision</h2>
            </div>
            
            <div className="p-6 space-y-5">
              <div className="p-4 border border-[#e5e5e5] bg-[#fafafa]/50 space-y-2">
                <p className="text-[12px] text-[#09090b] font-semibold">Cognitive Provisioning</p>
                <p className="text-[11px] text-[#71717a] leading-relaxed">
                  Approving this request will dynamically generate a workspace, assign app entitlements, and dispatch a welcome email to <code className="font-mono text-[#09090b]">{request.email}</code>.
                </p>
              </div>

              <div className="space-y-3 pt-2">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a] block mb-1">Queue Action</label>
                
                <button 
                  type="button"
                  onClick={() => approveMutation.mutate()}
                  disabled={approveMutation.isPending || rejectMutation.isPending}
                  className="w-full h-10 border border-emerald-200 bg-emerald-50 text-emerald-700 hover:bg-emerald-100 text-[11px] font-bold uppercase tracking-widest rounded-none transition-colors flex items-center justify-center gap-2 focus:outline-none disabled:opacity-50"
                >
                  {approveMutation.isPending ? <Loader2 size={13} className="animate-spin" /> : <Check size={13} />}
                  Approve Application
                </button>

                <button 
                  type="button"
                  onClick={() => rejectMutation.mutate()}
                  disabled={approveMutation.isPending || rejectMutation.isPending}
                  className="w-full h-10 border border-rose-200 bg-rose-50 text-rose-600 hover:bg-rose-100 text-[11px] font-bold uppercase tracking-widest rounded-none transition-colors flex items-center justify-center gap-2 focus:outline-none disabled:opacity-50"
                >
                  {rejectMutation.isPending ? <Loader2 size={13} className="animate-spin" /> : <X size={13} />}
                  Decline Application
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
}
