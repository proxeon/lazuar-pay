import { useState } from "react";
import { Loader2, UploadCloud, User, Mail, Phone, Globe, Info } from "lucide-react";
import { toast } from "sonner";
import { cn } from "../lib/utils";

export default function Profile() {
  const [isLoading, setIsLoading] = useState(false);
  const [name, setName] = useState("Akmal Firdaus");
  const [email, setEmail] = useState("akmal@lazuar.com");
  const [phone, setPhone] = useState("+60 12-345 6789");
  const [timezone, setTimezone] = useState("Asia/Kuala_Lumpur");

  const handleSave = (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);

    // Mock 1-second network latency
    setTimeout(() => {
      setIsLoading(false);
      toast.success("Profile updated successfully", {
        description: "Your global identity has been synced across all tenants.",
      });
    }, 1000);
  };

  return (
    <div className="flex-1 w-full p-4 md:p-8 mx-auto max-w-[800px] flex flex-col gap-6 animate-in fade-in duration-300">
      
      {/* Header */}
      <header className="flex flex-col pb-2 border-b border-[#e5e5e5]">
        <h1 className="text-[20px] font-semibold tracking-tight text-[#09090b]">Unified Profile</h1>
        <p className="text-[13px] text-[#71717a] mt-1">Manage your master identity across the Lazuar ecosystem.</p>
      </header>

      <form onSubmit={handleSave} className="space-y-6">
        
        {/* --- 1. Avatar & Basic Info Section --- */}
        <section className="bg-white border border-[#e5e5e5] rounded-xl shadow-sm overflow-hidden">
          <div className="px-5 py-4 border-b border-[#f4f4f5] bg-[#fafafa]/50 flex items-center gap-2">
            <User size={16} className="text-[#a1a1aa]" />
            <h2 className="text-[13px] font-semibold text-[#09090b]">Basic Information</h2>
          </div>
          
          <div className="p-6 space-y-6">
            {/* Avatar Upload Placeholder */}
            <div className="flex items-center gap-5">
              <div className="flex h-20 w-20 shrink-0 items-center justify-center rounded-full bg-[#f4f4f5] border border-[#e5e5e5] text-[24px] font-bold text-[#52525b] shadow-inner">
                AF
              </div>
              <div className="flex flex-col gap-2">
                <button type="button" className="inline-flex items-center gap-2 px-3 py-1.5 border border-[#e5e5e5] rounded-md text-[12px] font-medium text-[#09090b] hover:bg-[#f4f4f5] transition-colors shadow-sm w-fit">
                  <UploadCloud size={14} />
                  Upload new avatar
                </button>
                <p className="text-[11px] text-[#a1a1aa]">JPEG, PNG or GIF. Max size 2MB.</p>
              </div>
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-5">
              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Global Full Name</label>
                <input
                  type="text"
                  required
                  value={name}
                  onChange={(e) => setName(e.target.value)}
                  className="flex h-10 w-full rounded-md border border-[#e5e5e5] bg-white px-3 py-1 text-sm shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]"
                />
              </div>

              <div className="space-y-1.5">
                <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Master Email</label>
                <input
                  type="email"
                  required
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  className="flex h-10 w-full rounded-md border border-[#e5e5e5] bg-white px-3 py-1 text-sm shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]"
                />
              </div>
            </div>

            <div className="flex items-start gap-2.5 p-3 bg-blue-50 border border-blue-100 rounded-md">
              <Info size={16} className="text-blue-600 shrink-0 mt-0.5" />
              <p className="text-[12px] text-blue-800 leading-relaxed">
                <strong className="font-semibold">Ecosystem Sync:</strong> Changing your name, avatar, or email here will instantly update your profile across all Lazuar communities and vaults you belong to.
              </p>
            </div>
          </div>
        </section>

        {/* --- 2. Contact Preferences Section --- */}
        <section className="bg-white border border-[#e5e5e5] rounded-xl shadow-sm overflow-hidden">
          <div className="px-5 py-4 border-b border-[#f4f4f5] bg-[#fafafa]/50 flex items-center gap-2">
            <Phone size={16} className="text-[#a1a1aa]" />
            <h2 className="text-[13px] font-semibold text-[#09090b]">Contact & Localization</h2>
          </div>
          
          <div className="p-6 grid grid-cols-1 md:grid-cols-2 gap-5">
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">WhatsApp Number</label>
              <input
                type="tel"
                value={phone}
                onChange={(e) => setPhone(e.target.value)}
                className="flex h-10 w-full rounded-md border border-[#e5e5e5] bg-white px-3 py-1 text-sm shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]"
              />
              <p className="text-[11px] text-[#a1a1aa] mt-1">Used by communities for session reminders.</p>
            </div>

            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Timezone</label>
              <div className="relative">
                <Globe size={14} className="absolute left-3 top-3 text-[#a1a1aa]" />
                <select
                  value={timezone}
                  onChange={(e) => setTimezone(e.target.value)}
                  className="flex h-10 w-full rounded-md border border-[#e5e5e5] bg-white pl-9 pr-3 py-1 text-sm shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b] appearance-none"
                >
                  <option value="Asia/Kuala_Lumpur">Asia/Kuala Lumpur (GMT+8)</option>
                  <option value="Asia/Singapore">Asia/Singapore (GMT+8)</option>
                  <option value="Asia/Jakarta">Asia/Jakarta (GMT+7)</option>
                  <option value="UTC">UTC (GMT+0)</option>
                </select>
              </div>
            </div>
          </div>
        </section>

        {/* --- 3. Save Action --- */}
        <div className="flex justify-end pt-2">
          <button
            type="submit"
            disabled={isLoading}
            className="h-10 px-6 bg-[#09090b] text-white text-[13px] font-semibold tracking-wide rounded-md flex items-center justify-center gap-2 hover:bg-[#27272a] disabled:opacity-50 transition-colors shadow-sm"
          >
            {isLoading ? <Loader2 size={16} className="animate-spin" /> : "Save Changes"}
          </button>
        </div>
      </form>
    </div>
  );
}
