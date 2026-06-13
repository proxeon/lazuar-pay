import { useState, useEffect } from "react";
import { useNavigate } from "react-router-dom";
import { X, Loader2, CreditCard } from "lucide-react";
import { toast } from "sonner";
import { client } from "../lib/api-client";

export default function PaymentSettingsPage() {
  const navigate = useNavigate();
  
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  
  const [gatewayType, setGatewayType] = useState<"STRIPE" | "BILLPLZ">("BILLPLZ");
  const [isActive, setIsActive] = useState(true);
  
  const [apiKey, setApiKey] = useState("");
  const [webhookSecret, setWebhookSecret] = useState("");
  const [secretKey, setSecretKey] = useState("");
  const [collectionId, setCollectionId] = useState("");
  
  const [estimatedFeePct, setEstimatedFeePct] = useState("0");
  const [fixedFee, setFixedFee] = useState("0");
  const [taxRate, setTaxRate] = useState("0");

  useEffect(() => {
    async function loadConfig() {
      try {
        const { data, error } = await client.GET("/admin/community/payment-config");
        if (!error && data) {
          setGatewayType(data.gateway_type as any || "BILLPLZ");
          setIsActive(data.is_active ?? true);
          setApiKey(data.api_key || "");
          setWebhookSecret(data.webhook_secret || "");
          setSecretKey(data.secret_key || "");
          setCollectionId(data.merchant_id || "");
          setEstimatedFeePct((data.estimated_fee_percentage || 0).toString());
          setFixedFee((data.fixed_fee || 0).toString());
          setTaxRate((data.tax_rate || 0).toString());
        }
      } catch (err) {
        toast.error("Failed to load payment configuration.");
      } finally {
        setIsLoading(false);
      }
    }
    loadConfig();
  }, []);

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

    setIsSaving(true);
    try {
      const { error } = await client.PUT("/admin/community/payment-config", {
        body: {
          gateway_type: gatewayType,
          is_active: isActive,
          api_key: apiKey.trim(),
          secret_key: secretKey.trim(),
          webhook_secret: webhookSecret.trim(),
          collection_id: collectionId.trim(),
          estimated_fee_percentage: parseFloat(estimatedFeePct) || 0,
          fixed_fee: parseFloat(fixedFee) || 0,
          tax_rate: parseFloat(taxRate) || 0
        }
      });

      if (error) throw new Error(error.detail || "Failed to save configuration");
      
      toast.success("Payment configuration saved securely.");
      navigate("/chat");
    } catch (err: any) {
      toast.error(err.message);
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <div className="flex-1 w-full p-6 md:p-12 overflow-y-auto bg-[#fafafa]">
      <div className="max-w-2xl mx-auto bg-white border border-[#e5e5e5] rounded-none flex flex-col">
        <div className="flex items-center justify-between p-5 border-b border-[#e5e5e5] shrink-0 bg-[#fafafa]/50">
          <div className="flex items-center gap-3">
            <div className="p-2 bg-white border border-[#e5e5e5] text-[#09090b]">
              <CreditCard size={16} />
            </div>
            <div>
              <h3 className="text-[14px] font-semibold tracking-tight text-[#09090b]">Payment Configuration</h3>
              <p className="text-[11px] text-[#71717a] mt-0.5">Securely manage your active payment gateway.</p>
            </div>
          </div>
          <button onClick={() => navigate("/chat")} className="text-[#a1a1aa] hover:bg-[#e5e5e5] hover:text-[#09090b] transition-colors p-1"><X size={16} /></button>
        </div>

        {isLoading ? (
          <div className="p-12 flex justify-center"><Loader2 className="animate-spin text-[#a1a1aa]" /></div>
        ) : (
          <form onSubmit={handleSubmit} className="flex flex-col">
            <div className="p-6 space-y-6">
              
              <div className="space-y-3">
                <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1">Provider Settings</label>
                <div className="grid grid-cols-2 gap-4">
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-semibold text-[#09090b]">Gateway Type</label>
                    <select value={gatewayType} onChange={e => setGatewayType(e.target.value as any)} className="w-full h-10 border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b]">
                      <option value="BILLPLZ">Billplz (Malaysia)</option>
                      <option value="STRIPE">Stripe (Global)</option>
                    </select>
                  </div>
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-semibold text-[#09090b]">Status</label>
                    <select value={isActive ? "true" : "false"} onChange={e => setIsActive(e.target.value === "true")} className="w-full h-10 border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b]">
                      <option value="true">Active (Accepting Payments)</option>
                      <option value="false">Disabled</option>
                    </select>
                  </div>
                </div>
              </div>

              <div className="space-y-4">
                <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1">Secure Credentials</label>
                
                {gatewayType === "BILLPLZ" ? (
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
                      <p className="text-[10px] text-[#a1a1aa]">Must be exactly 128 characters long for signature verification.</p>
                    </div>
                  </>
                ) : (
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
              </div>

              <div className="space-y-3">
                <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1">Accounting Overrides</label>
                <div className="grid grid-cols-3 gap-3">
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-semibold text-[#09090b]">Est. Fee (%)</label>
                    <input type="number" step="0.01" value={estimatedFeePct} onChange={e => setEstimatedFeePct(e.target.value)} className="w-full h-10 border border-[#e5e5e5] px-3 text-[13px] focus:outline-none focus:border-[#09090b]" />
                  </div>
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-semibold text-[#09090b]">Fixed Fee (MYR)</label>
                    <input type="number" step="0.01" value={fixedFee} onChange={e => setFixedFee(e.target.value)} className="w-full h-10 border border-[#e5e5e5] px-3 text-[13px] focus:outline-none focus:border-[#09090b]" />
                  </div>
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-semibold text-[#09090b]">Tax Rate (%)</label>
                    <input type="number" step="0.01" value={taxRate} onChange={e => setTaxRate(e.target.value)} className="w-full h-10 border border-[#e5e5e5] px-3 text-[13px] focus:outline-none focus:border-[#09090b]" />
                  </div>
                </div>
              </div>

            </div>

            <div className="flex items-center justify-end gap-3 p-5 border-t border-[#f4f4f5] bg-[#fafafa]/50 mt-auto">
              <button type="button" onClick={() => navigate("/chat")} className="text-[11px] font-bold uppercase tracking-widest text-[#71717a] hover:text-[#09090b] transition-colors">Cancel</button>
              <button type="submit" disabled={isSaving} className="h-10 px-6 bg-[#09090b] text-white text-[11px] font-bold tracking-widest uppercase rounded-none hover:bg-[#27272a] disabled:opacity-50 transition-colors flex items-center gap-2">
                {isSaving && <Loader2 size={13} className="animate-spin" />} Save Configuration
              </button>
            </div>
          </form>
        )}
      </div>
    </div>
  );
}
