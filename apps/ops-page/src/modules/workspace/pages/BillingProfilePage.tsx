import { useState, useRef, useEffect } from "react";
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { Loader2, Save, UploadCloud, Building, Image as ImageIcon } from "lucide-react";
import { toast } from "sonner";
import { client } from "../../../lib/api-client";
import PageLayout from "../../core/components/PageLayout";

export default function BillingProfilePage() {
  const queryClient = useQueryClient();
  const fileInputRef = useRef<HTMLInputElement>(null);

  const [legalName, setLegalName] = useState("");
  const [tin, setTin] = useState("");
  const [registrationNumber, setRegistrationNumber] = useState("");
  const [sstNumber, setSstNumber] = useState("");
  const [logoUrl, setLogoUrl] = useState("");

  const [addressLine1, setAddressLine1] = useState("");
  const [addressLine2, setAddressLine2] = useState("");
  const [city, setCity] = useState("");
  const [postalCode, setPostalCode] = useState("");
  const [stateCode, setStateCode] = useState("");
  const [countryCode, setCountryCode] = useState("MYS");

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
        setStateCode(profile.address.state_code || "");
        setCountryCode(profile.address.country_code || "MYS");
      }
    }
  }, [profile]);

  const handleFileUpload = async (e: React.ChangeEvent<HTMLInputElement>) => {
    const file = e.target.files?.[0];
    if (!file) return;

    if (!file.type.startsWith("image/")) {
      toast.error("Please upload an image file (PNG/JPG).");
      return;
    }

    setIsUploading(true);

    try {
      const { data, error } = await client.POST("/admin/vault/presigned-url", {
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
    },
    onError: (err: any) => toast.error(err.message || "Failed to save profile.")
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    updateMutation.mutate();
  };

  return (
    <PageLayout
      title="Legal & Billing Profile"
      description="Configure your official corporate identity used for LHDN compliance and generating customer tax invoices."
      breadcrumbs={[{ label: "Workspace" }, { label: "Legal & Billing" }]}
    >
      <div className="max-w-3xl bg-white border border-[#e5e5e5] rounded-none flex flex-col">
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
                    <label className="text-[11px] font-semibold text-[#09090b]">Legal Business Name *</label>
                    <input 
                      type="text" 
                      required 
                      value={legalName} 
                      onChange={(e) => setLegalName(e.target.value)} 
                      disabled={updateMutation.isPending} 
                      placeholder="e.g. Acme Solutions Sdn Bhd"
                      className="w-full h-10 border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50" 
                    />
                  </div>
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-semibold text-[#09090b]">Tax Identification Number (TIN) *</label>
                    <input 
                      type="text" 
                      required 
                      value={tin} 
                      onChange={(e) => setTin(e.target.value)} 
                      disabled={updateMutation.isPending} 
                      placeholder="e.g. C12345678"
                      className="w-full h-10 border border-[#e5e5e5] bg-white px-3 font-mono text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50" 
                    />
                  </div>
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-semibold text-[#09090b]">Business Registration No. (SSM)</label>
                    <input 
                      type="text" 
                      value={registrationNumber} 
                      onChange={(e) => setRegistrationNumber(e.target.value)} 
                      disabled={updateMutation.isPending} 
                      placeholder="e.g. 202401001234"
                      className="w-full h-10 border border-[#e5e5e5] bg-white px-3 font-mono text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50" 
                    />
                  </div>
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-semibold text-[#09090b]">SST Registration No. (Optional)</label>
                    <input 
                      type="text" 
                      value={sstNumber} 
                      onChange={(e) => setSstNumber(e.target.value)} 
                      disabled={updateMutation.isPending} 
                      placeholder="e.g. W10-1808-12345678"
                      className="w-full h-10 border border-[#e5e5e5] bg-white px-3 font-mono text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50" 
                    />
                  </div>
                </div>
              </div>

              <div className="space-y-4">
                <label className="text-[10px] font-bold uppercase tracking-widest text-[#71717a] block border-b border-[#f4f4f5] pb-1.5 flex items-center gap-2">
                  <ImageIcon size={13} /> Official Logo
                </label>
                <p className="text-[12px] text-[#71717a]">
                  This logo will be displayed on all formal quotations and LHDN tax invoices sent to your customers.
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
                    <label className="text-[11px] font-semibold text-[#09090b]">Address Line 1 *</label>
                    <input type="text" required value={addressLine1} onChange={(e) => setAddressLine1(e.target.value)} disabled={updateMutation.isPending} className="w-full h-10 border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50" />
                  </div>
                  <div className="space-y-1.5">
                    <label className="text-[11px] font-semibold text-[#09090b]">Address Line 2 (Optional)</label>
                    <input type="text" value={addressLine2} onChange={(e) => setAddressLine2(e.target.value)} disabled={updateMutation.isPending} className="w-full h-10 border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50" />
                  </div>
                  <div className="grid grid-cols-2 gap-4">
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-semibold text-[#09090b]">City *</label>
                      <input type="text" required value={city} onChange={(e) => setCity(e.target.value)} disabled={updateMutation.isPending} className="w-full h-10 border border-[#e5e5e5] bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50" />
                    </div>
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-semibold text-[#09090b]">Postal Code *</label>
                      <input type="text" required value={postalCode} onChange={(e) => setPostalCode(e.target.value)} disabled={updateMutation.isPending} className="w-full h-10 border border-[#e5e5e5] bg-white px-3 font-mono text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50" />
                    </div>
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-semibold text-[#09090b]">State Code *</label>
                      <input type="text" required value={stateCode} onChange={(e) => setStateCode(e.target.value)} disabled={updateMutation.isPending} placeholder="e.g. 14" className="w-full h-10 border border-[#e5e5e5] bg-white px-3 font-mono text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50" />
                    </div>
                    <div className="space-y-1.5">
                      <label className="text-[11px] font-semibold text-[#09090b]">Country Code *</label>
                      <input type="text" required value={countryCode} onChange={(e) => setCountryCode(e.target.value)} disabled={updateMutation.isPending} placeholder="e.g. MYS" className="w-full h-10 border border-[#e5e5e5] bg-white px-3 font-mono text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50" />
                    </div>
                  </div>
                </div>
              </div>

            </div>

            <div className="flex items-center justify-end p-5 border-t border-[#f4f4f5] bg-[#fafafa]/50 mt-auto">
              <button type="submit" disabled={updateMutation.isPending} className="h-10 px-8 bg-[#09090b] text-white text-[11px] font-bold tracking-widest uppercase rounded-none hover:bg-[#27272a] disabled:opacity-50 transition-colors flex items-center gap-2">
                {updateMutation.isPending ? <Loader2 size={13} className="animate-spin" /> : <Save size={13} />} Save Profile
              </button>
            </div>
          </form>
        )}
      </div>
    </PageLayout>
  );
}
