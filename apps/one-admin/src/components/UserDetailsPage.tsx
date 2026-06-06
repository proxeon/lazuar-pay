import { useState } from "react";
import { useParams, Link } from "react-router-dom";
import { ArrowLeft, Menu, Key, Ban, CheckCircle, Database } from "lucide-react";
import { toast } from "sonner";
import type { MockUser } from "./Users";
import { cn } from "../lib/utils";

import ResetPasswordModal from "./ResetPasswordModal";

interface UserDetailsPageProps {
  users: MockUser[];
  setUsers: React.Dispatch<React.SetStateAction<MockUser[]>>;
  isMobile?: boolean;
  toggleSidebar?: () => void;
}

const APP_CATEGORIES = [
  {
    category: "ACQUISITION",
    apps: [
      { id: "FUNNEL", name: "Funnel" },
      { id: "EVENT", name: "Event" },
      { id: "CONSULT", name: "Consult" }
    ]
  },
  {
    category: "FULFILLMENT",
    apps: [
      { id: "VAULT", name: "Vault" },
      { id: "ACADEMY", name: "Academy" }
    ]
  },
  {
    category: "RETENTION",
    apps: [
      { id: "COMMUNITY", name: "Community" },
      { id: "BROADCAST", name: "Broadcast" },
      { id: "AFFILIATE", name: "Affiliate" }
    ]
  }
];

