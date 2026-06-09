import { useNavigate } from "react-router-dom"; // Added for routing navigation
import { Menu, UserCheck, Settings, SearchX } from "lucide-react";

export interface OnboardingRequest {
  id: string;
  name: string;
  email: string;
  requestedApps: string[];
  createdAt: string;
}

interface OnboardProps {
  pendingRequests: OnboardingRequest[];
  isMobile?: boolean;
  toggleSidebar?: () => void;
}

export default function Onboard({ pendingRequests, isMobile, toggleSidebar }: OnboardProps) {
  const navigate = useNavigate();

  const formatDate = (isoString: string) => {
    return new Date(isoString).toLocaleDateString("en-US", {
      year: "numeric",
      month: "short",
      day: "numeric",
    });
  };

  const getFormatAppName = (appId: string) => {
    return appId.charAt(0).toUpperCase() + appId.slice(1).toLowerCase();
  };

  return (
    <div className="flex-1 w-full p-4 md:p-8 mx-auto max-w-[1240px] flex flex-col gap-6 animate-in fade-in duration-300">
      
      {/* Page Header */}
      <header className="flex flex-col md:flex-row md:items-center justify-between pb-2 gap-4">
        <div className="flex items-center gap-3">
          {isMobile && (
            <button 
              onClick={toggleSidebar}
              className="p-1.5 -ml-1.5 rounded-md text-[#71717a] hover:bg-[#e5e5e5] hover:text-[#09090b] focus:outline-none transition-colors"
            >
              <Menu size={20} />
            </button>
          )}
          <div>
            <h1 className="text-[20px] font-semibold tracking-tight text-[#09090b]">Onboarding Queue</h1>
            <p className="text-[13px] text-[#71717a] mt-0.5">Review and approve pending portal registration requests.</p>
          </div>
        </div>
      </header>

      {/* Directory Queue Card */}
      <div className="bg-white border border-[#e5e5e5] rounded-none shadow-[4px_4px_0px_0px_rgba(0,0,0,0.05)] overflow-hidden flex flex-col">
        <div className="px-5 py-4 border-b border-[#f4f4f5] bg-[#fafafa]/50 flex items-center gap-2">
          <UserCheck size={16} className="text-[#a1a1aa]" />
          <h2 className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">Pending Approvals</h2>
        </div>

        {pendingRequests.length === 0 ? (
          <div className="py-20 text-center flex flex-col items-center justify-center">
            <div className="flex h-12 w-12 items-center justify-center bg-emerald-50 border border-emerald-100 text-emerald-600 rounded-none mb-4">
              <UserCheck size={20} />
            </div>
            <h3 className="text-[14px] font-bold uppercase tracking-widest text-[#09090b]">Queue Clean</h3>
            <p className="text-[12px] text-[#71717a] mt-1 max-w-xs">No pending registration approvals in queue.</p>
          </div>
        ) : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-[13px]">
              <thead>
                <tr className="border-b border-[#e5e5e5] bg-[#fafafa]">
                  <th className="h-10 px-5 font-bold uppercase tracking-widest text-[#71717a] text-[10px]">Applicant</th>
                  <th className="h-10 px-5 font-bold uppercase tracking-widest text-[#71717a] text-[10px]">Requested Apps</th>
                  <th className="h-10 px-5 font-bold uppercase tracking-widest text-[#71717a] text-[10px]">Requested Date</th>
                  <th className="h-10 px-5 font-bold uppercase tracking-widest text-[#71717a] text-[10px] text-right">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-[#f4f4f5]">
                {pendingRequests.map((request) => (
                  <tr key={request.id} className="hover:bg-[#fafafa]/50 transition-colors group">
                    <td className="p-5 whitespace-nowrap">
                      <p className="font-semibold text-[#09090b] text-[14px]">{request.name}</p>
                      <p className="text-[11px] font-mono text-[#71717a] mt-0.5">{request.email}</p>
                    </td>

                    <td className="p-5">
                      <div className="flex flex-wrap gap-1 max-w-[280px]">
                        {request.requestedApps.map(app => (
                          <span key={app} className="px-1.5 py-0.5 bg-zinc-50 border border-zinc-200 text-[#52525b] text-[9px] font-bold uppercase tracking-wider font-mono">
                            {getFormatAppName(app)}
                          </span>
                        ))}
                      </div>
                    </td>

                    <td className="p-5 whitespace-nowrap text-[#52525b] font-mono text-[12px]">
                      {formatDate(request.createdAt)}
                    </td>

                    {/* Standardized single route Manage trigger */}
                    <td className="p-5 whitespace-nowrap text-right">
                      <button 
                        onClick={() => navigate(`/onboard/${request.id}`)}
                        className="inline-flex items-center gap-1.5 h-8 px-3 rounded-none border border-[#e5e5e5] bg-white text-[#09090b] text-[11px] font-bold uppercase tracking-widest hover:bg-[#f4f4f5] hover:border-[#a1a1aa] transition-colors focus:outline-none"
                      >
                        <Settings size={13} />
                        Manage
                      </button>
                    </td>

                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

    </div>
  );
}
