import { useState } from "react";
import { Key, Shield, Laptop, Smartphone, LogOut, Trash2, CheckCircle2, User } from "lucide-react";
import { toast } from "sonner";
import { cn } from "../lib/utils";

// Mock SVG icons for SSO partners
const GoogleIcon = () => (
  <svg viewBox="0 0 24 24" width="16" height="16" xmlns="http://www.w3.org/2000/svg" className="shrink-0">
    <path d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z" fill="#4285F4"/>
    <path d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z" fill="#34A853"/>
    <path d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z" fill="#FBBC05"/>
    <path d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z" fill="#EA4335"/>
  </svg>
);

const AppleIcon = () => (
  <svg viewBox="0 0 24 24" width="16" height="16" xmlns="http://www.w3.org/2000/svg" fill="currentColor" className="shrink-0 text-[#09090b]">
    <path d="M12.152 6.896c-.948 0-2.415-1.078-3.96-1.04-2.04.027-3.91 1.183-4.961 3.014-2.117 3.675-.546 9.103 1.519 12.09 1.013 1.454 2.208 3.126 3.802 3.08 1.498-.046 2.096-.948 3.926-.948 1.815 0 2.37.948 3.926.91 1.637-.038 2.65-1.516 3.633-3.004 1.144-1.688 1.615-3.327 1.637-3.418-.035-.015-3.197-1.222-3.228-4.85-.027-3.04 2.484-4.513 2.597-4.582-1.428-2.09-3.623-2.37-4.408-2.417-1.921-.194-3.818 1.166-4.502 1.166zm1.513-4.71c.84-1.016 1.403-2.428 1.25-3.836-1.185.048-2.65.787-3.513 1.802-.77.876-1.428 2.325-1.25 3.705 1.328.102 2.673-.65 3.513-1.67z" />
  </svg>
);

type Session = { id: string; device: string; browser: string; location: string; lastActive: string; isCurrentDevice: boolean; type: "desktop" | "mobile"; };

