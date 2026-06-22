import { useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { Loader2 } from "lucide-react";
import { client } from "../lib/api-client";

export default function ResetPasswordPage() {
  const [searchParams] = useSearchParams();
  const [isLoading, setIsLoading] = useState(false);
  const [success, setSuccess] = useState(false);
  const [error, setError] = useState("");
  const [password, setPassword] = useState("");

  const email = searchParams.get("email");
  const token = searchParams.get("token");

  if (!email || !token) {
    return <div className="flex h-screen items-center justify-center text-sm">Invalid password reset link.</div>;
  }

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    setError("");

    const { error: apiError } = await client.POST("/one/auth/reset-password", {
      body: { email, token, new_password: password }
    });

    if (apiError) {
      setError(apiError.detail || "Failed to reset password.");
    } else {
      setSuccess(true);
    }
    setIsLoading(false);
  };

  return (
    <div className="flex h-screen w-full items-center justify-center bg-[#f5f5f5] font-sans">
      <div className="w-full max-w-[380px] mx-4 bg-white border border-[#e5e5e5] p-8 rounded-none">
        <div className="text-center mb-8">
          <h1 className="text-xl font-semibold tracking-tight text-[#09090b]">Set New Password</h1>
        </div>

        {error && <div className="mb-6 p-4 bg-rose-50 border border-rose-200 text-[10px] font-bold tracking-wide uppercase text-rose-600">{error}</div>}

        {success ? (
          <div className="text-center space-y-4">
            <p className="text-[13px] text-[#71717a]">Your password has been updated securely.</p>
            <Link to="/login" className="inline-block mt-4 text-[11px] font-bold uppercase tracking-widest text-[#09090b] hover:underline">Proceed to Login</Link>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">New Password</label>
              <input type="password" required value={password} onChange={e => setPassword(e.target.value)} className="flex h-11 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm focus:outline-none focus:border-[#09090b]" />
            </div>
            <button type="submit" disabled={isLoading} className="w-full h-11 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest rounded-none flex items-center justify-center hover:bg-[#27272a] disabled:opacity-50 transition-colors mt-2">
              {isLoading ? <Loader2 size={16} className="animate-spin" /> : "Update Password"}
            </button>
          </form>
        )}
      </div>
    </div>
  );
}
