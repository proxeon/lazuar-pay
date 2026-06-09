import { useState, useEffect } from "react";
import { X, Loader2, Key } from "lucide-react";
import { toast } from "sonner";
import type { UserRole } from "./Users";
import { cn } from "../lib/utils";

interface CreateUserModalProps {
  onClose: () => void;
  onSuccess: (userData: { name: string; email: string; role: UserRole; authorizedApps: string[] }) => void;
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

const ALL_APP_IDS = ["FUNNEL", "EVENT", "CONSULT", "VAULT", "ACADEMY", "COMMUNITY", "BROADCAST", "AFFILIATE"];

export default function CreateUserModal({ onClose, onSuccess }: CreateUserModalProps) {
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [selectedApps, setSelectedApps] = useState<string[]>([]);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleGeneratePassword = () => {
    const chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()";
    let generated = "";
    for (let i = 12; i > 0; i--) {
      generated += chars.charAt(Math.floor(Math.random() * chars.length));
    }
    setPassword(generated);
    setConfirmPassword(generated);
    toast.success("Secure password generated.");
  };

  const handleAppToggle = (appId: string) => {
    setSelectedApps(prev => 
      prev.includes(appId) ? prev.filter(id => id !== appId) : [...prev, appId]
    );
  };

  const handleSelectAllToggle = () => {
    if (selectedApps.length === ALL_APP_IDS.length) {
      setSelectedApps([]);
    } else {
      setSelectedApps(ALL_APP_IDS);
    }
  };

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim() || !email.trim() || !password.trim() || !confirmPassword.trim()) return;

    if (password !== confirmPassword) {
      toast.error("Passwords do not match.");
      return;
    }

    if (password.length < 6) {
      toast.error("Password must be at least 6 characters.");
      return;
    }

    setIsSubmitting(true);

