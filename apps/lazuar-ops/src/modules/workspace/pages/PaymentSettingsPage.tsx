import { useState, useEffect } from "react";
import { Loader2 } from "lucide-react";
import { toast } from "sonner";
import { client } from "../../../lib/api-client";
import PageLayout from "../../core/components/PageLayout";

type GatewayType = "STRIPE" | "BILLPLZ" | "RAZORPAY" | "CHIP";

type PaymentConfigRow = {
  gateway_type: string;
  merchant_id?: string | null;
  is_active?: boolean;
  has_api_key?: boolean;
  api_key_hint?: string | null;
  has_webhook_secret?: boolean;
  webhook_secret_hint?: string | null;
  has_secret_key?: boolean;
  secret_key_hint?: string | null;
};

export default function PaymentSettingsPage() {
  const [isLoading, setIsLoading] = useState(true);
  const [isSaving, setIsSaving] = useState(false);

  const [configs, setConfigs] = useState<PaymentConfigRow[]>([]);
  const [gatewayType, setGatewayType] = useState<GatewayType>("BILLPLZ");

  const [apiKey, setApiKey] = useState("");
  const [webhookSecret, setWebhookSecret] = useState("");
  const [secretKey, setSecretKey] = useState("");
  const [collectionId, setCollectionId] = useState("");
  const [isActive, setIsActive] = useState(true);
  const [hasApiKey, setHasApiKey] = useState(false);
  const [apiKeyHint, setApiKeyHint] = useState<string | null>(null);
  const [hasWebhookSecret, setHasWebhookSecret] = useState(false);
  const [webhookHint, setWebhookHint] = useState<string | null>(null);
  const [hasSecretKey, setHasSecretKey] = useState(false);
  const [secretKeyHint, setSecretKeyHint] = useState<string | null>(null);

  const applyConfig = (current: PaymentConfigRow | undefined) => {
    setCollectionId(current?.merchant_id || "");
    setIsActive(current?.is_active ?? true);
    setHasApiKey(Boolean(current?.has_api_key));
    setApiKeyHint(current?.api_key_hint ?? null);
    setHasWebhookSecret(Boolean(current?.has_webhook_secret));
    setWebhookHint(current?.webhook_secret_hint ?? null);
    setHasSecretKey(Boolean(current?.has_secret_key));
    setSecretKeyHint(current?.secret_key_hint ?? null);
    // Never populate password fields with stored secrets (masked GET).
    setApiKey("");
    setWebhookSecret("");
    setSecretKey("");
  };

  useEffect(() => {
    async function loadConfig() {
      try {
        const { data, error } = await client.GET("/admin/commerce/payment-config");
        if (error) throw new Error(error.detail);
        if (data) {
          setConfigs(data as PaymentConfigRow[]);
          applyConfig((data as PaymentConfigRow[]).find((c) => c.gateway_type === "BILLPLZ"));
        }
      } catch {
        toast.error("Failed to load payment configuration.");
      } finally {
        setIsLoading(false);
      }
    }
    loadConfig();
  }, []);

  const handleGatewayChange = (type: GatewayType) => {
    setGatewayType(type);
    applyConfig(configs.find((c) => c.gateway_type === type));
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();

    if (gatewayType === "BILLPLZ") {
      if (!hasWebhookSecret && webhookSecret.trim().length !== 128) {
        toast.error("Invalid Configuration", {
          description: "Billplz X-Signature Key must be exactly 128 characters long."
        });
        return;
      }
      if (webhookSecret.trim() && webhookSecret.trim().length !== 128) {
        toast.error("Invalid Configuration", {
          description: "Billplz X-Signature Key must be exactly 128 characters long."
        });
        return;
      }
      if (!collectionId.trim()) {
        toast.error("Collection ID is required for Billplz.");
        return;
      }
      if (!hasApiKey && !apiKey.trim()) {
        toast.error("API Key is required for first-time Billplz configuration.");
        return;
      }
    }

    if (gatewayType === "CHIP") {
      if (!collectionId.trim()) {
        toast.error("Brand ID is required for CHIP Collect.");
        return;
      }
      if (!hasApiKey && !apiKey.trim()) {
        toast.error("API Key is required for first-time CHIP configuration.");
        return;
      }
    }

    if (gatewayType === "STRIPE") {
      if (!hasSecretKey && !secretKey.trim()) {
        toast.error("Secret Key is required for first-time Stripe configuration.");
        return;
      }
    }

    if (gatewayType === "RAZORPAY" && !hasApiKey && !apiKey.trim()) {
      toast.error("API Key is required for first-time Razorpay configuration.");
      return;
    }

    setIsSaving(true);
    try {
      const { error } = await client.PUT("/admin/commerce/payment-config", {
        body: {
          gateway_type: gatewayType,
          api_key: apiKey.trim() || undefined,
          secret_key: secretKey.trim() || undefined,
          webhook_secret: webhookSecret.trim() || undefined,
          collection_id: collectionId.trim() || undefined,
          is_active: isActive
        }
      });

      if (error) throw new Error(error.detail || "Failed to save configuration");

      setConfigs((prev) => {
        const existing = prev.find((c) => c.gateway_type === gatewayType);
        const next: PaymentConfigRow = {
          gateway_type: gatewayType,
          merchant_id: collectionId,
          is_active: isActive,
          has_api_key: hasApiKey || Boolean(apiKey.trim()) || Boolean(secretKey.trim()),
          has_secret_key: hasSecretKey || Boolean(secretKey.trim()) || Boolean(apiKey.trim()),
          has_webhook_secret: hasWebhookSecret || Boolean(webhookSecret.trim()),
          api_key_hint: apiKeyHint,
          secret_key_hint: secretKeyHint,
          webhook_secret_hint: webhookHint
        };
        if (existing) {
          return prev.map((c) => (c.gateway_type === gatewayType ? next : c));
        }
        return [...prev, next];
      });

      setHasApiKey((v) => v || Boolean(apiKey.trim()) || Boolean(secretKey.trim()));
      setHasSecretKey((v) => v || Boolean(secretKey.trim()) || Boolean(apiKey.trim()));
      setHasWebhookSecret((v) => v || Boolean(webhookSecret.trim()));
      setApiKey("");
      setWebhookSecret("");
      setSecretKey("");

      toast.success(`${gatewayType} credentials saved securely.`);
    } catch (err: any) {
      toast.error(err.message);
    } finally {
      setIsSaving(false);
    }
  };

  return (
    <PageLayout
      title="Payment Credential Vault"
      description="Securely store API keys for multiple payment gateways. You can route specific checkout links to different gateways."
      breadcrumbs={[{ label: "Workspace" }, { label: "Payment Gateways" }]}
    >
      <div className="bg-white border border-[#e5e5e5] rounded-none flex flex-col">
        {isLoading ? (
          <div className="p-12 flex justify-center">
            <Loader2 className="animate-spin text-[#a1a1aa]" />
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="flex flex-col">
            <div className="p-6 md:p-8 space-y-8">
              <div className="space-y-4">
                <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">
                  Target Provider
                </label>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-semibold text-[#09090b]">Gateway Type</label>
                    <select
                      value={gatewayType}
                      onChange={(e) => handleGatewayChange(e.target.value as GatewayType)}
                      className="w-full h-10 border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b]"
                    >
                      <option value="CHIP">CHIP Collect (Malaysia)</option>
                      <option value="BILLPLZ">Billplz (Malaysia)</option>
                      <option value="STRIPE">Stripe (Global)</option>
                      <option value="RAZORPAY">Razorpay (Global)</option>
                    </select>
                  </div>
                  <div className="space-y-1.5 flex items-end">
                    <label className="flex items-center gap-2 text-[12px] font-semibold text-[#09090b] cursor-pointer">
                      <input
                        type="checkbox"
                        checked={isActive}
                        onChange={(e) => setIsActive(e.target.checked)}
                        className="h-4 w-4"
                      />
                      Gateway active (soft-disable keeps credentials)
                    </label>
                  </div>
                </div>
              </div>

              <div className="space-y-4">
                <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">
                  Secure Credentials
                </label>

                {gatewayType === "CHIP" && (
                  <>
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-semibold text-[#09090b]">Brand ID</label>
                      <input
                        type="text"
                        value={collectionId}
                        onChange={(e) => setCollectionId(e.target.value)}
                        required
                        placeholder="e.g. 75a76529-..."
                        className="w-full h-10 border border-[#e5e5e5] px-3 font-mono text-[13px] focus:outline-none focus:border-[#09090b]"
                      />
                    </div>
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-semibold text-[#09090b]">
                        Secret Key (API Key){hasApiKey ? ` · stored ${apiKeyHint ?? ""}` : ""}
                      </label>
                      <input
                        type="password"
                        value={apiKey}
                        onChange={(e) => setApiKey(e.target.value)}
                        placeholder={hasApiKey ? "Leave blank to keep existing" : "••••••••"}
                        className="w-full h-10 border border-[#e5e5e5] px-3 font-mono text-[13px] focus:outline-none focus:border-[#09090b]"
                      />
                      <p className="text-[10px] text-[#a1a1aa] mt-1">
                        Lazuar will autonomously fetch your RSA Public Key and configure your webhook endpoints upon
                        saving a new key.
                      </p>
                    </div>
                  </>
                )}

                {gatewayType === "BILLPLZ" && (
                  <>
                    <div className="p-3 bg-amber-50 border border-amber-200 rounded-none text-[12px] text-amber-900 leading-relaxed">
                      <strong>Pay-link renewals.</strong> Billplz cannot vault. Each cycle we create a hosted bill and
                      email it. There is no silent auto-charge (subscription renewals, dunning AUTO_CHARGE). Use Stripe
                      or CHIP Collect when you need recurring auto-debit.
                    </div>
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-semibold text-[#09090b]">Collection ID</label>
                      <input
                        type="text"
                        value={collectionId}
                        onChange={(e) => setCollectionId(e.target.value)}
                        required
                        placeholder="e.g. qigic0ou"
                        className="w-full h-10 border border-[#e5e5e5] px-3 font-mono text-[13px] focus:outline-none focus:border-[#09090b]"
                      />
                    </div>
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-semibold text-[#09090b]">
                        API Key (Secret Key){hasApiKey ? ` · stored ${apiKeyHint ?? ""}` : ""}
                      </label>
                      <input
                        type="password"
                        value={apiKey}
                        onChange={(e) => setApiKey(e.target.value)}
                        placeholder={hasApiKey ? "Leave blank to keep existing" : "••••••••"}
                        className="w-full h-10 border border-[#e5e5e5] px-3 font-mono text-[13px] focus:outline-none focus:border-[#09090b]"
                      />
                    </div>
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-semibold text-[#09090b]">
                        X-Signature Key (Webhook Secret)
                        {hasWebhookSecret ? ` · stored ${webhookHint ?? ""}` : ""}
                      </label>
                      <input
                        type="password"
                        value={webhookSecret}
                        onChange={(e) => setWebhookSecret(e.target.value)}
                        placeholder={hasWebhookSecret ? "Leave blank to keep existing" : "128-character hex string"}
                        className="w-full h-10 border border-[#e5e5e5] px-3 font-mono text-[13px] focus:outline-none focus:border-[#09090b]"
                      />
                      <p className="text-[10px] text-[#a1a1aa] mt-1">Must be exactly 128 characters long for signature verification.</p>
                    </div>
                  </>
                )}

                {gatewayType === "STRIPE" && (
                  <>
                    <p className="text-[10px] text-[#a1a1aa] leading-relaxed">
                      Apple Pay and Google Pay appear on Stripe-hosted Checkout when the Stripe account can take cards
                      and the buyer’s device supports them. Enable cards in Stripe Dashboard → Payment methods. Not
                      available on Billplz. Domain verification is only needed if you host wallet buttons yourself
                      (Lazuar does not).
                    </p>
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-semibold text-[#09090b]">
                        Secret Key{hasSecretKey ? ` · stored ${secretKeyHint ?? ""}` : ""}
                      </label>
                      <input
                        type="password"
                        value={secretKey}
                        onChange={(e) => setSecretKey(e.target.value)}
                        placeholder={hasSecretKey ? "Leave blank to keep existing" : "sk_live_..."}
                        className="w-full h-10 border border-[#e5e5e5] px-3 font-mono text-[13px] focus:outline-none focus:border-[#09090b]"
                      />
                    </div>
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-semibold text-[#09090b]">
                        Webhook Signing Secret
                        {hasWebhookSecret ? ` · stored ${webhookHint ?? ""}` : ""}
                      </label>
                      <input
                        type="password"
                        value={webhookSecret}
                        onChange={(e) => setWebhookSecret(e.target.value)}
                        placeholder={hasWebhookSecret ? "Leave blank to keep existing" : "whsec_..."}
                        className="w-full h-10 border border-[#e5e5e5] px-3 font-mono text-[13px] focus:outline-none focus:border-[#09090b]"
                      />
                    </div>
                  </>
                )}

                {gatewayType === "RAZORPAY" && (
                  <>
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-semibold text-[#09090b]">
                        API Key (KeyId:KeySecret){hasApiKey ? ` · stored ${apiKeyHint ?? ""}` : ""}
                      </label>
                      <input
                        type="password"
                        value={apiKey}
                        onChange={(e) => setApiKey(e.target.value)}
                        placeholder={hasApiKey ? "Leave blank to keep existing" : "rzp_live_xxx:secret_yyy"}
                        className="w-full h-10 border border-[#e5e5e5] px-3 font-mono text-[13px] focus:outline-none focus:border-[#09090b]"
                      />
                      <p className="text-[10px] text-[#a1a1aa] mt-1">Format must be KeyId:KeySecret</p>
                    </div>
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-semibold text-[#09090b]">
                        Webhook Signing Secret
                        {hasWebhookSecret ? ` · stored ${webhookHint ?? ""}` : ""}
                      </label>
                      <input
                        type="password"
                        value={webhookSecret}
                        onChange={(e) => setWebhookSecret(e.target.value)}
                        placeholder={hasWebhookSecret ? "Leave blank to keep existing" : "Your custom webhook secret"}
                        className="w-full h-10 border border-[#e5e5e5] px-3 font-mono text-[13px] focus:outline-none focus:border-[#09090b]"
                      />
                    </div>
                  </>
                )}
              </div>
            </div>

            <div className="flex items-center justify-end p-5 border-t border-[#f4f4f5] bg-[#fafafa]/50 mt-auto">
              <button
                type="submit"
                disabled={isSaving}
                className="h-10 px-8 bg-[#09090b] text-white text-[11px] font-bold tracking-widest uppercase rounded-none hover:bg-[#27272a] disabled:opacity-50 transition-colors flex items-center gap-2"
              >
                {isSaving && <Loader2 size={13} className="animate-spin" />} Save Credentials
              </button>
            </div>
          </form>
        )}
      </div>
    </PageLayout>
  );
}
