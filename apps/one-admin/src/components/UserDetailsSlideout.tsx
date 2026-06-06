import { useState } from "react";
import { X, Key, Shield, Ban, CheckCircle } from "lucide-react";
import { toast } from "sonner";
import type { MockUser } from "./Users";
import { cn } from "../lib/utils";

import ResetPasswordModal from "./ResetPasswordModal";

interface UserDetailsSlideoutProps {
  user: MockUser | null;
  onClose: () => void;
  onUpdateStatus: (status: boolean) => void;
}

export default function UserDetailsSlideout({ 
  user, 
  onClose, 
  onUpdateStatus
}: UserDetailsSlideoutProps) {
  const [showResetModal, setShowResetModal] = useState(false);

  if (!user) return null;

  // Callback executed when the child ResetPasswordModal completes
  const handleResetSuccess = (newPassword: string) => {
    setShowResetModal(false);
    toast.success(`Password for ${user.email} updated successfully in UserAccess storage.`);
  };

  return (
    <div className="fixed inset-0 z-50 overflow-hidden flex justify-end">
      {/* Backdrop */}
      <div 
        className="absolute inset-0 bg-black/10 backdrop-blur-[2px] transition-opacity animate-in fade-in duration-200" 
        onClick={onClose} 
      />
      
      {/* Slide-out Panel */}
      <div className="relative w-full max-w-sm bg-white h-full border-l border-[#e5e5e5] shadow-2xl flex flex-col animate-in slide-in-from-right duration-300">
        
        {/* Header */}
        <div className="flex items-start justify-between p-6 border-b border-[#f4f4f5] shrink-0 bg-[#fafafa]/50">
          <div>
            <h2 className="text-[18px] font-semibold text-[#09090b] leading-tight">
              {user.name}
            </h2>
            <p className="text-[11px] font-mono text-[#71717a] mt-1">{user.email}</p>
          </div>
          <button 
            onClick={onClose} 
            className="text-[#a1a1aa] hover:bg-[#e5e5e5] hover:text-[#09090b] transition-colors p-1.5 rounded-none"
          >
            <X size={16} />
          </button>
        </div>

        {/* Content Panel */}
        <div className="flex-1 overflow-y-auto p-6 space-y-8">
          
          {/* Information Block */}
          <section className="space-y-3">
            <h3 className="text-[10px] font-bold uppercase tracking-widest text-[#a1a1aa] mb-3 border-b border-[#f4f4f5] pb-1">
              Account Metadata
            </h3>
            <div className="flex justify-between items-center text-[12px] font-mono">
              <span className="text-[#71717a]">Client ID</span>
              <span className="text-[#09090b] truncate max-w-[180px]" title={user.id}>{user.id}</span>
            </div>
            <div className="flex justify-between items-center text-[12px] font-mono">
              <span className="text-[#71717a]">Date Registered</span>
              <span className="text-[#09090b]">{new Date(user.createdAt).toLocaleDateString()}</span>
            </div>
            <div className="flex justify-between items-center text-[12px] font-mono">
              <span className="text-[#71717a]">Security Standard</span>
              <span className="text-emerald-700 bg-emerald-50 px-1 font-bold border border-emerald-100 flex items-center gap-1 text-[9px] uppercase tracking-wider">
                <Shield size={10} /> BCrypt (Factor 11)
              </span>
            </div>
          </section>

          {/* Configuration Actions */}
          <section className="space-y-5">
            <h3 className="text-[10px] font-bold uppercase tracking-widest text-[#a1a1aa] mb-3 border-b border-[#f4f4f5] pb-1">
              Account Management
            </h3>

            <div className="p-4 border border-[#e5e5e5] bg-[#fafafa]/50 space-y-2">
              <p className="text-[12px] text-[#09090b] font-semibold">Client Portal Access</p>
              <p className="text-[11px] text-[#71717a] leading-relaxed">
                This account holds standard client credentials and can log in at <code className="font-mono text-[#09090b]">http://localhost:3001</code> to view portal resources.
              </p>
            </div>

            {/* Explicit spacing setup */}
            <div className="pt-2">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a] block mb-3">Credential Security</label>
              
              {/* space-y-3 enforces exactly 12px margin-top on the second button */}
              <div className="space-y-3">
                {/* Reset Password Button */}
                <button 
                  type="button"
                  onClick={() => setShowResetModal(true)}
                  className="w-full h-10 border border-[#e5e5e5] bg-white text-[#09090b] hover:bg-[#f4f4f5] hover:text-[#09090b] text-[11px] font-bold uppercase tracking-widest rounded-none transition-colors flex items-center justify-center gap-2 focus:outline-none"
                >
                  <Key size={13} />
                  Reset Client Password
                </button>

                {/* Status suspension button */}
                <button 
                  type="button"
                  onClick={() => onUpdateStatus(!user.isActive)}
                  className={cn(
                    "w-full h-10 border text-[11px] font-bold uppercase tracking-widest transition-colors rounded-none flex items-center justify-center gap-2 focus:outline-none",
                    user.isActive 
                      ? "bg-rose-50 text-rose-600 border-rose-200 hover:bg-rose-100 hover:text-rose-700"
                      : "bg-emerald-50 text-emerald-600 border-emerald-200 hover:bg-emerald-100 hover:text-emerald-700"
                  )}
                >
                  {user.isActive ? <Ban size={13} /> : <CheckCircle size={13} />}
                  {user.isActive ? "Suspend Client Access" : "Activate Client Access"}
                </button>
              </div>
            </div>
          </section>

        </div>
      </div>

      {/* --- PASSWORD RESET OVERLAY MODAL --- */}
      {showResetModal && (
        <ResetPasswordModal 
          userEmail={user.email}
          onClose={() => setShowResetModal(false)}
          onSuccess={handleResetSuccess}
        />
      )}
    </div>
  );
}
