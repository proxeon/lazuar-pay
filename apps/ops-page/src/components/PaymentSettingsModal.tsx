import { useState, useEffect } from "react";
import { X, Loader2, CreditCard } from "lucide-react";
import { toast } from "sonner";
import { client } from "../lib/api-client";

interface PaymentSettingsModalProps {
  onClose: () => void;
}

export default function PaymentSettingsModal({ onClose }: PaymentSettingsModalProps) {
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  
  const [configs, setConfigs] = useState<any[]>([]);
  const [gatewayType, setGatewayType] = useState<"STRIPE" | "BILLPLZ" | "RAZORPAY" | "CHIP">("BILLPLZ");
  
  const [apiKey, setApiKey] = useState("");
  const [webhookSecret, setWebhookSecret] = useState(""); 
  const [secretKey, setSecretKey] = useState(""); 
  const [collectionId, setCollectionId] = useState(""); 

  useEffect(() => {
    async function loadConfig() {
      try {
        const { data, error } = await client.GET("/admin/commerce/payment-config");
        if (error) throw new Error(error.detail);
        if (data) {
          setConfigs(data);
          const current = data.find(c => c.gateway_type === "BILLPLZ");
          setApiKey(current?.api_key || "");
          setWebhookSecret(current?.webhook_secret || "");
          setSecretKey(current?.secret_key || "");
          setCollectionId(current?.merchant_id || "");
        }
      } catch (err) {
        toast.error("Failed to load payment configuration.");
      } finally {
        setIsLoading(false);
      }
    }
    loadConfig();
  }, []);

  const handleGatewayChange = (type: any) => {
    setGatewayType(type);
    const current = configs.find(c => c.gateway_type === type);
    setApiKey(current?.api_key || "");
    setWebhookSecret(current?.webhook_secret || "");
    setSecretKey(current?.secret_key || "");
    setCollectionId(current?.merchant_id || "");
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (gatewayType === "BILLPLZ") {
      const activeSignature = webhookSecret.trim();
      if (!activeSignature.includes("••••") && activeSignature.length !== 128) {
        toast.error("Invalid Configuration", { 
          description: "Billplz X-Signature Key must be exactly 128 characters long." 
        });
        return;
      }
      if (!collectionId.trim()) {
        toast.error("Collection ID is required for Billplz.");
        return;
      }
    }

    if (gatewayType === "CHIP" && !collectionId.trim()) {
      toast.error("Brand ID is required for CHIP Collect.");
      return;
    }

    setIsSaving(true);
    try {
      const { error } = await client.PUT("/admin/commerce/payment-config", {
        body: {
          gateway_type: gatewayType,
          api_key: apiKey.trim(),
          secret_key: secretKey.trim(),
          webhook_secret: webhookSecret.trim(),
          collection_id: collectionId.trim()
        }
      });

      if (error) throw new Error(error.detail || "Failed to save configuration");
      
      toast.success(`${gatewayType} credentials saved securely.`);
      onClose();
    } catch (err: any) {
      toast.error(err.message);
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4">
      <div className="absolute inset-0 bg-black/20 backdrop-blur-sm transition-opacity" onClick={onClose} />
      
      <div className="relative bg-white border border-[#e5e5e5] rounded-none shadow-[8px_8px_0px_0px_rgba(0,0,0,0.1)] w-full max-w-lg overflow-hidden flex flex-col animate-in fade-in zoom-in-95 duration-200">
        <div className="flex items-center justify-between p-5 border-b border-[#e5e5e5] shrink-0 bg-[#fafafa]/50">
          <div className="flex items-center gap-3">
            <div className="p-2 bg-white border border-[#e5e5e5] text-[#09090b]">
              <CreditCard size={16} />
            </div>
            <div>
              <h3 className="text-[14px] font-semibold tracking-tight text-[#09090b]">Payment Credential Vault</h3>
              <p className="text-[11px] text-[#71717a] mt-0.5">Securely store API keys for multiple providers.</p>
            </div>
          </div>
          <button onClick={onClose} className="text-[#a1a1aa] hover:bg-[#e5e5e5] hover:text-[#09090b] transition-colors p-1"><X size={16} /></button>
        </div>

        {isLoading ? (
          <div className="p-12 flex justify-center"><Loader2 className="animate-spin text-[#a1a1aa]" /></div>
        ) : (
          <form onSubmit={handleSubmit} className="flex flex-col">
            <div className="p-6 space-y-6 max-h-[65vh] overflow-y-auto">
              
              <div className="space-y-3">
                <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">Target Provider</label>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-semibold text-[#09090b]">Gateway Type</label>
                    <select value={gatewayType} onChange={e => handleGatewayChange(e.target.value)} className="w-full h-10 border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b]">
                      <option value="CHIP">CHIP Collect (Malaysia)</option>
                      <option value="BILLPLZ">Billplz (Malaysia)</option>
                      <option value="STRIPE">Stripe (Global)</option>
                      <option value="RAZORPAY">Razorpay (Global)</option>
                    </select>
                  </div>
                </div>
              </div>

              <div className="space-y-4">
                <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">Secure Credentials</label>
                
                {gatewayType === "CHIP" && (
                  <>
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-semibold text-[#09090b]">Brand ID</label>
                      <input type="text" value={collectionId} onChange={e => setCollectionId(e.target.value)} required placeholder="e.g. 75a76529-..." className="w-full h-10 border border-[#e5e5e5] px-3 font-mono text-[13px] focus:outline-none focus:border-[#09090b]" />
                    </div>
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-semibold text-[#09090b]">Secret Key (API Key)</label>
                      <input type="password" value={apiKey} onChange={e => setApiKey(e.target.value)} required placeholder="••••••••" className="w-full h-10 border border-[#e5e5e5] px-3 font-mono text-[13px] focus:outline-none focus:border-[#09090b]" />
                      <p className="text-[10px] text-[#a1a1aa] mt-1">Lazuar will autonomously fetch your RSA Public Key and configure your webhook endpoints upon saving.</p>
                    </div>
                  </>
                )}

                {gatewayType === "BILLPLZ" && (
                  <>
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-semibold text-[#09090b]">Collection ID</label>
                      <input type="text" value={collectionId} onChange={e => setCollectionId(e.target.value)} required placeholder="e.g. qigic0ou" className="w-full h-10 border border-[#e5e5e5] px-3 font-mono text-[13px] focus:outline-none focus:border-[#09090b]" />
                    </div>
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-semibold text-[#09090b]">API Key (Secret Key)</label>
                      <input type="password" value={apiKey} onChange={e => setApiKey(e.target.value)} required placeholder="••••••••" className="w-full h-10 border border-[#e5e5e5] px-3 font-mono text-[13px] focus:outline-none focus:border-[#09090b]" />
                    </div>
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-semibold text-[#09090b]">X-Signature Key (Webhook Secret)</label>
                      <input type="password" value={webhookSecret} onChange={e => setWebhookSecret(e.target.value)} required placeholder="128-character hex string" className="w-full h-10 border border-[#e5e5e5] px-3 font-mono text-[13px] focus:outline-none focus:border-[#09090b]" />
                      <p className="text-[10px] text-[#a1a1aa] mt-1">Must be exactly 128 characters long for signature verification.</p>
                    </div>
                  </>
                )}

                {gatewayType === "STRIPE" && (
                  <>
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-semibold text-[#09090b]">Secret Key</label>
                      <input type="password" value={secretKey} onChange={e => setSecretKey(e.target.value)} required placeholder="sk_live_..." className="w-full h-10 border border-[#e5e5e5] px-3 font-mono text-[13px] focus:outline-none focus:border-[#09090b]" />
                    </div>
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-semibold text-[#09090b]">Webhook Signing Secret</label>
                      <input type="password" value={webhookSecret} onChange={e => setWebhookSecret(e.target.value)} required placeholder="whsec_..." className="w-full h-10 border border-[#e5e5e5] px-3 font-mono text-[13px] focus:outline-none focus:border-[#09090b]" />
                    </div>
                  </>
                )}

                {gatewayType === "RAZORPAY" && (
                  <>
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-semibold text-[#09090b]">API Key (KeyId:KeySecret)</label>
                      <input type="password" value={apiKey} onChange={e => setApiKey(e.target.value)} required placeholder="rzp_live_xxx:secret_yyy" className="w-full h-10 border border-[#e5e5e5] px-3 font-mono text-[13px] focus:outline-none focus:border-[#09090b]" />
                      <p className="text-[10px] text-[#a1a1aa] mt-1">Format must be KeyId:KeySecret</p>
                    </div>
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-semibold text-[#09090b]">Webhook Signing Secret</label>
                      <input type="password" value={webhookSecret} onChange={e => setWebhookSecret(e.target.value)} required placeholder="Your custom webhook secret" className="w-full h-10 border border-[#e5e5e5] px-3 font-mono text-[13px] focus:outline-none focus:border-[#09090b]" />
                    </div>
                  </>
                )}
              </div>

            </div>

            <div className="flex items-center justify-end p-5 border-t border-[#f4f4f5] bg-[#fafafa]/50 mt-auto">
              <button type="button" onClick={onClose} className="text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:text-[#09090b] transition-colors">Cancel</button>
              <button type="submit" disabled={isSaving} className="h-10 px-6 bg-[#09090b] text-white text-[11px] font-bold tracking-widest uppercase rounded-none hover:bg-[#27272a] disabled:opacity-50 transition-colors flex items-center gap-2">
                {isSaving && <Loader2 size={13} className="animate-spin" />} Save Credentials
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
}
