import { useEffect, useState } from "react";
import { useSearchParams, useNavigate } from "react-router-dom";
import { client, OPS_URL, AUTH_URL } from "../lib/api-client";
import { Loader2, CheckCircle2, AlertCircle } from "lucide-react";

export default function AcceptInvitePage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const token = searchParams.get("token");
  
  const [status, setStatus] = useState<"processing" | "success" | "error">("processing");
  const [errorMsg, setErrorMsg] = useState("");

  useEffect(() => {
    if (!token) {
      setStatus("error");
      setErrorMsg("No invitation token provided in the URL.");
      return;
    }

    async function processInvite() {
      try {
        // Step 1: Check if user is logged in
        const authCheck = await client.GET("/one/auth/me");
        if (authCheck.error) {
          // Send them to login but ensure they come back here to accept the invite
          window.location.href = `${AUTH_URL}/login?returnUrl=${encodeURIComponent(window.location.href)}`;
          return;
        }

        // Step 2: Accept the invitation
        const { error } = await client.POST("/one/workspaces/invites/accept", {
          body: { token: token! }
        });

        if (error) throw new Error(error.detail);

        setStatus("success");

        // Step 3: Evaluate entitlements to route them
        const { data: entitlements } = await client.GET("/one/me/entitlements");
        
        setTimeout(() => {
          if (entitlements && entitlements.some(e => e.role === "ADMIN" || e.role === "SUPER_ADMIN" || e.role === "STAFF")) {
            window.location.href = OPS_URL;
          } else {
            navigate("/hub");
          }
        }, 1500);

      } catch (err: any) {
        setStatus("error");
        setErrorMsg(err.message || "Failed to accept the invitation. It may have expired.");
      }
    }

    processInvite();
  }, [token, navigate]);

  return (
    <div className="min-h-screen flex items-center justify-center bg-[#f5f5f5] p-4">
      <div className="bg-white border border-[#e5e5e5] p-8 max-w-sm w-full text-center flex flex-col items-center">
        {status === "processing" && (
          <>
            <Loader2 className="animate-spin text-[#09090b] mb-4" size={32} />
            <h2 className="text-[15px] font-bold text-[#09090b]">Verifying Invitation</h2>
            <p className="text-[12px] text-[#71717a] mt-2">Please wait while we secure your access...</p>
          </>
        )}

        {status === "success" && (
          <>
            <div className="h-12 w-12 rounded-full bg-emerald-50 flex items-center justify-center mb-4">
              <CheckCircle2 className="text-emerald-600" size={24} />
            </div>
            <h2 className="text-[15px] font-bold text-[#09090b]">Invitation Accepted</h2>
            <p className="text-[12px] text-[#71717a] mt-2">Redirecting to your workspace...</p>
          </>
        )}

        {status === "error" && (
          <>
            <div className="h-12 w-12 rounded-full bg-rose-50 flex items-center justify-center mb-4">
              <AlertCircle className="text-rose-600" size={24} />
            </div>
            <h2 className="text-[15px] font-bold text-[#09090b]">Verification Failed</h2>
            <p className="text-[12px] text-[#71717a] mt-2">{errorMsg}</p>
            <button 
              onClick={() => navigate("/hub")}
              className="mt-6 h-9 px-6 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest hover:bg-[#27272a] transition-colors"
            >
              Return to Hub
            </button>
          </>
        )}
      </div>
    </div>
  );
}
