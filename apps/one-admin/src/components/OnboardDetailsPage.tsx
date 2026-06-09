import { useParams, useNavigate, Link } from "react-router-dom";
import { ArrowLeft, Menu, Check, X, Database } from "lucide-react";
import { toast } from "sonner";
import type { OnboardingRequest } from "./Onboard";
import type { MockUser } from "./Users";

interface OnboardDetailsPageProps {
  pendingRequests: OnboardingRequest[];
  setPendingRequests: React.Dispatch<React.SetStateAction<OnboardingRequest[]>>;
  setUsers: React.Dispatch<React.SetStateAction<MockUser[]>>;
  isMobile?: boolean;
  toggleSidebar?: () => void;
}

export default function OnboardDetailsPage({
  pendingRequests,
  setPendingRequests,
  setUsers,
  isMobile,
  toggleSidebar
}: OnboardDetailsPageProps) {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  // Retrieve matching application request from global array
  const request = pendingRequests.find((r) => r.id === id);

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

  // Handle Client Approval Flow
  const handleApprove = () => {
    // 1. Create fully provisioned CLIENT model
    const newUser: MockUser = {
      id: `usr_${crypto.randomUUID()}`,
      name: request.name,
      email: request.email,
      role: "CLIENT",
      isActive: true,
      authorizedApps: request.requestedApps,
      createdAt: new Date().toISOString()
    };

    // 2. Append directly to global directory state
    setUsers((prev) => [newUser, ...prev]);

    // 3. Remove from pending onboarding approvals queue
    setPendingRequests((prev) => prev.filter((r) => r.id !== request.id));

    // 4. Trigger Toast feedback and navigate back
    toast.success(`Client account approved and authorized.`);
    navigate("/onboard");
  };

  // Handle Client Rejection Flow
  const handleDecline = () => {
    // 1. Delete request from queue
    setPendingRequests((prev) => prev.filter((r) => r.id !== request.id));

    // 2. Trigger feedback toast and navigate back
    toast.info(`Registration request for ${request.name} declined.`);
    navigate("/onboard");
  };

  const getFormatAppName = (appId: string) => {
    return appId.charAt(0).toUpperCase() + appId.slice(1).toLowerCase();
  };

  return (
    <div className="flex-1 w-full p-4 md:p-8 mx-auto max-w-[1240px] flex flex-col gap-6 animate-in fade-in duration-300">
      
      {/* Navigation Header */}
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

      {/* Main Layout Grid */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 items-start">
        
        {/* Left Columns (Application details) */}
        <div className="lg:col-span-2 space-y-6">
          
          {/* Metadata */}
          <div className="bg-white border border-[#e5e5e5] rounded-none shadow-[4px_4px_0px_0px_rgba(0,0,0,0.05)] overflow-hidden">
            <div className="px-5 py-4 border-b border-[#f4f4f5] bg-[#fafafa]/50 flex items-center gap-2">
              <Database size={16} className="text-[#a1a1aa]" />
              <h2 className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">Application Metadata</h2>
            </div>
            
            <div className="p-6 grid grid-cols-1 md:grid-cols-2 gap-4">
              <div className="space-y-1">
                <span className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block">Date Submitted</span>
                <span className="text-[13px] font-mono text-[#09090b]">{new Date(request.createdAt).toLocaleString()}</span>
              </div>
              <div className="space-y-1">
                <span className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block">Requested Access Role</span>
                <span className="text-[13px] font-mono text-[#09090b]">CLIENT (Standard Client Portal Access)</span>
              </div>
            </div>
          </div>

          {/* Requested Apps List */}
          <div className="bg-white border border-[#e5e5e5] rounded-none shadow-[4px_4px_0px_0px_rgba(0,0,0,0.05)] overflow-hidden">
            <div className="px-5 py-4 border-b border-[#f4f4f5] bg-[#fafafa]/50">
              <h2 className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">Requested App Access Details</h2>
            </div>

            <div className="p-6 space-y-4">
              <p className="text-[13px] text-[#71717a] leading-relaxed">
                If approved, the client profile will be generated with credentials automatically entitled to these selected modules:
              </p>
              
              <div className="flex flex-wrap gap-2">
                {request.requestedApps.map((app) => (
                  <span key={app} className="px-3 py-1.5 bg-zinc-50 border border-zinc-200 text-[#52525b] text-[11px] font-bold uppercase tracking-wider font-mono">
                    {getFormatAppName(app)}
                  </span>
                ))}
              </div>
            </div>
          </div>

        </div>

        {/* Right Column (Administrative Approval actions) */}
        <div className="lg:col-span-1 space-y-6">
          
          <div className="bg-white border border-[#e5e5e5] rounded-none shadow-[4px_4px_0px_0px_rgba(0,0,0,0.05)] overflow-hidden">
            <div className="px-5 py-4 border-b border-[#f4f4f5] bg-[#fafafa]/50">
              <h2 className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">Review Decision</h2>
            </div>
            
            <div className="p-6 space-y-5">
              <div className="p-4 border border-[#e5e5e5] bg-[#fafafa]/50 space-y-2">
                <p className="text-[12px] text-[#09090b] font-semibold">Cognitive Provisioning</p>
                <p className="text-[11px] text-[#71717a] leading-relaxed">
                  Approving this request will securely hash password credentials, create a master CRM Profile, and notify the client at <code className="font-mono text-[#09090b]">{request.email}</code>.
                </p>
              </div>

              {/* Symmetrical double action button stack */}
              <div className="space-y-3 pt-2">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a] block mb-1">Queue Action</label>
                
                {/* Approve Access */}
                <button 
                  type="button"
                  onClick={handleApprove}
                  className="w-full h-10 border border-emerald-200 bg-emerald-50 text-emerald-700 hover:bg-emerald-100 text-[11px] font-bold uppercase tracking-widest rounded-none transition-colors flex items-center justify-center gap-2 focus:outline-none"
                >
                  <Check size={13} />
                  Approve Application
                </button>

                {/* Decline Access */}
                <button 
                  type="button"
                  onClick={handleDecline}
                  className="w-full h-10 border border-rose-200 bg-rose-50 text-rose-600 hover:bg-rose-100 text-[11px] font-bold uppercase tracking-widest rounded-none transition-colors flex items-center justify-center gap-2 focus:outline-none"
                >
                  <X size={13} />
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
