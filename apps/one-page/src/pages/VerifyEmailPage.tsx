import { useEffect, useState } from "react";
import { useSearchParams, useNavigate } from "react-router-dom";
import { Loader2 } from "lucide-react";
import { client } from "../lib/api-client";

export default function VerifyEmailPage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const [status, setStatus] = useState<"loading" | "success" | "error">("loading");

  useEffect(() => {
    async function verify() {
      const token = searchParams.get("token");
      if (!token) {
        setStatus("error");
        return;
      }
      
      const { error } = await client.POST("/one/auth/verify-email", {
        body: { token }
      });

      if (error) {
        setStatus("error");
      } else {
        setStatus("success");
        setTimeout(() => navigate("/profile"), 2000);
      }
    }
    verify();
  }, [searchParams, navigate]);

  return (
    <div className="flex h-screen w-full items-center justify-center bg-[#f5f5f5] font-sans">
      <div className="w-full max-w-[380px] mx-4 bg-white border border-[#e5e5e5] p-8 text-center">
        {status === "loading" && <div className="flex flex-col items-center"><Loader2 className="animate-spin text-[#a1a1aa] mb-4" /><p className="text-[13px] text-[#71717a]">Verifying your email...</p></div>}
        {status === "error" && <p className="text-[13px] text-rose-600">The verification link is invalid or expired. Please request a new one from your profile.</p>}
        {status === "success" && <p className="text-[13px] text-emerald-600 font-medium">Email verified successfully. Redirecting...</p>}
      </div>
    </div>
  );
}
