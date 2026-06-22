import { useState } from "react";
import { useSearchParams, useNavigate } from "react-router-dom";
import { Loader2 } from "lucide-react";
import { client, OPS_URL } from "../lib/api-client";

export default function AcceptInvitePage() {
  const [searchParams] = useSearchParams();
  const navigate = useNavigate();
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState("");

  const token = searchParams.get("token");

  if (!token) {
    return <div className="flex h-screen items-center justify-center text-sm">Invalid invitation link.</div>;
  }

  const handleAccept = async () => {
    setIsLoading(true);
    setError("");

    const { error: authError } = await client.GET("/one/auth/me");
    if (authError) {
      navigate(`/login?returnUrl=/accept-invite?token=${token}`);
      return;
    }

    const { error: acceptError } = await client.POST("/one/workspaces/invites/accept", {
      body: { token }
    });

    if (acceptError) {
      setError(acceptError.detail || "Failed to accept invitation.");
      setIsLoading(false);
    } else {
      window.location.href = OPS_URL;
    }
  };

  return (
    <div className="flex h-screen w-full items-center justify-center bg-[#f5f5f5] font-sans">
      <div className="w-full max-w-[380px] mx-4 bg-white border border-[#e5e5e5] p-8 text-center rounded-none">
        <h1 className="text-xl font-semibold tracking-tight text-[#09090b] mb-2">Workspace Invitation</h1>
        <p className="text-[13px] text-[#71717a] mb-8">You have been invited to join a workspace.</p>
        
        {error && <div className="mb-6 p-4 bg-rose-50 border border-rose-200 text-[10px] font-bold tracking-wide uppercase text-rose-600">{error}</div>}

        <button onClick={handleAccept} disabled={isLoading} className="w-full h-11 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest rounded-none flex items-center justify-center hover:bg-[#27272a] disabled:opacity-50 transition-colors">
          {isLoading ? <Loader2 size={16} className="animate-spin" /> : "Accept Invitation"}
        </button>
      </div>
    </div>
  );
}
