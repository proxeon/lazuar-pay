import { useState, useRef, useEffect } from "react";
import { useOutletContext } from "react-router-dom";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2, Save, UploadCloud, Building, Image as ImageIcon, Shield } from "lucide-react";
import { toast } from "sonner";
import { client, type components } from "../../../lib/api-client";
import PageLayout from "../../core/components/PageLayout";

type LhdnConfig = components["schemas"]["Lhdn.LhdnTenantConfigDto"];

const MY_STATE_CODES = [
  { code: "01", label: "Johor" },
  { code: "02", label: "Kedah" },
  { code: "03", label: "Kelantan" },
  { code: "04", label: "Melaka" },
  { code: "05", label: "Negeri Sembilan" },
  { code: "06", label: "Pahang" },
  { code: "07", label: "Pulau Pinang" },
  { code: "08", label: "Perak" },
  { code: "09", label: "Perlis" },
  { code: "10", label: "Selangor" },
  { code: "11", label: "Terengganu" },
  { code: "12", label: "Sabah" },
  { code: "13", label: "Sarawak" },
  { code: "14", label: "W.P. Kuala Lumpur" },
  { code: "15", label: "W.P. Labuan" },
  { code: "16", label: "W.P. Putrajaya" },
  { code: "17", label: "Not applicable" },
];

const ID_TYPES = ["BRN", "NRIC", "PASSPORT", "ARMY"] as const;

async function fileToBase64(file: File): Promise<string> {
  const buf = await file.arrayBuffer();
  const bytes = new Uint8Array(buf);
  let binary = "";
  for (let i = 0; i < bytes.length; i++) {
    binary += String.fromCharCode(bytes[i]);
  }
  return btoa(binary);
}

