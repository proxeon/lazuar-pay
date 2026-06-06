import React, { useState } from "react";
import { useOutletContext } from "react-router-dom";
import { Loader2, UploadCloud, User, Globe, Info } from "lucide-react";
import { toast } from "sonner";
import type { AuthUser } from "../lib/api-client";

export default function Profile() {
  const { user } = useOutletContext<{ user: AuthUser }>();

  const [isSaving, setIsSaving] = useState(false);
  const [displayName, setDisplayName] = useState(user.name);
  
  // Localization Form State
  const [timezone, setTimezone] = useState("Asia/Kuala_Lumpur");
  const [language, setLanguage] = useState("en-US");

  const handleSaveProfile = (e: React.FormEvent) => {
    e.preventDefault();
    setIsSaving(true);
    setTimeout(() => {
      setIsSaving(false);
      toast.success("Profile updated successfully");
    }, 800);
  };

  return (
    <div className="flex-1 w-full p-4 md:p-8 mx-auto max-w-[800px] flex flex-col gap-6 animate-in fade-in duration-300">
      <header className="flex flex-col pb-2 border-b border-[#e5e5e5]">
        <h1 className="text-[20px] font-semibold tracking-tight text-[#09090b]">Unified Profile</h1>
        <p className="text-[13px] text-[#71717a] mt-1 font-mono uppercase tracking-wider">Manage your master identity.</p>
      </header>

      <form onSubmit={handleSaveProfile} className="space-y-6">
        <section className="bg-white border border-[#e5e5e5] rounded-none shadow-[4px_4px_0px_0px_rgba(0,0,0,0.05)] overflow-hidden">
          <div className="px-5 py-4 border-b border-[#f4f4f5] bg-[#fafafa]/50 flex items-center gap-2">
            <User size={16} className="text-[#a1a1aa]" />
            <h2 className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">Basic Information</h2>
          </div>
          
          <div className="p-6 space-y-6">
            <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Global Display Name</label>
                <input 
                  type="text" 
                  value={displayName} 
                  onChange={(e) => setDisplayName(e.target.value)} 
                  className="flex h-10 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]" 
                />
              </div>
            </div>

            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Master Email</label>
              <input 
                type="email" 
                disabled 
                value={user.email} 
                className="flex h-10 w-full rounded-none border border-[#e5e5e5] bg-[#fafafa] px-3 py-1 text-sm text-[#71717a] cursor-not-allowed outline-none" 
              />
            </div>
            
            <div className="flex items-start gap-2.5 p-3 bg-blue-50/50 border border-blue-200 rounded-none">
              <Info size={16} className="text-blue-600 shrink-0 mt-0.5" />
              <p className="text-[12px] text-blue-800 leading-relaxed font-mono">
                <strong className="font-bold uppercase tracking-widest">Ecosystem Sync:</strong> Profile updates processed here will automatically propagate across all authorized tenant networks.
              </p>
            </div>
          </div>
        </section>

        {/* --- CARD 2: CONTACT & LOCALIZATION --- */}
        <section className="bg-white border border-[#e5e5e5] rounded-none shadow-[4px_4px_0px_0px_rgba(0,0,0,0.05)] overflow-hidden">
          <div className="px-5 py-4 border-b border-[#f4f4f5] bg-[#fafafa]/50 flex items-center gap-2">
            <Globe size={16} className="text-[#a1a1aa]" />
            <h2 className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">Localization</h2>
          </div>
          <div className="p-6 grid grid-cols-1 md:grid-cols-2 gap-5">
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Timezone</label>
              <select value={timezone} onChange={(e) => setTimezone(e.target.value)} className="flex h-10 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm shadow-sm focus:outline-none focus:ring-1 focus:ring-[#09090b] cursor-pointer">
                <option value="Asia/Kuala_Lumpur">Asia/Kuala Lumpur (GMT+8)</option>
              </select>
            </div>
          </div>
        </section>

        <div className="flex justify-end pt-2 pb-10">
          <button type="submit" disabled={isSaving} className="h-10 px-8 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest rounded-none flex items-center justify-center gap-2 hover:bg-[#27272a] disabled:opacity-50 transition-all shadow-[4px_4px_0px_0px_rgba(0,0,0,0.1)] hover:shadow-none hover:translate-y-[2px] hover:translate-x-[2px] active:scale-95">
            {isSaving ? <Loader2 size={16} className="animate-spin" /> : "Save Changes"}
          </button>
        </div>
      </form>
    </div>
  );
}