export default function Security() {
  const [sessions, setSessions] = useState<Session[]>([
    { id: "ses_1", device: "MacBook Pro", browser: "Chrome", location: "Kuala Lumpur, MY", lastActive: "Active now", isCurrentDevice: true, type: "desktop" },
    { id: "ses_2", device: "iPhone 14 Pro", browser: "Safari", location: "Subang Jaya, MY", lastActive: "2 hours ago", isCurrentDevice: false, type: "mobile" },
    { id: "ses_3", device: "Windows Desktop", browser: "Edge", location: "Singapore, SG", lastActive: "3 days ago", isCurrentDevice: false, type: "desktop" },
  ]);
  
  const [is2FAEnabled, setIs2FAEnabled] = useState(false);
  const [googleLinked, setGoogleLinked] = useState(true);
  const [appleLinked, setAppleLinked] = useState(false);

  const handleRevoke = (id: string) => { 
    setSessions(prev => prev.filter(s => s.id !== id)); 
    toast.info("Session revoked."); 
  };
  
  const handleRevokeAll = () => { 
    setSessions(prev => prev.filter(s => s.isCurrentDevice)); 
    toast.success("All other sessions revoked."); 
  };
  
  const handlePasswordReset = (e: React.FormEvent) => { 
    e.preventDefault(); 
    toast.success("Password updated securely."); 
  };

  const toggleLink = (provider: "google" | "apple", current: boolean) => {
    if (provider === "google") {
      setGoogleLinked(!current);
    } else {
      setAppleLinked(!current);
    }
    toast.success(current ? "SSO connection unlinked" : "SSO linked successfully");
  };

  return (
    <div className="flex-1 w-full p-4 md:p-8 mx-auto max-w-[800px] flex flex-col gap-6 animate-in fade-in duration-300">
      
      <header className="flex flex-col pb-2 border-b border-[#e5e5e5]">
        <h1 className="text-[20px] font-semibold tracking-tight text-[#09090b]">Security & Access</h1>
        <p className="text-[13px] text-[#71717a] mt-1 font-mono uppercase tracking-wider">Manage your password and sessions.</p>
      </header>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
        
        {/* Card 1: Change Password */}
        <section className="bg-white border border-[#e5e5e5] rounded-none shadow-[4px_4px_0px_0px_rgba(0,0,0,0.05)] overflow-hidden flex flex-col">
          <div className="px-5 py-4 border-b border-[#f4f4f5] bg-[#fafafa]/50 flex items-center gap-2">
            <Key size={16} className="text-[#a1a1aa]" />
            <h2 className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">Change Password</h2>
          </div>
          <form onSubmit={handlePasswordReset} className="p-5 space-y-4 flex-1 flex flex-col">
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Current Password</label>
              <input type="password" required className="flex h-9 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm shadow-sm focus:outline-none focus:ring-1 focus:ring-[#09090b]" />
            </div>
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">New Password</label>
              <input type="password" required className="flex h-9 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm shadow-sm focus:outline-none focus:ring-1 focus:ring-[#09090b]" />
            </div>
            <div className="mt-auto pt-4">
              <button type="submit" className="h-9 w-full bg-[#f4f4f5] text-[#09090b] border border-[#e5e5e5] text-[11px] font-bold uppercase tracking-widest rounded-none hover:bg-[#e4e4e7] transition-colors">
                Update Password
              </button>
            </div>
          </form>
        </section>

        {/* Card 2: Two-Factor Auth */}
        <section className="bg-white border border-[#e5e5e5] rounded-none shadow-[4px_4px_0px_0px_rgba(0,0,0,0.05)] overflow-hidden flex flex-col">
          <div className="px-5 py-4 border-b border-[#f4f4f5] bg-[#fafafa]/50 flex items-center gap-2">
            <Shield size={16} className="text-[#a1a1aa]" />
            <h2 className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">Two-Factor Auth</h2>
          </div>
          <div className="p-5 flex-1 flex flex-col">
            <p className="text-[12px] text-[#71717a] font-mono leading-relaxed mb-6">
              Add an extra layer of security. You'll need both your password and an auth code to log in.
            </p>
            <div className="flex items-center justify-between p-4 border border-[#e5e5e5] rounded-none bg-[#fafafa] mt-auto">
              <div className="flex items-center gap-3">
                <div className={cn("flex h-8 w-8 items-center justify-center rounded-none", is2FAEnabled ? "bg-emerald-100 text-emerald-600" : "bg-[#e5e5e5] text-[#a1a1aa]")}>
                  <CheckCircle2 size={16} />
                </div>
                <div>
                  <p className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">{is2FAEnabled ? "2FA Active" : "2FA Disabled"}</p>
                  <p className="text-[10px] font-mono text-[#71717a]">Authenticator App</p>
                </div>
              </div>
              <button onClick={() => { setIs2FAEnabled(!is2FAEnabled); toast(is2FAEnabled ? "2FA Disabled" : "2FA Enabled"); }} className={cn("relative inline-flex h-5 w-9 shrink-0 cursor-pointer items-center rounded-none border-2 border-transparent transition-colors focus:outline-none focus:ring-2 focus:ring-[#09090b]", is2FAEnabled ? "bg-[#09090b]" : "bg-[#e5e5e5]")}>
                <span className={cn("pointer-events-none inline-block h-4 w-4 transform rounded-none bg-white shadow-sm transition duration-200", is2FAEnabled ? "translate-x-4" : "translate-x-0")} />
              </button>
            </div>
          </div>
        </section>

        {/* Card 3: Connected SSO Accounts */}
        <section className="md:col-span-2 bg-white border border-[#e5e5e5] rounded-none shadow-[4px_4px_0px_0px_rgba(0,0,0,0.05)] overflow-hidden">
          <div className="px-5 py-4 border-b border-[#f4f4f5] bg-[#fafafa]/50 flex items-center gap-2">
            <User size={16} className="text-[#a1a1aa]" />
            <h2 className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">Connected SSO Accounts</h2>
          </div>
          
          <div className="p-5 space-y-3">
            {/* Google */}
            <div className="flex items-center justify-between p-3 border border-[#e5e5e5] bg-white hover:bg-[#fafafa]/50 transition-colors">
              <div className="flex items-center gap-3">
                <GoogleIcon />
                <div>
                  <p className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">Google Cloud SSO</p>
                  <p className="text-[10px] font-mono text-[#71717a]">{googleLinked ? "Connected: akmal@gmail.com" : "Not Connected"}</p>
                </div>
              </div>
              <button 
                type="button"
                onClick={() => toggleLink("google", googleLinked)} 
                className={cn("h-7 px-3 text-[9px] font-bold uppercase tracking-widest border transition-colors rounded-none", googleLinked ? "text-rose-600 bg-rose-50/50 border-rose-200 hover:bg-rose-50 hover:text-rose-700" : "text-[#09090b] border-[#e5e5e5] hover:bg-[#f4f4f5]")}
              >
                {googleLinked ? "Disconnect" : "Link Profile"}
              </button>
            </div>

            {/* Apple */}
            <div className="flex items-center justify-between p-3 border border-[#e5e5e5] bg-white hover:bg-[#fafafa]/50 transition-colors">
              <div className="flex items-center gap-3">
                <AppleIcon />
                <div>
                  <p className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">Apple Identity Hub</p>
                  <p className="text-[10px] font-mono text-[#71717a]">{appleLinked ? "Connected" : "Not Connected"}</p>
                </div>
              </div>
              <button 
                type="button"
                onClick={() => toggleLink("apple", appleLinked)} 
                className={cn("h-7 px-3 text-[9px] font-bold uppercase tracking-widest border transition-colors rounded-none", appleLinked ? "text-rose-600 bg-rose-50/50 border-rose-200 hover:bg-rose-50 hover:text-rose-700" : "text-[#09090b] border-[#e5e5e5] hover:bg-[#f4f4f5]")}
              >
                {appleLinked ? "Disconnect" : "Link Profile"}
              </button>
            </div>
          </div>
        </section>

      </div>

      {/* Full-width Active Sessions Tracker */}
      <section className="bg-white border border-[#e5e5e5] rounded-none shadow-[4px_4px_0px_0px_rgba(0,0,0,0.05)] overflow-hidden">
        <div className="px-5 py-4 border-b border-[#f4f4f5] bg-[#fafafa]/50 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <Laptop size={16} className="text-[#a1a1aa]" />
            <h2 className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">Active Sessions</h2>
          </div>
          {sessions.length > 1 && (
            <button onClick={handleRevokeAll} className="text-[10px] font-bold uppercase tracking-widest text-rose-600 hover:text-rose-700 flex items-center gap-1.5"><LogOut size={12} /> Log out other devices</button>
          )}
        </div>
        <div className="divide-y divide-[#f4f4f5]">
          {sessions.map((session) => (
            <div key={session.id} className="p-5 flex items-center justify-between hover:bg-[#fafafa]/50 transition-colors">
              <div className="flex items-center gap-4">
                <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-none bg-[#f4f4f5] text-[#52525b]">
                  {session.type === "desktop" ? <Laptop size={18} /> : <Smartphone size={18} />}
                </div>
                <div>
                  <div className="flex items-center gap-2 mb-0.5">
                    <p className="text-[13px] font-bold uppercase tracking-wide text-[#09090b]">{session.device}</p>
                    {session.isCurrentDevice && <span className="inline-flex items-center px-1.5 py-0.5 rounded-none bg-emerald-50 border border-emerald-200 text-[9px] font-bold uppercase tracking-widest text-emerald-700">Current</span>}
                  </div>
                  <p className="text-[11px] font-mono text-[#71717a]">{session.browser} • {session.location} • {session.lastActive}</p>
                </div>
              </div>
              {!session.isCurrentDevice && (
                <button onClick={() => handleRevoke(session.id)} className="p-2 text-[#a1a1aa] hover:text-rose-600 hover:bg-rose-50 rounded-none transition-colors"><Trash2 size={16} /></button>
              )}
            </div>
          ))}
        </div>
      </section>
      
    </div>
  );
}
