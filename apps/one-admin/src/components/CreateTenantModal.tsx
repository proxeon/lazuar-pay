import { useState, useEffect } from "react";
import { X, Loader2 } from "lucide-react";
import { cn } from "../lib/utils";

interface ProvisionPayload {
  name: string;
  slug: string;
  owner_name: string;
  owner_email: string;
  provision_apps: string[];
}

interface CreateTenantModalProps {
  onClose: () => void;
  onSuccess: (payload: ProvisionPayload) => void;
  isSubmitting: boolean;
}

const AVAILABLE_APPS = [
  { id: "COMMUNITY", title: "Community Module", desc: "Subscription Engine & CRM" },
  { id: "VAULT", title: "Storage Vault", desc: "Digital Asset Delivery" },
  { id: "FUNNEL", title: "Funnel Builder", desc: "Acquisition Landing Pages" }
];

export default function CreateTenantModal({ onClose, onSuccess, isSubmitting }: CreateTenantModalProps) {
  // Workspace State
  const [name, setName] = useState("");
  const [slug, setSlug] = useState("");
  
  // Customer State
  const [ownerName, setOwnerName] = useState("");
  const [ownerEmail, setOwnerEmail] = useState("");
  
  // Entitlement State
  const [selectedApps, setSelectedApps] = useState<string[]>([]);

  // Auto-slugify effect
  useEffect(() => {
    const generatedSlug = name
      .trim()
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '-') 
      .replace(/(^-|-$)+/g, '');   
    
    setSlug(generatedSlug);
  }, [name]);

  const handleAppToggle = (appId: string) => {
    setSelectedApps(prev => 
      prev.includes(appId) ? prev.filter(id => id !== appId) : [...prev, appId]
    );
  };

  const isFormValid = name.trim() && slug.trim() && ownerName.trim() && ownerEmail.trim();

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (!isFormValid) return;
    
    onSuccess({
      name: name.trim(),
      slug: slug,
      owner_name: ownerName.trim(),
      owner_email: ownerEmail.trim().toLowerCase(),
      provision_apps: selectedApps
    });
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/20 backdrop-blur-sm transition-opacity" onClick={onClose} />
      
      <div className="relative bg-white border border-[#e5e5e5] rounded-none shadow-[8px_8px_0px_0px_rgba(0,0,0,0.1)] w-full max-w-2xl overflow-hidden flex flex-col animate-in fade-in zoom-in-95 duration-200 max-h-[90vh]">
        
        <div className="flex items-center justify-between p-5 border-b border-[#e5e5e5] shrink-0 bg-[#fafafa]/50">
          <div>
            <h3 className="text-[14px] font-semibold tracking-tight text-[#09090b]">Provision Customer Workspace</h3>
            <p className="text-[11px] text-[#71717a] mt-0.5">Create a tenant, bind an owner, and dispatch credentials.</p>
          </div>
          <button onClick={onClose} className="text-[#a1a1aa] hover:bg-[#e5e5e5] hover:text-[#09090b] rounded-none transition-colors p-1">
            <X size={16} />
          </button>
        </div>

        <form onSubmit={handleSubmit} className="flex-1 overflow-y-auto">
          
          <div className="p-6 space-y-8">
            
            {/* 1. Customer Binding */}
            <section className="space-y-4">
              <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#a1a1aa] border-b border-[#f4f4f5] pb-1">1. Customer Details</h4>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Owner Full Name *</label>
                  <input type="text" required value={ownerName} onChange={(e) => setOwnerName(e.target.value)} placeholder="e.g. Ahmad Firdaus" className="flex h-10 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]" />
                </div>
                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Owner Email *</label>
                  <input type="email" required value={ownerEmail} onChange={(e) => setOwnerEmail(e.target.value)} placeholder="ahmad@example.com" className="flex h-10 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]" />
                </div>
              </div>
            </section>

            {/* 2. Workspace Config */}
            <section className="space-y-4">
              <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#a1a1aa] border-b border-[#f4f4f5] pb-1">2. Workspace Configuration</h4>
              <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Workspace Name *</label>
                  <input type="text" required value={name} onChange={(e) => setName(e.target.value)} placeholder="e.g. Acme Corporation" className="flex h-10 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]" />
                </div>
                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Generated Slug</label>
                  <input type="text" readOnly disabled value={slug} className="flex h-10 w-full rounded-none border border-[#e5e5e5] bg-[#fafafa] px-3 py-1 text-sm text-[#71717a] font-mono cursor-not-allowed outline-none shadow-inner" />
                </div>
              </div>
            </section>

            {/* 3. Entitlements */}
            <section className="space-y-4">
              <h4 className="text-[10px] font-bold uppercase tracking-widest text-[#a1a1aa] border-b border-[#f4f4f5] pb-1">3. Initial Entitlements</h4>
              <div className="grid grid-cols-1 md:grid-cols-3 gap-3">
                {AVAILABLE_APPS.map(app => {
                  const isChecked = selectedApps.includes(app.id);
                  return (
                    <div 
                      key={app.id} 
                      onClick={() => handleAppToggle(app.id)}
                      className={cn(
                        "flex flex-col items-start justify-between p-3 border cursor-pointer select-none transition-colors rounded-none outline-none",
                        isChecked ? "bg-emerald-50/60 border-emerald-300" : "border-[#e5e5e5] bg-white hover:bg-[#fafafa]"
                      )}
                    >
                      <div className="flex items-center justify-between w-full mb-1">
                        <span className={cn("text-[12px] font-bold", isChecked ? "text-[#09090b]" : "text-[#71717a]")}>{app.title}</span>
                        <input type="checkbox" readOnly checked={isChecked} className="h-3.5 w-3.5 border-zinc-300 text-[#09090b] accent-[#09090b] pointer-events-none" />
                      </div>
                      <span className="text-[10px] text-[#71717a]">{app.desc}</span>
                    </div>
                  );
                })}
              </div>
            </section>

          </div>

          {/* Footer Actions */}
          <div className="flex items-center justify-between p-5 border-t border-[#f4f4f5] shrink-0 bg-[#fafafa]/50">
            <p className="text-[10px] text-[#71717a] max-w-[250px] leading-relaxed">
              Upon submission, a welcome email with a temporary password will be dispatched to the owner.
            </p>
            <div className="flex gap-3">
              <button type="button" onClick={onClose} className="text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:text-[#09090b] transition-colors px-2 py-1">Cancel</button>
              <button 
                type="submit" 
                disabled={isSubmitting || !isFormValid}
                className="h-10 px-6 bg-[#09090b] text-white text-[11px] font-bold tracking-widest uppercase rounded-none hover:bg-[#27272a] disabled:opacity-50 transition-colors shadow-[4px_4px_0px_0px_rgba(0,0,0,0.1)] hover:shadow-none hover:translate-y-[2px] hover:translate-x-[2px] active:scale-95 flex items-center justify-center gap-2"
              >
                {isSubmitting && <Loader2 size={12} className="animate-spin" />}
                {isSubmitting ? "Provisioning..." : "Provision Workspace"}
              </button>
            </div>
          </div>

        </form>
      </div>
    </div>
  );
}
