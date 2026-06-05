import { useState } from "react";
import { Key, Shield, Laptop, Smartphone, LogOut, Trash2, CheckCircle2 } from "lucide-react";
import { toast } from "sonner";
import { cn } from "../lib/utils";

type Session = { id: string; device: string; browser: string; location: string; lastActive: string; isCurrentDevice: boolean; type: "desktop" | "mobile"; };
const initialSessions: Session[] = [
  { id: "ses_1", device: "MacBook Pro", browser: "Chrome", location: "Kuala Lumpur, MY", lastActive: "Active now", isCurrentDevice: true, type: "desktop" },
  { id: "ses_2", device: "iPhone 14 Pro", browser: "Safari", location: "Subang Jaya, MY", lastActive: "2 hours ago", isCurrentDevice: false, type: "mobile" },
  { id: "ses_3", device: "Windows Desktop", browser: "Edge", location: "Singapore, SG", lastActive: "3 days ago", isCurrentDevice: false, type: "desktop" },
];

export default function Security() {
  const [sessions, setSessions] = useState<Session[]>(initialSessions);
  const [is2FAEnabled, setIs2FAEnabled] = useState(false);

  const handleRevoke = (id: string) => { setSessions(prev => prev.filter(s => s.id !== id)); toast.info("Session revoked."); };
  const handleRevokeAll = () => { setSessions(prev => prev.filter(s => s.isCurrentDevice)); toast.success("All other sessions revoked."); };
  const handlePasswordReset = (e: React.FormEvent) => { e.preventDefault(); toast.success("Password updated securely."); };

  return (
    <div className="flex-1 w-full p-4 md:p-8 mx-auto max-w-[800px] flex flex-col gap-6 animate-in fade-in duration-300">
      
      <header className="flex flex-col pb-2 border-b border-[#e5e5e5]">
        <h1 className="text-[20px] font-semibold tracking-tight text-[#09090b]">Security & Access</h1>
        <p className="text-[13px] text-[#71717a] mt-1 font-mono uppercase tracking-wider">Manage your password and sessions.</p>
      </header>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
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

        <section className="bg-white border border-[#e5e5e5] rounded-none shadow-[4px_4px_0px_0px_rgba(0,0,0,0.05)] overflow-hidden flex flex-col">
          <div className="px-5 py-4 border-b border-[#f4f4f5] bg-[#fafafa]/50 flex items-center gap-2">
            <Shield size={16} className="text-[#a1a1aa]" />
            <h2 className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">Two-Factor Auth</h2>
          </div>
          <div className="p-5 flex-1 flex flex-col">
            <p className="text-[12px] text-[#71717a] font-mono leading-relaxed mb-6">
              Add an extra layer of security. You'll need both your password and an auth code to log in.
            </p>
            <div className="flex items-center justify-between p-4 border border-[#e5e5e5] rounded-none bg-[#fafafa]">
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
      </div>

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
