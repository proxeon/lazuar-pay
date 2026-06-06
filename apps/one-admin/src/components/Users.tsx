import { useState } from "react";
import { useNavigate } from "react-router-dom"; // Added for routing navigation
import { Menu, Plus, UserCheck, Settings, SearchX } from "lucide-react";
import CreateUserModal from "./CreateUserModal";

// --- TYPES ---
export type UserRole = "CLIENT";

export interface MockUser {
  id: string;
  name: string;
  email: string;
  role: UserRole;
  isActive: boolean;
  authorizedApps: string[];
  createdAt: string;
}

interface UsersProps {
  users: MockUser[];
  setUsers: React.Dispatch<React.SetStateAction<MockUser[]>>;
  isMobile?: boolean;
  toggleSidebar?: () => void;
}

export default function Users({ users, setUsers, isMobile, toggleSidebar }: UsersProps) {
  const navigate = useNavigate();
  const [showCreateModal, setShowCreateModal] = useState(false);

  // --- HANDOFF LOGIC ---
  const handleUserCreated = (userData: { name: string; email: string; role: UserRole; authorizedApps: string[] }) => {
    const newUser: MockUser = {
      id: `usr_${crypto.randomUUID()}`,
      name: userData.name,
      email: userData.email,
      role: userData.role,
      isActive: true,
      authorizedApps: userData.authorizedApps,
      createdAt: new Date().toISOString(),
    };

    setUsers((prev) => [newUser, ...prev]);
    setShowCreateModal(false);
  };

  const formatDate = (isoString: string) => {
    return new Date(isoString).toLocaleDateString("en-US", {
      year: "numeric",
      month: "short",
      day: "numeric",
    });
  };

  return (
    <div className="flex-1 w-full p-4 md:p-8 mx-auto max-w-[1240px] flex flex-col gap-6 animate-in fade-in duration-300">
      
      {/* Header */}
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
            <h1 className="text-[20px] font-semibold tracking-tight text-[#09090b]">Client Registry</h1>
            <p className="text-[13px] text-[#71717a] mt-0.5">Manually register and manage global client credentials.</p>
          </div>
        </div>

        <div className="flex items-center gap-2">
          <button 
            onClick={() => setShowCreateModal(true)}
            className="flex items-center gap-1.5 bg-[#09090b] text-white text-[13px] font-semibold px-4 h-9 rounded-none shadow-[4px_4px_0px_0px_rgba(0,0,0,0.1)] hover:shadow-none hover:translate-y-[2px] hover:translate-x-[2px] hover:bg-[#27272a] transition-all active:scale-95"
          >
            <Plus size={16} />
            Register Client
          </button>
        </div>
      </header>

      {/* Directory Table */}
      <div className="bg-white border border-[#e5e5e5] rounded-none shadow-[4px_4px_0px_0px_rgba(0,0,0,0.05)] overflow-hidden flex flex-col">
        <div className="px-5 py-4 border-b border-[#f4f4f5] bg-[#fafafa]/50 flex items-center gap-2">
          <UserCheck size={16} className="text-[#a1a1aa]" />
          <h2 className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">Client Accounts</h2>
        </div>

        <div className="overflow-x-auto">
          <table className="w-full text-left text-[13px]">
            <thead>
              <tr className="border-b border-[#e5e5e5] bg-[#fafafa]">
                <th className="h-10 px-5 font-bold uppercase tracking-widest text-[#71717a] text-[10px]">Client</th>
                <th className="h-10 px-5 font-bold uppercase tracking-widest text-[#71717a] text-[10px]">Entitled Apps</th>
                <th className="h-10 px-5 font-bold uppercase tracking-widest text-[#71717a] text-[10px]">Status</th>
                <th className="h-10 px-5 font-bold uppercase tracking-widest text-[#71717a] text-[10px]">Registered</th>
                <th className="h-10 px-5 font-bold uppercase tracking-widest text-[#71717a] text-[10px] text-right">Actions</th>
              </tr>
            </thead>
            <tbody className="divide-y divide-[#f4f4f5]">
              {users.length === 0 ? (
                <tr>
                  <td colSpan={5} className="py-12 text-center">
                    <SearchX className="h-8 w-8 text-[#a1a1aa] mx-auto mb-3" />
                    <p className="text-[13px] font-bold uppercase tracking-widest text-[#09090b]">No clients found</p>
                  </td>
                </tr>
              ) : (
                users.map((user) => (
                  <tr key={user.id} className="hover:bg-[#fafafa]/50 transition-colors group">
                    <td className="p-5 whitespace-nowrap">
                      <p className="font-semibold text-[#09090b] text-[14px]">{user.name}</p>
                      <p className="text-[11px] font-mono text-[#71717a] mt-0.5">{user.email}</p>
                    </td>

                    <td className="p-5">
                      <div className="flex flex-wrap gap-1 max-w-[280px]">
                        {user.authorizedApps.length === 0 ? (
                          <span className="text-[10px] text-[#a1a1aa] font-mono uppercase tracking-wider">None</span>
                        ) : user.authorizedApps.length === 8 ? (
                          <span className="px-1.5 py-0.5 bg-blue-50 border border-blue-100 text-blue-700 text-[9px] font-bold uppercase tracking-widest font-mono">
                            All Access
                          </span>
                        ) : (
                          user.authorizedApps.map(app => (
                            <span key={app} className="px-1.5 py-0.5 bg-zinc-50 border border-zinc-200 text-[#52525b] text-[9px] font-bold uppercase tracking-wider font-mono">
                              {app}
                            </span>
                          ))
                        )}
                      </div>
                    </td>

                    <td className="p-5 whitespace-nowrap">
                      {user.isActive ? (
                        <span className="inline-flex items-center px-2 py-0.5 rounded-none border border-emerald-200 bg-emerald-50 text-[9px] font-bold uppercase tracking-widest text-emerald-700">
                          Active
                        </span>
                      ) : (
                        <span className="inline-flex items-center px-2 py-0.5 rounded-none border border-rose-200 bg-rose-50 text-[9px] font-bold uppercase tracking-widest text-rose-700">
                          Suspended
                        </span>
                      )}
                    </td>

                    <td className="p-5 whitespace-nowrap text-[#52525b] font-mono text-[12px]">
                      {formatDate(user.createdAt)}
                    </td>

                    {/* Navigation Trigger to new page instead of slideout overlay */}
                    <td className="p-5 whitespace-nowrap text-right">
                      <button 
                        onClick={() => navigate(`/users/${user.id}`)}
                        className="inline-flex items-center gap-1.5 h-8 px-3 rounded-none border border-[#e5e5e5] bg-white text-[#09090b] text-[11px] font-bold uppercase tracking-widest hover:bg-[#f4f4f5] hover:border-[#a1a1aa] transition-colors focus:outline-none"
                      >
                        <Settings size={13} />
                        Manage
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {showCreateModal && (
        <CreateUserModal 
          onClose={() => setShowCreateModal(false)}
          onSuccess={handleUserCreated}
        />
      )}
    </div>
  );
}
