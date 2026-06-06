import { X, CheckCircle2, Server, MessageSquare, Users, Database } from "lucide-react";
import type { MockTenant } from "./Tenants";
import { cn } from "../lib/utils";

interface TenantDetailsSlideoutProps {
  tenant: MockTenant | null;
  onClose: () => void;
}

export default function TenantDetailsSlideout({ tenant, onClose }: TenantDetailsSlideoutProps) {
  if (!tenant) return null;

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
              {tenant.name}
            </h2>
            <div className="flex items-center gap-2 mt-2">
              <span className="text-[11px] font-mono text-[#71717a] bg-[#f4f4f5] px-1.5 py-0.5 border border-[#e5e5e5]">
                {tenant.slug}
              </span>
              {tenant.provisioningStatus === 'COMPLETE' && tenant.isActive && (
                <span className="inline-flex items-center gap-1 text-[10px] font-bold uppercase tracking-widest text-emerald-600">
                  <span className="w-1.5 h-1.5 rounded-full bg-emerald-500" /> Active
                </span>
              )}
            </div>
          </div>
          <button 
            onClick={onClose} 
            className="text-[#a1a1aa] hover:bg-[#e5e5e5] hover:text-[#09090b] transition-colors p-1.5 rounded-none"
          >
            <X size={16} />
          </button>
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto p-6 space-y-8">
          
          {/* Metadata Section */}
          <section className="space-y-3">
            <h3 className="text-[10px] font-bold uppercase tracking-widest text-[#a1a1aa] mb-3 border-b border-[#f4f4f5] pb-1">
              Metadata
            </h3>
            <div className="flex justify-between items-center text-[13px]">
              <span className="text-[#71717a]">Tenant ID</span>
              <span className="font-mono text-[#09090b]">{tenant.id}</span>
            </div>
            <div className="flex justify-between items-center text-[13px]">
              <span className="text-[#71717a]">Created At</span>
              <span className="font-mono text-[#09090b]">
                {new Date(tenant.createdAt).toLocaleDateString("en-MY")}
              </span>
            </div>
          </section>

          {/* Provisioned Modules Mockup */}
          <section>
            <h3 className="text-[10px] font-bold uppercase tracking-widest text-[#a1a1aa] mb-4 border-b border-[#f4f4f5] pb-1">
              Provisioned Infrastructure
            </h3>
            
            <div className="space-y-3">
              {tenant.provisioningStatus === 'PROVISIONING' ? (
                <div className="p-4 border border-amber-200 bg-amber-50 text-amber-800 text-[12px] font-mono">
                  Provisioning sequence currently running...
                </div>
              ) : (
                <>
                  <ModuleItem 
                    icon={Database} 
                    title="Cognitive Core" 
                    status="Active" 
                    desc="Tenant DB schema & config isolated." 
                  />
                  <ModuleItem 
                    icon={MessageSquare} 
                    title="Messaging" 
                    status="Seeded" 
                    desc="Automation rules & templates injected." 
                  />
                  <ModuleItem 
                    icon={Users} 
                    title="Community" 
                    status="Ready" 
                    desc="Subscription engine & CRM linked." 
                  />
                  <ModuleItem 
                    icon={Server} 
                    title="Storage Vault" 
                    status="Mounted" 
                    desc="S3 buckets & policies provisioned." 
                  />
                </>
              )}
            </div>
          </section>

        </div>

        {/* Footer */}
        <div className="p-6 border-t border-[#f4f4f5] bg-[#fafafa]/50 shrink-0">
          <button 
            disabled={tenant.provisioningStatus === 'PROVISIONING'}
            className={cn(
              "w-full h-10 border text-[11px] font-bold uppercase tracking-widest transition-colors shadow-sm",
              tenant.provisioningStatus === 'PROVISIONING'
                ? "bg-[#f4f4f5] text-[#a1a1aa] border-[#e5e5e5] cursor-not-allowed"
                : "bg-white text-rose-600 border-[#e5e5e5] hover:bg-rose-50 hover:border-rose-200"
            )}
          >
            Suspend Tenant
          </button>
        </div>
      </div>
    </div>
  );
}

function ModuleItem({ icon: Icon, title, status, desc }: { icon: any, title: string, status: string, desc: string }) {
  return (
    <div className="flex items-start gap-3 p-3 border border-[#e5e5e5] bg-white">
      <div className="p-1.5 bg-emerald-50 text-emerald-600 border border-emerald-100">
        <Icon size={14} />
      </div>
      <div className="flex-1">
        <div className="flex items-center justify-between">
          <h4 className="text-[12px] font-semibold text-[#09090b]">{title}</h4>
          <span className="text-[9px] font-bold uppercase tracking-widest text-emerald-600 flex items-center gap-1">
            <CheckCircle2 size={10} /> {status}
          </span>
        </div>
        <p className="text-[11px] text-[#71717a] mt-0.5 leading-snug">{desc}</p>
      </div>
    </div>
  );
}