export default function BillingProfilePage() {
  const { activeWorkspaceId } = useOutletContext<{ activeWorkspaceId: string }>();
  const queryClient = useQueryClient();
  const fileInputRef = useRef<HTMLInputElement>(null);
  const certInputRef = useRef<HTMLInputElement>(null);

  const [legalName, setLegalName] = useState("");
  const [tin, setTin] = useState("");
  const [registrationNumber, setRegistrationNumber] = useState("");
  const [sstNumber, setSstNumber] = useState("");
  const [logoUrl, setLogoUrl] = useState("");

  const [addressLine1, setAddressLine1] = useState("");
  const [addressLine2, setAddressLine2] = useState("");
  const [city, setCity] = useState("");
  const [postalCode, setPostalCode] = useState("");
  const [stateCode, setStateCode] = useState("14");
  const [countryCode, setCountryCode] = useState("MYS");

  const [sameAsStationery, setSameAsStationery] = useState(true);
  const [supplierTin, setSupplierTin] = useState("");
  const [idType, setIdType] = useState<string>("BRN");
  const [idValue, setIdValue] = useState("");
  const [environment, setEnvironment] = useState<"SANDBOX" | "PROD">("SANDBOX");
  const [msicCode, setMsicCode] = useState("");
  const [intermediaryMode, setIntermediaryMode] = useState(false);
  const [clientId, setClientId] = useState("");
  const [clientSecret, setClientSecret] = useState("");
  const [lhdnLegalName, setLhdnLegalName] = useState("");
  const [lhdnAddressLine1, setLhdnAddressLine1] = useState("");
  const [lhdnCity, setLhdnCity] = useState("");
  const [lhdnState, setLhdnState] = useState("14");
  const [lhdnPostal, setLhdnPostal] = useState("");
  const [lhdnCountry, setLhdnCountry] = useState("MYS");
  const [certPassphrase, setCertPassphrase] = useState("");
  const [pendingCertBase64, setPendingCertBase64] = useState("");
  const [pendingCertName, setPendingCertName] = useState("");

  const [isUploading, setIsUploading] = useState(false);

  const { data: profile, isLoading } = useQuery({
    queryKey: ["billing-profile"],
    queryFn: async () => {
      const { data, error, response } = await client.GET("/admin/billing/profile");
      if (response.status === 404) return null;
      if (error) throw new Error(error.detail);
      return data;
    }
  });

  const { data: lhdnConfig, isLoading: isLhdnLoading } = useQuery({
    queryKey: ["lhdn-config", activeWorkspaceId],
    queryFn: async () => {
      const { data, error, response } = await client.GET("/lhdn/workspaces/{id}/lhdn-config", {
        params: { path: { id: activeWorkspaceId } }
      });
      if (response.status === 404) return null;
      if (error) throw new Error(error.detail);
      return data as LhdnConfig;
    },
    enabled: !!activeWorkspaceId
  });

  useEffect(() => {
    if (profile) {
      setLegalName(profile.legal_name || "");
      setTin(profile.tin || "");
      setRegistrationNumber(profile.registration_number || "");
      setSstNumber(profile.sst_registration_number || "");
      setLogoUrl(profile.logo_url || "");

      if (profile.address) {
        setAddressLine1(profile.address.line1 || "");
        setAddressLine2(profile.address.line2 || "");
        setCity(profile.address.city || "");
        setPostalCode(profile.address.postal_code || "");
        setStateCode(profile.address.state_code || "14");
        setCountryCode(profile.address.country_code || "MYS");
      }
    }
  }, [profile]);

  useEffect(() => {
    if (lhdnConfig) {
      setSupplierTin(lhdnConfig.supplier_tin || "");
      setIdType(lhdnConfig.id_type || "BRN");
      setIdValue(lhdnConfig.id_value || "");
      setEnvironment(lhdnConfig.environment === "PROD" ? "PROD" : "SANDBOX");
      setMsicCode(lhdnConfig.msic_code || "");
      setIntermediaryMode(lhdnConfig.intermediary_mode ?? false);
      setClientId(lhdnConfig.myinvois_client_id || "");
      setClientSecret("");
      setLhdnLegalName(lhdnConfig.legal_name || "");
      setLhdnAddressLine1(lhdnConfig.address_line1 || "");
      setLhdnCity(lhdnConfig.city || "");
      setLhdnState(lhdnConfig.state || "14");
      setLhdnPostal(lhdnConfig.postal || "");
      setLhdnCountry(lhdnConfig.country || "MYS");
    }
  }, [lhdnConfig]);

  const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (!file.type.startsWith("image/")) {
      toast.error("Please upload an image file (PNG/JPG).");
      return;
    }

    setIsUploading(true);

    try {
      const { data, error } = await client.POST("/one/storage/presigned-url", {
        body: {
          file_name: file.name,
          content_type: file.type
        }
      });

      if (error || !data) throw new Error(error?.detail || "Failed to generate secure upload link.");

      const uploadResponse = await fetch(data.upload_url, {
        method: "PUT",
        body: file,
        headers: {
          "Content-Type": file.type
        }
      });

      if (!uploadResponse.ok) {
        throw new Error("Failed to upload image. Check CORS configuration.");
      }

      setLogoUrl(data.final_url);
      toast.success("Logo uploaded successfully.");
    } catch (err: any) {
      toast.error(err.message);
    } finally {
      setIsUploading(false);
    }
  };

  const handleCertSelect = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;
    try {
      const base64 = await fileToBase64(file);
      setPendingCertBase64(base64);
      setPendingCertName(file.name);
    } catch {
      toast.error("Could not read certificate file.");
    }
  };

  const updateMutation = useMutation({
    mutationFn: async () => {
      const { error } = await client.PUT("/admin/billing/profile", {
        body: {
          legal_name: legalName.trim(),
          tin: tin.trim(),
          registration_number: registrationNumber.trim() || undefined,
          sst_registration_number: sstNumber.trim() || undefined,
          logo_url: logoUrl.trim() || undefined,
          address: {
            line1: addressLine1.trim(),
            line2: addressLine2.trim() || undefined,
            city: city.trim(),
            postal_code: postalCode.trim(),
            state_code: stateCode.trim(),
            country_code: countryCode.trim()
          }
        }
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: () => {
      toast.success("Billing profile saved successfully.");
      queryClient.invalidateQueries({ queryKey: ["billing-profile"] });
      queryClient.invalidateQueries({ queryKey: ["lhdn-config", activeWorkspaceId] });
    },
    onError: (err: any) => toast.error(err.message || "Failed to save profile.")
  });

  const lhdnMutation = useMutation({
    mutationFn: async () => {
      const useStationery = sameAsStationery;
      const { error } = await client.PUT("/lhdn/workspaces/{id}/lhdn-config", {
        params: { path: { id: activeWorkspaceId } },
        body: {
          supplier_tin: (useStationery ? tin : supplierTin).trim(),
          id_type: idType,
          id_value: idValue.trim(),
          environment,
          msic_code: msicCode.trim() || undefined,
          intermediary_mode: intermediaryMode,
          myinvois_client_id: clientId.trim() || undefined,
          myinvois_client_secret: clientSecret.trim() || undefined,
          legal_name: (useStationery ? legalName : lhdnLegalName).trim() || undefined,
          address_line1: (useStationery ? addressLine1 : lhdnAddressLine1).trim() || undefined,
          city: (useStationery ? city : lhdnCity).trim() || undefined,
          state: (useStationery ? stateCode : lhdnState).trim() || undefined,
          postal: (useStationery ? postalCode : lhdnPostal).trim() || undefined,
          country: (useStationery ? countryCode : lhdnCountry).trim() || undefined
        }
      });
      if (error) throw new Error(error.detail);

      if (pendingCertBase64) {
        if (!certPassphrase.trim()) {
          throw new Error("Certificate passphrase is required to store the .p12 file.");
        }
        const certResult = await client.PUT("/lhdn/workspaces/{id}/lhdn-certificate", {
          params: { path: { id: activeWorkspaceId } },
          body: {
            p12_base64_file: pendingCertBase64,
            passphrase: certPassphrase
          }
        });
        if (certResult.error) throw new Error(certResult.error.detail);
      }
    },
    onSuccess: () => {
      toast.success("MyInvois settings saved.");
      setClientSecret("");
      setCertPassphrase("");
      setPendingCertBase64("");
      setPendingCertName("");
      queryClient.invalidateQueries({ queryKey: ["lhdn-config", activeWorkspaceId] });
    },
    onError: (err: any) => toast.error(err.message || "Failed to save MyInvois settings.")
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    updateMutation.mutate();
  };

  const handleLhdnSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    lhdnMutation.mutate();
  };

  const inputClass = "w-full h-10 border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50";
  const labelClass = "text-[11px] font-semibold text-[#09090b]";

  return (
    <PageLayout
      title="Legal & Billing Profile"
      description="Supplier identity for tax invoices and MyInvois. Hosted checkout branding stays on General Settings."
      breadcrumbs={[{ label: "Workspace" }, { label: "Legal & Billing" }]}
    >
      <div className="max-w-3xl space-y-6">
        <div className="bg-white border border-[#e5e5e5] rounded-none flex flex-col">
          {isLoading ? (
            <div className="p-12 flex justify-center"><Loader2 className="animate-spin text-[#a1a1aa]" /></div>
          ) : (
            <form onSubmit={handleSubmit} className="flex flex-col">
              <div className="p-6 md:p-8 space-y-8">

                <div className="space-y-4">
                  <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5 flex items-center gap-2">
                    <Building size={13} /> Corporate Identity
                  </label>
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <div className="space-y-1.5">
                      <label className={labelClass}>Legal Business Name *</label>
                      <input
                        type="text"
                        required
                        value={legalName}
                        onChange={(e) => setLegalName(e.target.value)}
                        disabled={updateMutation.isPending}
                        placeholder="e.g. Acme Solutions Sdn Bhd"
                        className={inputClass}
                      />
                    </div>
                    <div className="space-y-1.5">
                      <label className={labelClass}>Tax Identification Number (TIN) *</label>
                      <input
                        type="text"
                        required
                        value={tin}
                        onChange={(e) => setTin(e.target.value)}
                        disabled={updateMutation.isPending}
                        placeholder="e.g. C12345678"
                        className={`${inputClass} font-mono`}
                      />
                    </div>
                    <div className="space-y-1.5">
                      <label className={labelClass}>Business Registration No. (SSM)</label>
                      <input
                        type="text"
                        value={registrationNumber}
                        onChange={(e) => setRegistrationNumber(e.target.value)}
                        disabled={updateMutation.isPending}
                        placeholder="e.g. 202401001234"
                        className={`${inputClass} font-mono`}
                      />
                    </div>
                    <div className="space-y-1.5">
                      <label className={labelClass}>SST Registration No. (Optional)</label>
                      <input
                        type="text"
                        value={sstNumber}
                        onChange={(e) => setSstNumber(e.target.value)}
                        disabled={updateMutation.isPending}
                        placeholder="e.g. W10-1808-12345678"
                        className={`${inputClass} font-mono`}
                      />
                    </div>
                  </div>
                </div>

                <div className="space-y-4">
                  <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5 flex items-center gap-2">
                    <ImageIcon size={13} /> Official Logo
                  </label>
                  <p className="text-[12px] text-[#71717a]">
                    This logo is used on quotations and tax invoices. Checkout branding is configured under General Settings.
                  </p>
                  <div className="flex flex-col gap-3 p-6 border border-dashed border-[#a1a1aa] bg-[#fafafa] rounded-sm text-center items-center justify-center relative hover:bg-[#f4f4f5] transition-colors group cursor-pointer max-w-sm">
                    <input
                      type="file"
                      accept="image/*"
                      ref={fileInputRef}
                      onChange={handleFileUpload}
                      className="absolute inset-0 w-full h-full opacity-0 cursor-pointer disabled:cursor-not-allowed"
                      disabled={isUploading || updateMutation.isPending}
                    />
                    {isUploading ? (
                      <>
                        <Loader2 className="animate-spin text-[#a1a1aa] mb-2" size={28} />
                        <span className="text-[11px] font-medium text-[#71717a]">Uploading...</span>
                      </>
                    ) : logoUrl ? (
                      <>
                        <img src={logoUrl} alt="Company Logo" className="max-h-20 object-contain mb-2" />
                        <span className="text-[11px] font-medium text-emerald-700">Logo saved. Click to replace.</span>
                      </>
                    ) : (
                      <>
                        <UploadCloud className="text-[#a1a1aa] group-hover:text-[#09090b] transition-colors mb-2" size={28} />
                        <span className="text-[11px] font-medium text-[#71717a] group-hover:text-[#09090b]">Click to upload PNG or JPG</span>
                      </>
                    )}
                  </div>
                </div>

                <div className="space-y-4">
                  <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">Registered Address</label>
                  <div className="space-y-4">
                    <div className="space-y-1.5">
                      <label className={labelClass}>Address Line 1 *</label>
                      <input type="text" required value={addressLine1} onChange={(e) => setAddressLine1(e.target.value)} disabled={updateMutation.isPending} className={inputClass} />
                    </div>
                    <div className="space-y-1.5">
                      <label className={labelClass}>Address Line 2 (Optional)</label>
                      <input type="text" value={addressLine2} onChange={(e) => setAddressLine2(e.target.value)} disabled={updateMutation.isPending} className={inputClass} />
                    </div>
                    <div className="grid grid-cols-2 gap-4">
                      <div className="space-y-1.5">
                        <label className={labelClass}>City *</label>
                        <input type="text" required value={city} onChange={(e) => setCity(e.target.value)} disabled={updateMutation.isPending} className={inputClass} />
                      </div>
                      <div className="space-y-1.5">
                        <label className={labelClass}>Postal Code *</label>
                        <input type="text" required value={postalCode} onChange={(e) => setPostalCode(e.target.value)} disabled={updateMutation.isPending} className={`${inputClass} font-mono`} />
                      </div>
                      <div className="space-y-1.5">
                        <label className={labelClass}>State *</label>
                        <select required value={stateCode} onChange={(e) => setStateCode(e.target.value)} disabled={updateMutation.isPending} className={inputClass}>
                          {MY_STATE_CODES.map((s) => (
                            <option key={s.code} value={s.code}>{s.code} — {s.label}</option>
                          ))}
                        </select>
                      </div>
                      <div className="space-y-1.5">
                        <label className={labelClass}>Country Code *</label>
                        <input type="text" required value={countryCode} onChange={(e) => setCountryCode(e.target.value)} disabled={updateMutation.isPending} placeholder="e.g. MYS" className={`${inputClass} font-mono`} />
                      </div>
                    </div>
                  </div>
                </div>

              </div>

              <div className="flex items-center justify-end p-5 border-t border-[#f4f4f5] bg-[#fafafa]/50 mt-auto">
                <button type="submit" disabled={updateMutation.isPending} className="h-10 px-8 bg-[#09090b] text-white text-[11px] font-bold tracking-widest uppercase rounded-none hover:bg-[#27272a] disabled:opacity-50 transition-colors flex items-center gap-2">
                  {updateMutation.isPending ? <Loader2 size={13} className="animate-spin" /> : <Save size={13} />} Save Stationery
                </button>
              </div>
            </form>
          )}
        </div>

        <div className="bg-white border border-[#e5e5e5] rounded-none flex flex-col">
          {isLhdnLoading ? (
            <div className="p-12 flex justify-center"><Loader2 className="animate-spin text-[#a1a1aa]" /></div>
          ) : (
            <form onSubmit={handleLhdnSubmit} className="flex flex-col">
              <div className="p-6 md:p-8 space-y-8">
                <div className="space-y-4">
                  <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5 flex items-center gap-2">
                    <Shield size={13} /> MyInvois (LHDN)
                  </label>
                  <p className="text-[12px] text-[#71717a] leading-relaxed">
                    Supplier TIN and address on UBL invoices come from this card. Saving stationery copies name, TIN, and address here when a config already exists. Client secret is never shown.
                  </p>
                  <label className="flex items-center gap-2 cursor-pointer w-fit">
                    <input
                      type="checkbox"
                      checked={sameAsStationery}
                      onChange={(e) => setSameAsStationery(e.target.checked)}
                      disabled={lhdnMutation.isPending}
                      className="rounded-sm border-[#e5e5e5] text-[#09090b] focus:ring-[#09090b]"
                    />
                    <span className="text-[12px] font-medium text-[#09090b]">Same as stationery</span>
                  </label>
                </div>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div className="space-y-1.5">
                    <label className={labelClass}>Supplier TIN *</label>
                    <input
                      type="text"
                      required={!sameAsStationery}
                      value={sameAsStationery ? tin : supplierTin}
                      onChange={(e) => setSupplierTin(e.target.value)}
                      disabled={lhdnMutation.isPending || sameAsStationery}
                      className={`${inputClass} font-mono`}
                    />
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelClass}>ID Type *</label>
                    <select required value={idType} onChange={(e) => setIdType(e.target.value)} disabled={lhdnMutation.isPending} className={inputClass}>
                      {ID_TYPES.map((t) => <option key={t} value={t}>{t}</option>)}
                    </select>
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelClass}>ID Value (BRN / NRIC) *</label>
                    <div className="flex gap-2">
                      <input type="text" required value={idValue} onChange={(e) => setIdValue(e.target.value)} disabled={lhdnMutation.isPending} className={`${inputClass} font-mono`} />
                      <button
                        type="button"
                        disabled={lhdnMutation.isPending || !supplierTin && !tin}
                        onClick={async () => {
                          try {
                            const { data, error } = await client.POST("/lhdn/taxpayer/validate", {
                              body: {
                                tin: (sameAsStationery ? tin : supplierTin).trim(),
                                id_type: idType as "BRN" | "NRIC" | "PASSPORT" | "ARMY",
                                id_value: idValue.trim(),
                              }
                            });
                            if (error) throw new Error(error.detail);
                            toast.success(data.is_valid ? `Valid${data.taxpayer_name ? `: ${data.taxpayer_name}` : ""}` : "TIN / ID pair is not valid.");
                          } catch (err: any) {
                            toast.error(err.message || "TIN check failed.");
                          }
                        }}
                        className="h-10 px-3 border border-[#e5e5e5] bg-white text-[10px] font-bold uppercase tracking-widest shrink-0"
                      >
                        Check TIN
                      </button>
                    </div>
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelClass}>Environment *</label>
                    <select value={environment} onChange={(e) => setEnvironment(e.target.value as "SANDBOX" | "PROD")} disabled={lhdnMutation.isPending} className={inputClass}>
                      <option value="SANDBOX">Sandbox</option>
                      <option value="PROD">Production</option>
                    </select>
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelClass}>MSIC Code</label>
                    <input type="text" value={msicCode} onChange={(e) => setMsicCode(e.target.value)} disabled={lhdnMutation.isPending} placeholder="e.g. 62010" className={`${inputClass} font-mono`} />
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelClass}>Legal name</label>
                    <input
                      type="text"
                      value={sameAsStationery ? legalName : lhdnLegalName}
                      onChange={(e) => setLhdnLegalName(e.target.value)}
                      disabled={lhdnMutation.isPending || sameAsStationery}
                      className={inputClass}
                    />
                  </div>
                  <div className="space-y-1.5 md:col-span-2">
                    <label className={labelClass}>Address line 1</label>
                    <input
                      type="text"
                      value={sameAsStationery ? addressLine1 : lhdnAddressLine1}
                      onChange={(e) => setLhdnAddressLine1(e.target.value)}
                      disabled={lhdnMutation.isPending || sameAsStationery}
                      className={inputClass}
                    />
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelClass}>City</label>
                    <input
                      type="text"
                      value={sameAsStationery ? city : lhdnCity}
                      onChange={(e) => setLhdnCity(e.target.value)}
                      disabled={lhdnMutation.isPending || sameAsStationery}
                      className={inputClass}
                    />
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelClass}>Postal</label>
                    <input
                      type="text"
                      value={sameAsStationery ? postalCode : lhdnPostal}
                      onChange={(e) => setLhdnPostal(e.target.value)}
                      disabled={lhdnMutation.isPending || sameAsStationery}
                      className={`${inputClass} font-mono`}
                    />
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelClass}>State</label>
                    <select
                      value={sameAsStationery ? stateCode : lhdnState}
                      onChange={(e) => setLhdnState(e.target.value)}
                      disabled={lhdnMutation.isPending || sameAsStationery}
                      className={inputClass}
                    >
                      {MY_STATE_CODES.map((s) => (
                        <option key={s.code} value={s.code}>{s.code} — {s.label}</option>
                      ))}
                    </select>
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelClass}>Country</label>
                    <input
                      type="text"
                      value={sameAsStationery ? countryCode : lhdnCountry}
                      onChange={(e) => setLhdnCountry(e.target.value)}
                      disabled={lhdnMutation.isPending || sameAsStationery}
                      className={`${inputClass} font-mono`}
                    />
                  </div>
                </div>

                <label className="flex items-center gap-2 cursor-pointer w-fit">
                  <input
                    type="checkbox"
                    checked={intermediaryMode}
                    onChange={(e) => setIntermediaryMode(e.target.checked)}
                    disabled={lhdnMutation.isPending}
                    className="rounded-sm border-[#e5e5e5] text-[#09090b] focus:ring-[#09090b]"
                  />
                  <span className="text-[12px] font-medium text-[#09090b]">Intermediary mode</span>
                </label>

                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                  <div className="space-y-1.5">
                    <label className={labelClass}>MyInvois client ID</label>
                    <input type="text" value={clientId} onChange={(e) => setClientId(e.target.value)} disabled={lhdnMutation.isPending} className={`${inputClass} font-mono`} />
                  </div>
                  <div className="space-y-1.5">
                    <label className={labelClass}>MyInvois client secret</label>
                    <input
                      type="password"
                      value={clientSecret}
                      onChange={(e) => setClientSecret(e.target.value)}
                      disabled={lhdnMutation.isPending}
                      placeholder={lhdnConfig?.has_client_secret ? `Stored${lhdnConfig.client_secret_hint ? ` (${lhdnConfig.client_secret_hint})` : ""} — leave blank to keep` : "Paste secret"}
                      className={`${inputClass} font-mono`}
                    />
                  </div>
                </div>

                <div className="space-y-3">
                  <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5">Signing certificate</label>
                  <p className="text-[12px] text-[#71717a]">
                    Certificate on file: {lhdnConfig?.has_certificate ? "yes" : "no"}.
                    {" "}
                    Submissions: {lhdnConfig?.has_certificate && lhdnConfig.signing === "Auto"
                      ? "signed JSON v1.1 when Auto is on"
                      : "unsigned v1.0. Signed v1.1 only if a .p12 is stored and Lhdn:Signing=Auto."}
                  </p>
                  <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <div className="space-y-1.5">
                      <label className={labelClass}>.p12 file</label>
                      <input
                        type="file"
                        accept=".p12,.pfx"
                        ref={certInputRef}
                        onChange={handleCertSelect}
                        disabled={lhdnMutation.isPending}
                        className="block w-full text-[12px] text-[#71717a] file:mr-3 file:h-8 file:px-3 file:border file:border-[#e5e5e5] file:bg-white file:text-[11px] file:font-bold file:uppercase file:tracking-widest"
                      />
                      {pendingCertName && <p className="text-[11px] text-emerald-700">{pendingCertName} ready to upload.</p>}
                    </div>
                    <div className="space-y-1.5">
                      <label className={labelClass}>Certificate passphrase</label>
                      <input
                        type="password"
                        value={certPassphrase}
                        onChange={(e) => setCertPassphrase(e.target.value)}
                        disabled={lhdnMutation.isPending}
                        className={inputClass}
                      />
                    </div>
                  </div>
                </div>
              </div>

              <div className="flex items-center justify-end p-5 border-t border-[#f4f4f5] bg-[#fafafa]/50 mt-auto">
                <button type="submit" disabled={lhdnMutation.isPending || !activeWorkspaceId} className="h-10 px-8 bg-[#09090b] text-white text-[11px] font-bold tracking-widest uppercase rounded-none hover:bg-[#27272a] disabled:opacity-50 transition-colors flex items-center gap-2">
                  {lhdnMutation.isPending ? <Loader2 size={13} className="animate-spin" /> : <Save size={13} />} Save MyInvois
                </button>
              </div>
            </form>
          )}
        </div>
      </div>
    </PageLayout>
  );
}
