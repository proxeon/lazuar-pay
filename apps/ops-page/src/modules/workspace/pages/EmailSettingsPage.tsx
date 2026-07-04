import { useState, useEffect } from "react";
import { Loader2, Mail, AlertTriangle } from "lucide-react";
import { toast } from "sonner";
import { client } from "../../../lib/api-client";
import PageLayout from "../../core/components/PageLayout";

export default function EmailSettingsPage() {
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  
  const [isActive, setIsActive] = useState(true);
  const [apiKey, setApiKey] = useState("");
  const [senderEmail, setSenderEmail] = useState("");

  useEffect(() => {
    async function loadConfig() {
      try {
        const { data, error, response } = await client.GET("/admin/communications/email-config");
        if (response.status === 404) return;
        if (error) throw new Error(error.detail);
        if (data) {
          setIsActive(data.is_active ?? true);
          setApiKey(data.api_key || "");
          setSenderEmail(data.sender_email || "");
        }
      } catch (err) {
        toast.error("Failed to load email configuration.");
      } finally {
        setIsLoading(false);
      }
    }
    loadConfig();
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!apiKey.trim() || !senderEmail.trim()) {
      toast.error("API Key and Sender Email are required.");
      return;
    }

    setIsSaving(true);
    try {
      const { error } = await client.PUT("/admin/communications/email-config", {
        body: {
          api_key: apiKey.trim(),
          sender_email: senderEmail.trim(),
          is_active: isActive
        }
      });

      if (error) throw new Error(error.detail || "Failed to save configuration");
      
      toast.success("Email configuration verified and saved securely.");
    } catch (err: any) {
      toast.error(err.message);
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <PageLayout
      title="Email Provider Settings"
      description="Configure your Resend API credentials to enable automated receipts, dunning emails, and broadcasts."
      breadcrumbs={[{ label: "Workspace" }, { label: "Email Provider" }]}
    >
      <div className="bg-white border border-[#e5e5e5] rounded-none flex flex-col">
        {isLoading ? (
          <div className="p-12 flex justify-center"><Loader2 className="animate-spin text-[#a1a1aa]" /></div>
        ) : (
          <form onSubmit={handleSubmit} className="flex flex-col">
            <div className="p-6 md:p-8 space-y-8">
              
              <div className="flex items-start gap-3 p-4 bg-blue-50 border border-blue-200">
                <AlertTriangle size={18} className="text-blue-600 mt-0.5" />
                <div>
                  <h4 className="text-[12px] font-bold text-blue-800 uppercase tracking-widest">Custom Domain Required</h4>
                  <p className="text-[12px] text-blue-700 mt-1 leading-relaxed">
                    To prevent spam and ensure deliverability, you must own a custom domain (e.g., hello@yourdomain.com). You cannot use standard Gmail or Yahoo addresses. Ensure your domain's DNS records are fully verified in your Resend account before saving.
                  </p>
                </div>
              </div>

              <div className="space-y-4">
                <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">Provider Configuration</label>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-semibold text-[#09090b]">Provider</label>
                    <div className="w-full h-10 border border-[#e5e5e5] bg-[#fafafa] px-3 flex items-center text-[13px] text-[#71717a] cursor-not-allowed">
                      <Mail size={14} className="mr-2" /> Resend
                    </div>
                  </div>
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-semibold text-[#09090b]">Status</label>
                    <select value={isActive ? "true" : "false"} onChange={e => setIsActive(e.target.value === "true")} className="w-full h-10 border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b]">
                      <option value="true">Active (Sending Emails)</option>
                      <option value="false">Disabled</option>
                    </select>
                  </div>
                </div>
              </div>

              <div className="space-y-4">
                <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">Secure Credentials</label>
                
                <div className="space-y-1.5">
                  <label className="text-[11px] font-semibold text-[#09090b]">Resend API Key *</label>
                  <input type="password" value={apiKey} onChange={e => setApiKey(e.target.value)} required placeholder="re_..." className="w-full h-10 border border-[#e5e5e5] px-3 font-mono text-[13px] focus:outline-none focus:border-[#09090b]" />
                  <p className="text-[10px] text-[#a1a1aa] mt-1">Lazuar will instantly verify this key against the Resend API before saving.</p>
                </div>
                
                <div className="space-y-1.5">
                  <label className="text-[11px] font-semibold text-[#09090b]">Sender Email Address *</label>
                  <input type="email" value={senderEmail} onChange={e => setSenderEmail(e.target.value)} required placeholder="receipts@yourdomain.com" className="w-full h-10 border border-[#e5e5e5] px-3 font-mono text-[13px] focus:outline-none focus:border-[#09090b]" />
                </div>
              </div>

            </div>

            <div className="flex items-center justify-end p-5 border-t border-[#f4f4f5] bg-[#fafafa]/50 mt-auto">
              <button type="submit" disabled={isSaving} className="h-10 px-8 bg-[#09090b] text-white text-[11px] font-bold tracking-widest uppercase rounded-none hover:bg-[#27272a] disabled:opacity-50 transition-colors flex items-center gap-2">
                {isSaving && <Loader2 size={13} className="animate-spin" />} Save Configuration
              </button>
            </div>
          </form>
        )}
      </div>
    </PageLayout>
  );
}
