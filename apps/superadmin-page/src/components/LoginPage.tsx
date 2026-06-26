import { useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { Loader2 } from "lucide-react";
import { client } from "../lib/api-client";

export default function LoginPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");

  const handleLoginSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    setError("");

    try {
      const { error: apiError } = await client.POST("/one/auth/login", {
        body: { email, password }
      });

      if (apiError) throw new Error(apiError.detail || "Invalid credentials.");

      const returnUrl = searchParams.get("returnUrl");
      if (returnUrl) {
        window.location.href = returnUrl;
      } else {
        window.location.href = "/platform/gateways";
      }
    } catch (err: any) {
      setError(err.message || "Invalid credentials.");
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="flex h-screen w-full items-center justify-center bg-[#f5f5f5] font-sans">
      <div className="w-full max-w-[380px] mx-4 animate-in fade-in zoom-in-95 duration-300">
        <div className="bg-white border border-[#e5e5e5] p-8 rounded-none">
          {error && (
            <div className="mb-6 p-4 bg-rose-50 border border-rose-200">
              <p className="text-[10px] font-bold tracking-wide uppercase text-rose-600">{error}</p>
            </div>
          )}

          <div className="text-center mb-8">
            <h1 className="text-xl font-semibold tracking-tight text-[#09090b]">Platform Admin</h1>
            <p className="text-[13px] text-[#71717a] mt-1.5">Sign in to the global control plane.</p>
          </div>

          <form onSubmit={handleLoginSubmit} className="space-y-4">
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Root Email</label>
              <input type="email" required value={email} onChange={(e) => setEmail(e.target.value)} className="flex h-11 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]" placeholder="admin@lazuar.com" />
            </div>

            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Password</label>
              <input type="password" required value={password} onChange={(e) => setPassword(e.target.value)} className="flex h-11 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]" placeholder="••••••••" />
            </div>

            <button type="submit" disabled={isLoading} className="w-full h-11 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest rounded-none flex items-center justify-center hover:bg-[#27272a] disabled:opacity-50 transition-colors mt-2">
              {isLoading ? <Loader2 size={16} className="animate-spin" /> : "Sign In"}
            </button>
          </form>
        </div>
      </div>
    </div>
  );
}
