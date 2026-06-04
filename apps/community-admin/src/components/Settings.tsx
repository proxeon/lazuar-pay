import { useState, useEffect } from "react";
import { client, TENANT_SLUG } from "../lib/api-client";
import { Menu, Copy, Check, CreditCard, ExternalLink } from "lucide-react";

export default function Settings({ isMobile, toggleSidebar }: any) {
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [copied, setCopied] = useState(false);
  const [error, setError] = useState("");

  const [gatewayType, setGatewayType] = useState("BILLPLZ");
  const [apiKey, setApiKey] = useState("");
  const [collectionId, setCollectionId] = useState("");
  const [webhookSecret, setWebhookSecret] = useState("");
  const [secretKey, setSecretKey] = useState("");
  const [isActive, setIsActive] = useState(false);
  const [webhookUrl, setWebhookUrl] = useState("");

  useEffect(() => {
    loadConfig();
  }, []);

  async function loadConfig() {
    try {
      const { data, error } = await client.GET("/admin/community/payment-config");
      
      if (error && error.status !== 404) {
          throw new Error(error.detail || "Failed to load settings");
      }

      if (data) {
        setGatewayType(data.gateway_type || "BILLPLZ");
        setApiKey(data.api_key || "");
        setCollectionId(data.merchant_id || "");
        setWebhookSecret(data.webhook_secret || "");
        setSecretKey(data.secret_key || "");
        setIsActive(data.is_active || false);
        
        // Reconstruct expected callback route
        setWebhookUrl(`${client.baseUrl}/webhooks/payments/${data.gateway_type.toLowerCase()}/${TENANT_SLUG}`);
      }
    } catch (err: any) {
      setError(err.message);
    } finally {
      setIsLoading(false);
    }
  }

  async function handleSave(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    setSaved(false);
    setIsSaving(true);

    const { error: apiError } = await client.PUT("/admin/community/payment-config", {
        body: {
            gateway_type: gatewayType,
            api_key: apiKey,
            collection_id: collectionId,
            webhook_secret: webhookSecret,
            secret_key: secretKey,
            is_active: isActive
        }
    });

    setIsSaving(false);

    if (apiError) {
        setError(apiError.detail || "Failed to save configuration");
    } else {
        setWebhookUrl(`${client.baseUrl}/webhooks/payments/${gatewayType.toLowerCase()}/${TENANT_SLUG}`);
        setSaved(true);
        setTimeout(() => setSaved(false), 3000);
    }
  }

  function copyWebhookUrl() {
    navigator.clipboard.writeText(webhookUrl);
    setCopied(true);
    setTimeout(() => setCopied(false), 2000);
  }

  if (isLoading) {
    return (
      <div className="flex-1 w-full p-4 md:p-8 mx-auto max-w-[1240px]">
        <p className="text-sm font-medium uppercase tracking-widest text-muted-foreground">Loading settings...</p>
      </div>
    );
  }

  return (
    <div className="flex-1 w-full p-4 md:p-8 mx-auto max-w-[800px] flex flex-col gap-6">
      <header className="flex items-center gap-3 pb-2">
        {isMobile && <button onClick={toggleSidebar} className="p-1.5 hover:bg-secondary rounded-none transition-colors"><Menu size={20} /></button>}
        <div>
          <h1 className="text-[20px] font-semibold tracking-tight text-foreground">Settings</h1>
          <p className="text-[11px] font-bold uppercase tracking-[0.2em] text-muted-foreground mt-1">Configure payment gateway for community subscriptions.</p>
        </div>
      </header>

      {error && (
        <div className="p-4 bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-900 rounded-none">
          <p className="text-xs font-medium text-red-600 dark:text-red-400">{error}</p>
        </div>
      )}

      {saved && (
        <div className="p-4 bg-green-50 dark:bg-green-950/30 border border-green-200 dark:border-green-900 rounded-none flex items-center gap-2">
          <Check size={16} className="text-green-600 dark:text-green-500" />
          <p className="text-xs text-green-700 dark:text-green-400 font-bold uppercase tracking-widest">Payment configuration saved successfully.</p>
        </div>
      )}

      <form onSubmit={handleSave} className="space-y-6">
        <div className="bg-card border border-border/60 rounded-none shadow-sm p-6">
          <div className="flex items-center gap-2 mb-6">
            <CreditCard size={16} className="text-muted-foreground" />
            <h2 className="text-xs font-bold uppercase tracking-widest text-foreground">Payment Gateway</h2>
          </div>

          <div className="space-y-5">
            <div className="space-y-2">
              <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Gateway Provider</label>
              <select value={gatewayType} onChange={e => setGatewayType(e.target.value)} className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm shadow-sm transition-colors">
                <option value="BILLPLZ">Billplz (FPX / Malaysia)</option>
                <option value="STRIPE">Stripe (Credit Card / International)</option>
                <option value="MANUAL">Manual (No online payments)</option>
              </select>
            </div>

            {gatewayType === "BILLPLZ" && (
              <>
                <div className="space-y-2">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">API Key</label>
                  <input type="password" value={apiKey} onChange={e => setApiKey(e.target.value)} className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm font-mono shadow-sm transition-colors" placeholder="fc123456-abcd-efgh-ijkl-..." />
                  <p className="text-[11px] text-muted-foreground/80">Found in Billplz Dashboard → Settings → Keys</p>
                </div>
                <div className="space-y-2">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Collection ID</label>
                  <input type="text" value={collectionId} onChange={e => setCollectionId(e.target.value)} className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm font-mono shadow-sm transition-colors" placeholder="abc123de" />
                  <p className="text-[11px] text-muted-foreground/80">The collection where payments will be received</p>
                </div>
                <div className="space-y-2">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">X-Signature Key (Webhook Secret)</label>
                  <input type="password" value={webhookSecret} onChange={e => setWebhookSecret(e.target.value)} className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm font-mono shadow-sm transition-colors" placeholder="S-abcdefghijk..." />
                  <p className="text-[11px] text-muted-foreground/80">Used to verify webhook callbacks are from Billplz</p>
                </div>
              </>
            )}

            {gatewayType === "STRIPE" && (
              <>
                <div className="space-y-2">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Secret Key</label>
                  <input type="password" value={secretKey} onChange={e => setSecretKey(e.target.value)} className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm font-mono shadow-sm transition-colors" placeholder="sk_live_... or sk_test_..." />
                  <p className="text-[11px] text-muted-foreground/80">Found in Stripe Dashboard → Developers → API Keys</p>
                </div>
                <div className="space-y-2">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-muted-foreground">Webhook Signing Secret</label>
                  <input type="password" value={webhookSecret} onChange={e => setWebhookSecret(e.target.value)} className="flex h-10 w-full rounded-none border border-border/60 bg-background px-3 py-1 text-sm font-mono shadow-sm transition-colors" placeholder="whsec_..." />
                  <p className="text-[11px] text-muted-foreground/80">Found in Stripe Dashboard → Developers → Webhooks → Signing secret</p>
                </div>
              </>
            )}

            {gatewayType === "MANUAL" && (
              <div className="p-5 bg-secondary/50 border border-border/60 rounded-none mt-2">
                <p className="text-sm font-medium text-foreground">Online payments are disabled. Customers will not be able to subscribe via the community page.</p>
              </div>
            )}

            <div className="flex items-center gap-3 pt-4 pb-2">
              <input type="checkbox" id="is_active" checked={isActive} onChange={e => setIsActive(e.target.checked)} className="h-4 w-4 rounded-none border-border/60 focus:ring-foreground accent-foreground" />
              <label htmlFor="is_active" className="text-sm font-bold tracking-wide uppercase text-foreground">Enable online payments</label>
            </div>
          </div>
        </div>

        {gatewayType !== "MANUAL" && webhookUrl && (
          <div className="bg-card border border-border/60 rounded-none shadow-sm p-6">
            <div className="flex items-center gap-2 mb-4">
              <ExternalLink size={16} className="text-muted-foreground" />
              <h2 className="text-xs font-bold uppercase tracking-widest text-foreground">Webhook Callback URL</h2>
            </div>
            <p className="text-[11px] text-muted-foreground mb-4 leading-relaxed">
              {gatewayType === "BILLPLZ"
                ? "Paste this URL in your Billplz Dashboard → Collection → Settings → Callback URL (Payment completion)"
                : "Paste this URL in Stripe Dashboard → Developers → Webhooks → Add endpoint"}
            </p>
            <div className="flex items-center gap-2">
              <code className="flex-1 px-4 py-3 bg-secondary/50 border border-border/60 rounded-none text-xs font-mono text-foreground truncate">{webhookUrl}</code>
              <button type="button" onClick={copyWebhookUrl} className="shrink-0 h-10 w-12 flex items-center justify-center border border-border/60 bg-card rounded-none hover:bg-secondary transition-colors">
                {copied ? <Check size={15} className="text-emerald-600" /> : <Copy size={15} className="text-foreground" />}
              </button>
            </div>
            {copied && <p className="text-[10px] font-bold uppercase tracking-widest text-emerald-600 mt-2">Copied to clipboard!</p>}
          </div>
        )}

        <div className="flex justify-end pt-2 pb-8">
          <button type="submit" disabled={isSaving} className="h-12 px-8 bg-foreground text-background text-sm font-bold tracking-wide uppercase rounded-none hover:bg-foreground/90 disabled:opacity-50 transition-colors shadow-[4px_4px_0px_0px_rgba(0,0,0,0.1)] hover:shadow-none hover:translate-y-[2px] hover:translate-x-[2px] active:scale-95">
            {isSaving ? "Saving..." : "Save Configuration"}
          </button>
        </div>
      </form>
    </div>
  );
}
