import React, { useState } from "react";
import { Loader2, UploadCloud, User, Globe, Info } from "lucide-react";
import { toast } from "sonner";

export default function Profile() {
  const [isSaving, setIsSaving] = useState(false);
  
  // Master Identity Form State
  const [firstName, setFirstName] = useState("Akmal");
  const [lastName, setLastName] = useState("Firdaus");
  const [displayName, setDisplayName] = useState("Akmal Firdaus");
  const [email] = useState("akmal@lazuar.com");
  const [phone, setPhone] = useState("+60123456789");
  
  // Localization Form State
  const [timezone, setTimezone] = useState("Asia/Kuala_Lumpur");
  const [language, setLanguage] = useState("en-US");
  const [theme, setTheme] = useState("system");

  const handleSaveProfile = (e: React.FormEvent) => {
    e.preventDefault();
    setIsSaving(true);
    setTimeout(() => {
      setIsSaving(false);
      toast.success("Profile updated successfully", {
        description: "Your global identity has been synced across all nodes.",
      });
    }, 1000);
  };

  return (
    <div className="flex-1 w-full p-4 md:p-8 mx-auto max-w-[800px] flex flex-col gap-6 animate-in fade-in duration-300">
      
      {/* Standardized Header */}
      <header className="flex flex-col pb-2 border-b border-[#e5e5e5]">
        <h1 className="text-[20px] font-semibold tracking-tight text-[#09090b]">Unified Profile</h1>
        <p className="text-[13px] text-[#71717a] mt-1 font-mono uppercase tracking-wider">Manage your master identity.</p>
      </header>

      <form onSubmit={handleSaveProfile} className="space-y-6">
        
        {/* --- CARD 1: BASIC INFORMATION --- */}
        <section className="bg-white border border-[#e5e5e5] rounded-none shadow-[4px_4px_0px_0px_rgba(0,0,0,0.05)] overflow-hidden">
          <div className="px-5 py-4 border-b border-[#f4f4f5] bg-[#fafafa]/50 flex items-center gap-2">
            <User size={16} className="text-[#a1a1aa]" />
            <h2 className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">Basic Information</h2>
          </div>
          
          <div className="p-6 space-y-6">
            <div className="flex items-center gap-5">
              <div className="flex h-16 w-16 shrink-0 items-center justify-center rounded-none bg-[#f4f4f5] border border-[#e5e5e5] text-[20px] font-bold text-[#52525b] shadow-inner">
                {firstName.charAt(0)}{lastName.charAt(0)}
              </div>
              <div className="flex flex-col gap-2">
                <button type="button" className="inline-flex items-center gap-2 px-3 py-1.5 border border-[#e5e5e5] rounded-none text-[11px] font-bold uppercase tracking-widest text-[#09090b] hover:bg-[#f4f4f5] transition-colors shadow-sm w-fit">
                  <UploadCloud size={14} /> Upload new avatar
                </button>
                <p className="text-[11px] text-[#a1a1aa] font-mono">JPEG, PNG. Max size 2MB.</p>
              </div>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">First Name</label>
                <input 
                  type="text" 
                  required 
                  value={firstName} 
                  onChange={(e) => setFirstName(e.target.value)} 
                  className="flex h-10 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]" 
                />
              </div>
              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Last Name</label>
                <input 
                  type="text" 
                  required 
                  value={lastName} 
                  onChange={(e) => setLastName(e.target.value)} 
                  className="flex h-10 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]" 
                />
              </div>
              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Global Display Name</label>
                <input 
                  type="text" 
                  value={displayName} 
                  onChange={(e) => setDisplayName(e.target.value)} 
                  className="flex h-10 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]" 
                />
              </div>
              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">WhatsApp Number</label>
                <input 
                  type="tel" 
                  value={phone} 
                  onChange={(e) => setPhone(e.target.value)} 
                  className="flex h-10 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]" 
                />
              </div>
            </div>

            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Master Email</label>
              <input 
                type="email" 
                disabled 
                value={email} 
                className="flex h-10 w-full rounded-none border border-[#e5e5e5] bg-[#fafafa] px-3 py-1 text-sm text-[#71717a] cursor-not-allowed outline-none" 
              />
              <p className="text-[11px] text-[#a1a1aa] font-mono mt-1">To change your master email, please open an admin security request.</p>
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
            <h2 className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">Contact & Localization</h2>
          </div>
          
          <div className="p-6 grid grid-cols-1 md:grid-cols-3 gap-5">
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Timezone</label>
              <select 
                value={timezone} 
                onChange={(e) => setTimezone(e.target.value)} 
                className="flex h-10 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm shadow-sm focus:outline-none focus:ring-1 focus:ring-[#09090b] cursor-pointer"
              >
                <option value="Asia/Kuala_Lumpur">Asia/Kuala Lumpur (GMT+8)</option>
                <option value="Asia/Singapore">Asia/Singapore (GMT+8)</option>
                <option value="Asia/Jakarta">Asia/Jakarta (GMT+7)</option>
                <option value="UTC">UTC (GMT+0)</option>
              </select>
            </div>

            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">System Language</label>
              <select 
                value={language} 
                onChange={(e) => setLanguage(e.target.value)} 
                className="flex h-10 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm shadow-sm focus:outline-none focus:ring-1 focus:ring-[#09090b] cursor-pointer"
              >
                <option value="en-US">English (US)</option>
                <option value="ms-MY">Bahasa Melayu</option>
                <option value="id-ID">Bahasa Indonesia</option>
              </select>
            </div>

            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Theme Preference</label>
              <select 
                value={theme} 
                onChange={(e) => setTheme(e.target.value)} 
                className="flex h-10 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm shadow-sm focus:outline-none focus:ring-1 focus:ring-[#09090b] cursor-pointer"
              >
                <option value="system">System Preference</option>
                <option value="light">Light Mode</option>
                <option value="dark">Dark Mode</option>
              </select>
            </div>
          </div>
        </section>

        {/* Standardized Brutalist Button */}
        <div className="flex justify-end pt-2">
          <button 
            type="submit" 
            disabled={isSaving} 
            className="h-10 px-8 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest rounded-none flex items-center justify-center gap-2 hover:bg-[#27272a] disabled:opacity-50 transition-all shadow-[4px_4px_0px_0px_rgba(0,0,0,0.1)] hover:shadow-none hover:translate-y-[2px] hover:translate-x-[2px] active:scale-95"
          >
            {isSaving ? <Loader2 size={16} className="animate-spin" /> : "Save Changes"}
          </button>
        </div>
        
      </form>
    </div>
  );
}
