import { useState, useEffect } from "react";
import { Loader2 } from "lucide-react";
import { client } from "../lib/api-client";
import { Toaster, toast } from "sonner";

export default function ProfilePage() {
  const [isLoading, setIsLoading] = useState(true);
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [isVerified, setIsVerified] = useState(true);
  const [isSaving, setIsSaving] = useState(false);

  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");

  useEffect(() => {
    async function loadProfile() {
      const { data, error } = await client.GET("/one/auth/me");
      if (error || !data) {
        window.location.href = "/login";
        return;
      }
      setName(data.name);
      setEmail(data.email);
      setIsVerified(data.is_email_verified);
      setIsLoading(false);
    }
    loadProfile();
  }, []);

  const handleUpdateProfile = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSaving(true);
    const { error } = await client.PUT("/one/me/profile", { body: { name } });
    if (error) toast.error(error.detail || "Failed to update profile.");
    else toast.success("Profile updated successfully.");
    setIsSaving(false);
  };

  const handleUpdatePassword = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSaving(true);
    const { error } = await client.PUT("/one/me/security/password", { 
      body: { current_password: currentPassword, new_password: newPassword } 
    });
    if (error) toast.error(error.detail || "Failed to update password.");
    else {
      toast.success("Password updated successfully.");
      setCurrentPassword("");
      setNewPassword("");
    }
    setIsSaving(false);
  };

  const handleResendVerification = async () => {
    const { error } = await client.POST("/one/auth/resend-verification", { body: { email } });
    if (error) toast.error("Failed to send verification email.");
    else toast.success("Verification email sent.");
  };

  const handleLogout = async () => {
    await client.POST("/one/auth/logout");
    window.location.href = "/login";
  };

  if (isLoading) return <div className="flex h-screen items-center justify-center bg-[#f5f5f5]"><Loader2 className="animate-spin text-[#a1a1aa]" /></div>;

  return (
    <div className="flex h-screen w-full flex-col bg-[#f5f5f5] font-sans overflow-y-auto">
      <div className="w-full max-w-2xl mx-auto py-12 px-4 space-y-6">
        
        <div className="flex items-center justify-between bg-white border border-[#e5e5e5] p-6">
          <div>
            <h1 className="text-xl font-bold text-[#09090b]">Global Profile</h1>
            <p className="text-[13px] text-[#71717a] mt-1">Manage your identity across the Lazuar ecosystem.</p>
          </div>
          <button onClick={handleLogout} className="text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:text-[#09090b]">Log Out</button>
        </div>

        {!isVerified && (
          <div className="bg-amber-50 border border-amber-200 p-4 flex items-center justify-between">
            <span className="text-[12px] text-amber-800 font-medium">Your email address is not verified.</span>
            <button onClick={handleResendVerification} className="text-[11px] font-bold uppercase tracking-widest text-amber-700 hover:underline">Resend Email</button>
          </div>
        )}

        <form onSubmit={handleUpdateProfile} className="bg-white border border-[#e5e5e5] p-6 space-y-4">
          <h2 className="text-[11px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-2">Personal Details</h2>
          <div className="space-y-1.5">
            <label className="text-[11px] font-semibold text-[#09090b]">Email Address (Cannot be changed)</label>
            <input type="email" disabled value={email} className="w-full h-10 border border-[#e5e5e5] bg-[#f4f4f5] px-3 text-[13px] text-[#71717a] focus:outline-none" />
          </div>
          <div className="space-y-1.5">
            <label className="text-[11px] font-semibold text-[#09090b]">Full Name</label>
            <input type="text" required value={name} onChange={e => setName(e.target.value)} className="w-full h-10 border border-[#e5e5e5] px-3 text-[13px] focus:outline-none focus:border-[#09090b]" />
          </div>
          <div className="pt-2 flex justify-end">
            <button type="submit" disabled={isSaving} className="h-9 px-6 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest hover:bg-[#27272a] disabled:opacity-50">Save Details</button>
          </div>
        </form>

        <form onSubmit={handleUpdatePassword} className="bg-white border border-[#e5e5e5] p-6 space-y-4">
          <h2 className="text-[11px] font-bold uppercase tracking-widest text-[#71717a] border-b border-[#f4f4f5] pb-2">Security</h2>
          <div className="space-y-1.5">
            <label className="text-[11px] font-semibold text-[#09090b]">Current Password</label>
            <input type="password" required value={currentPassword} onChange={e => setCurrentPassword(e.target.value)} className="w-full h-10 border border-[#e5e5e5] px-3 text-[13px] focus:outline-none focus:border-[#09090b]" />
          </div>
          <div className="space-y-1.5">
            <label className="text-[11px] font-semibold text-[#09090b]">New Password</label>
            <input type="password" required value={newPassword} onChange={e => setNewPassword(e.target.value)} className="w-full h-10 border border-[#e5e5e5] px-3 text-[13px] focus:outline-none focus:border-[#09090b]" />
          </div>
          <div className="pt-2 flex justify-end">
            <button type="submit" disabled={isSaving} className="h-9 px-6 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest hover:bg-[#27272a] disabled:opacity-50">Change Password</button>
          </div>
        </form>

      </div>
      <Toaster position="top-center" />
    </div>
  );
}