    setTimeout(() => {
      setIsSubmitting(false);
      onSuccess({
        name: name.trim(),
        email: email.trim().toLowerCase(),
        role: "CLIENT",
        authorizedApps: selectedApps
      });
    }, 500);
  };

  const passwordsMatch = password && password === confirmPassword;
  const hasAllAccess = selectedApps.length === ALL_APP_IDS.length;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      {/* Backdrop */}
      <div className="absolute inset-0 bg-black/20 backdrop-blur-sm" onClick={onClose} />
      
      {/* Modal Container */}
      <div className="relative bg-white border border-[#e5e5e5] rounded-none shadow-[8px_8px_0px_0px_rgba(0,0,0,0.1)] w-full max-w-lg max-h-[90vh] flex flex-col animate-in fade-in zoom-in-95 duration-200">
        
        {/* Header */}
        <div className="flex items-center justify-between p-5 border-b border-[#e5e5e5] shrink-0">
          <div>
            <h3 className="text-[14px] font-bold uppercase tracking-wider text-[#09090b]">Register Client</h3>
            <p className="text-[11px] text-[#71717a] mt-1">Configure client credentials and app permissions.</p>
          </div>
          <button onClick={onClose} className="text-[#a1a1aa] hover:bg-[#f4f4f5] hover:text-[#09090b] transition-colors p-1"><X size={16} /></button>
        </div>

        {/* Form Body */}
        <form onSubmit={handleSubmit} className="p-5 space-y-5 overflow-y-auto flex-1">
          
          {/* Section: Master Credentials */}
          <div className="space-y-3.5">
            <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#a1a1aa] border-b border-[#f4f4f5] pb-1">Master Credentials</h4>
            
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div className="space-y-1">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Client Full Name</label>
                <input type="text" required value={name} onChange={e => setName(e.target.value)} placeholder="Ahmad Firdaus" className="flex h-10 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]" />
              </div>
              <div className="space-y-1">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Email Address</label>
                <input type="email" required value={email} onChange={e => setEmail(e.target.value)} placeholder="ahmad@gmail.com" className="flex h-10 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]" />
              </div>
            </div>

            {/* Symmetrically Aligned Passwords Grid */}
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              <div className="space-y-1">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Master Password</label>
                <input type="text" required value={password} onChange={e => setPassword(e.target.value)} placeholder="••••••••••••" className="flex h-10 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm font-mono shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]" />
              </div>

              <div className="space-y-1">
                <div className="flex justify-between items-center h-4">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Confirm Password</label>
                  {passwordsMatch && <span className="text-emerald-600 flex items-center gap-0.5 text-[10px] font-bold uppercase tracking-wider">✓ Match</span>}
                </div>
                <input type="password" required value={confirmPassword} onChange={e => setConfirmPassword(e.target.value)} placeholder="••••••••••••" className="flex h-10 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm font-mono shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]" />
              </div>
            </div>

            {/* Generate Trigger - Positioned completely below the inputs */}
            <div className="flex justify-start pt-1">
              <button 
                type="button" 
                onClick={handleGeneratePassword} 
                className="text-[10px] font-bold uppercase tracking-widest text-blue-600 hover:text-blue-800 transition-colors flex items-center gap-1.5 focus:outline-none outline-none select-none"
              >
                <Key size={11} /> Generate Secure Password
              </button>
            </div>
          </div>

          {/* Section: App Entitlements */}
          <div className="space-y-4 pt-1">
            <div className="flex items-center justify-between border-b border-[#f4f4f5] pb-1">
              <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#a1a1aa]">Ecosystem App Access</h4>
              <button 
                type="button" 
                onClick={handleSelectAllToggle}
                className="text-[10px] font-bold uppercase tracking-widest text-blue-600 hover:text-blue-800 transition-colors focus:outline-none select-none"
              >
                {hasAllAccess ? "Deselect All" : "Access All Apps"}
              </button>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-3 gap-5">
              {APP_CATEGORIES.map(group => (
                <div key={group.category} className="space-y-2.5">
                  <span className="text-[9px] font-bold tracking-widest text-[#a1a1aa] uppercase">{group.category}</span>
                  
                  <div className="space-y-2">
                    {group.apps.map(app => {
                      const isChecked = selectedApps.includes(app.id);
                      return (
                        <div 
                          key={app.id} 
                          onClick={() => handleAppToggle(app.id)}
                          role="button"
                          tabIndex={0}
                          className={cn(
                            "flex items-center justify-between p-2.5 border cursor-pointer select-none transition-colors rounded-none outline-none",
                            isChecked 
                              ? "bg-emerald-50/60 border-emerald-300 text-emerald-950" 
                              : "border-[#e5e5e5] bg-white text-[#71717a] hover:bg-[#fafafa]"
                          )}
                        >
                          <span className="text-[12px] font-medium leading-none">{app.name}</span>
                          <input 
                            type="checkbox" 
                            readOnly 
                            checked={isChecked}
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

          {/* Footer Actions */}
          <div className="flex items-center justify-end gap-3 pt-5 border-t border-[#f4f4f5] shrink-0">
            <button type="button" onClick={onClose} className="text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:text-[#09090b] transition-colors px-2 py-1">Cancel</button>
            <button 
              type="submit" 
              disabled={isSubmitting || !name.trim() || !email.trim() || !password.trim() || !confirmPassword.trim()}
              className="h-10 px-8 bg-[#09090b] text-white text-[11px] font-bold tracking-widest uppercase rounded-none hover:bg-[#27272a] disabled:opacity-50 transition-all flex items-center justify-center gap-2 whitespace-nowrap shadow-[4px_4px_0px_0px_rgba(0,0,0,0.1)] hover:shadow-none hover:translate-y-[2px] hover:translate-x-[2px] active:scale-95"
            >
              {isSubmitting && <Loader2 size={12} className="animate-spin" />}
              {isSubmitting ? "Registering..." : "Register Client"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