export default function UserDetailsPage({ users, setUsers, isMobile, toggleSidebar }: UserDetailsPageProps) {
  const { id } = useParams<{ id: string }>();
  const [showResetModal, setShowResetModal] = useState(false);

  // Retrieve the selected client from the global array
  const client = users.find((u) => u.id === id);

  if (!client) {
    return (
      <div className="flex-1 w-full p-8 mx-auto max-w-[1240px] text-center py-20">
        <h2 className="text-lg font-bold text-[#09090b]">Client Not Found</h2>
        <p className="text-sm text-[#71717a] mt-2">The requested client identifier does not exist inside our directory.</p>
        <Link to="/users" className="inline-flex h-10 px-6 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest rounded-none mt-6 items-center">
          ← Return to Directory
        </Link>
      </div>
    );
  }

  // State Change Handlers
  const handleUpdateStatus = () => {
    const updatedStatus = !client.isActive;
    setUsers((prev) =>
      prev.map((u) => (u.id === client.id ? { ...u, isActive: updatedStatus } : u))
    );
    toast.success(updatedStatus ? "Client access reactivated." : "Client access suspended.");
  };

  const handleAppPermissionToggle = (appId: string) => {
    const isCurrentlyEntitled = client.authorizedApps.includes(appId);
    let updatedApps: string[];

    if (isCurrentlyEntitled) {
      updatedApps = client.authorizedApps.filter((id) => id !== appId);
    } else {
      updatedApps = [...client.authorizedApps, appId];
    }

    setUsers((prev) =>
      prev.map((u) => (u.id === client.id ? { ...u, authorizedApps: updatedApps } : u))
    );
    toast.success("Client app permissions updated successfully.");
  };

  const handleResetSuccess = (newPassword: string) => {
    setShowResetModal(false);
    toast.success(`Password for ${client.email} updated successfully in UserAccess storage.`);
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
            <Link to="/users" className="inline-flex items-center gap-1.5 text-[#71717a] hover:text-[#09090b] font-bold uppercase tracking-widest transition-colors text-[11px] select-none">
              <ArrowLeft size={14} /> Back to Directory
            </Link>
          </div>
        </div>

        <div className="flex flex-col md:flex-row md:items-end justify-between gap-4 mt-2">
          <div>
            <h1 className="text-[24px] font-semibold text-[#09090b] leading-tight">
              {client.name}
            </h1>
            <p className="text-[12px] font-mono text-[#71717a] mt-1">{client.email}</p>
          </div>

          <div className="flex items-center gap-3">
            <span className="text-[11px] font-mono text-[#71717a] bg-white px-2 py-1 border border-[#e5e5e5]">
              ID: {client.id}
            </span>
            {client.isActive ? (
              <span className="inline-flex items-center px-2 py-1 rounded-none border border-emerald-200 bg-emerald-50 text-[10px] font-bold uppercase tracking-widest text-emerald-700">
                Active Access
              </span>
            ) : (
              <span className="inline-flex items-center px-2 py-1 rounded-none border border-rose-200 bg-rose-50 text-[10px] font-bold uppercase tracking-widest text-rose-700">
                Access Suspended
              </span>
            )}
          </div>
        </div>
      </header>

      {/* Main Column Grid Layout */}
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 items-start">
        
        {/* Left Columns (Account Details & App Entitlements) */}
        <div className="lg:col-span-2 space-y-6">
          
          {/* Metadata Block */}
          <div className="bg-white border border-[#e5e5e5] rounded-none shadow-[4px_4px_0px_0px_rgba(0,0,0,0.05)] overflow-hidden">
            <div className="px-5 py-4 border-b border-[#f4f4f5] bg-[#fafafa]/50 flex items-center gap-2">
              <Database size={16} className="text-[#a1a1aa]" />
              <h2 className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">Account Configuration</h2>
            </div>
            
            <div className="p-6 grid grid-cols-1 md:grid-cols-2 gap-4">
              <div className="space-y-1">
                <span className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block">Date Registered</span>
                <span className="text-[13px] font-mono text-[#09090b]">{new Date(client.createdAt).toLocaleString()}</span>
              </div>
              <div className="space-y-1">
                <span className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block">Access Level</span>
                <span className="text-[13px] font-mono text-[#09090b]">STANDARD_CLIENT (CLIENT)</span>
              </div>
            </div>
          </div>

          {/* Entitlements Selection Grid */}
          <div className="bg-white border border-[#e5e5e5] rounded-none shadow-[4px_4px_0px_0px_rgba(0,0,0,0.05)] overflow-hidden">
            <div className="px-5 py-4 border-b border-[#f4f4f5] bg-[#fafafa]/50 flex items-center justify-between">
              <div className="flex items-center gap-2">
                <Database size={16} className="text-[#a1a1aa]" />
                <h2 className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">Ecosystem Entitlements</h2>
              </div>
              <span className="text-[10px] font-mono text-[#71717a]">{client.authorizedApps.length} of 8 apps provisioned</span>
            </div>

            <div className="p-6 grid grid-cols-1 md:grid-cols-3 gap-6">
              {APP_CATEGORIES.map(categoryGroup => (
                <div key={categoryGroup.category} className="space-y-3">
                  <span className="text-[10px] font-bold tracking-widest text-[#71717a] uppercase border-b border-[#f4f4f5] pb-1 block">
                    {categoryGroup.category}
                  </span>
                  <div className="space-y-2">
                    {categoryGroup.apps.map(app => {
                      const isEntitled = client.authorizedApps.includes(app.id);
                      return (
                        <div 
                          key={app.id} 
                          onClick={() => handleAppPermissionToggle(app.id)}
                          role="button"
                          className={cn(
                            "flex items-center justify-between p-3 border cursor-pointer select-none transition-colors rounded-none outline-none",
                            isEntitled 
                              ? "bg-emerald-50/60 border-emerald-300 text-emerald-950" 
                              : "border-[#e5e5e5] bg-white text-[#71717a] hover:bg-[#fafafa]"
                          )}
                        >
                          <span className="text-[12px] font-medium leading-none">{app.name}</span>
                          <input 
                            type="checkbox" 
                            readOnly 
                            checked={isEntitled}
                            className="h-3.5 w-3.5 border-zinc-300 text-[#09090b] accent-[#09090b] pointer-events-none"
                          />
                        </div>
                      );
                    })}
                  </div>
                </div>
              ))}
            </div>
          </div>

        </div>

        {/* Right Column (Administrative Actions) */}
        <div className="lg:col-span-1 space-y-6">
          
          <div className="bg-white border border-[#e5e5e5] rounded-none shadow-[4px_4px_0px_0px_rgba(0,0,0,0.05)] overflow-hidden">
            <div className="px-5 py-4 border-b border-[#f4f4f5] bg-[#fafafa]/50 flex items-center gap-2">
              <h2 className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">Account Actions</h2>
            </div>
            
            <div className="p-6 space-y-5">
              <div className="p-4 border border-[#e5e5e5] bg-[#fafafa]/50 space-y-2">
                <p className="text-[12px] text-[#09090b] font-semibold">Portal Gateway</p>
                <p className="text-[11px] text-[#71717a] leading-relaxed">
                  This account holds standard client credentials and can log in at <code className="font-mono text-[#09090b]">http://localhost:3001</code> to view portal resources.
                </p>
              </div>

              {/* Action Button stack with consistent spacing and sizing */}
              <div className="space-y-3 pt-2">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a] block mb-1">Credential Security</label>
                
                {/* Reset Password Button */}
                <button 
                  type="button"
                  onClick={() => setShowResetModal(true)}
                  className="w-full h-10 border border-[#e5e5e5] bg-white text-[#09090b] hover:bg-[#f4f4f5] hover:text-[#09090b] text-[11px] font-bold uppercase tracking-widest rounded-none transition-colors flex items-center justify-center gap-2 focus:outline-none"
                >
                  <Key size={13} />
                  Reset Client Password
                </button>

                {/* Suspend button */}
                <button 
                  type="button"
                  onClick={handleUpdateStatus}
                  className={cn(
                    "w-full h-10 border text-[11px] font-bold uppercase tracking-widest transition-colors rounded-none flex items-center justify-center gap-2 focus:outline-none",
                    client.isActive 
                      ? "bg-rose-50 text-rose-600 border-rose-200 hover:bg-rose-100 hover:text-rose-700"
                      : "bg-emerald-50 text-emerald-600 border-emerald-200 hover:bg-emerald-100 hover:text-emerald-700"
                  )}
                >
                  {client.isActive ? <Ban size={13} /> : <CheckCircle size={13} />}
                  {client.isActive ? "Suspend Client Access" : "Activate Client Access"}
                </button>
              </div>
            </div>
          </div>

        </div>
      </div>

      {/* --- PASSWORD RESET OVERLAY MODAL --- */}
      {showResetModal && (
        <ResetPasswordModal 
          userEmail={client.email}
          onClose={() => setShowResetModal(false)}
          onSuccess={handleResetSuccess}
        />
      )}
    </div>
  );
}
