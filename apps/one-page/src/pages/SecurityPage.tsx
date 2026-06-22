import { useState } from "react";
import { useMutation } from "@tanstack/react-query";
import { toast } from "sonner";
import { Loader2, ShieldCheck } from "lucide-react";
import { client } from "../lib/api-client";

export default function SecurityPage() {
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");

  const updatePasswordMutation = useMutation({
    mutationFn: async () => {
      const { error } = await client.PUT("/one/me/security/password", {
        body: { current_password: currentPassword, new_password: newPassword }
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("Password updated successfully.");
      setCurrentPassword("");
      setNewPassword("");
      setConfirmPassword("");
    },
    onError: (err: any) => toast.error("Failed to update password", { description: err.message })
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (newPassword !== confirmPassword) {
      toast.error("New passwords do not match.");
      return;
    }
    if (newPassword.length < 8) {
      toast.error("Password must be at least 8 characters.");
      return;
    }
    updatePasswordMutation.mutate();
  };

  return (
    <div className="space-y-8 animate-in fade-in duration-300">
      <div>
        <h1 className="text-2xl font-bold text-[#09090b] tracking-tight">Security Settings</h1>
        <p className="text-[13px] text-[#71717a] mt-1">Manage your account credentials.</p>
      </div>

      <div className="bg-white border border-[#e5e5e5] max-w-xl">
        <div className="p-5 border-b border-[#f4f4f5] bg-[#fafafa]/50">
          <h2 className="text-[13px] font-bold uppercase tracking-widest text-[#09090b]">Change Password</h2>
        </div>
        
        <form onSubmit={handleSubmit}>
          <div className="p-6 space-y-6">
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Current Password *</label>
              <input 
                type="password" 
                required
                value={currentPassword} 
                onChange={e => setCurrentPassword(e.target.value)}
                disabled={updatePasswordMutation.isPending}
                className="w-full h-10 px-3 text-[13px] border border-[#e5e5e5] bg-white focus:outline-none focus:border-[#09090b] font-mono"
              />
            </div>

            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">New Password *</label>
              <input 
                type="password" 
                required
                value={newPassword} 
                onChange={e => setNewPassword(e.target.value)}
                disabled={updatePasswordMutation.isPending}
                className="w-full h-10 px-3 text-[13px] border border-[#e5e5e5] bg-white focus:outline-none focus:border-[#09090b] font-mono"
              />
            </div>

            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Confirm New Password *</label>
              <input 
                type="password" 
                required
                value={confirmPassword} 
                onChange={e => setConfirmPassword(e.target.value)}
                disabled={updatePasswordMutation.isPending}
                className="w-full h-10 px-3 text-[13px] border border-[#e5e5e5] bg-white focus:outline-none focus:border-[#09090b] font-mono"
              />
            </div>
          </div>

          <div className="p-5 border-t border-[#e5e5e5] bg-[#fafafa] flex justify-end">
            <button 
              type="submit" 
              disabled={updatePasswordMutation.isPending || !currentPassword || !newPassword || !confirmPassword}
              className="h-9 px-6 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest hover:bg-[#27272a] transition-colors disabled:opacity-50 flex items-center gap-2"
            >
              {updatePasswordMutation.isPending ? <Loader2 size={14} className="animate-spin" /> : <ShieldCheck size={14} />} 
              Update Security
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
