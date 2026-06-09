import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Loader2, ArrowLeft, Mail } from "lucide-react";
import { client } from "../lib/api-client";

type AuthMode = "password" | "register" | "magic_link";

export default function LoginPage() {
  const navigate = useNavigate();
  const [mode, setMode] = useState<AuthMode>("password");
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState("");
  
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");

  async function handleLoginSubmit(e: React.FormEvent) {
    e.preventDefault();
    setIsLoading(true);
    setError("");

    try {
      const { data, error: apiError } = await client.POST("/one/auth/login", {
        body: { email, password }
      });

      if (apiError) throw new Error(apiError.detail || "Invalid credentials.");

      if (data && data.user) {
        // Handle "Google-style" SSO return URL logic
        const searchParams = new URLSearchParams(window.location.search);
        const returnUrl = searchParams.get("returnUrl");
        
        if (returnUrl) {
          window.location.href = returnUrl;
        } else {
          navigate("/launchpad");
        }
      }
    } catch (err: any) {
      setError(err.message || "Invalid credentials.");
    } finally {
      setIsLoading(false);
    }
  }

  return (
    <div className="flex h-screen w-full items-center justify-center bg-[#f5f5f5] font-sans">
      <div className="w-full max-w-[380px] mx-4 animate-in fade-in zoom-in-95 duration-300">
        
        <div className="bg-white border border-[#e5e5e5] p-8 rounded-none shadow-[8px_8px_0px_0px_rgba(0,0,0,0.05)]">
          
          {mode === "password" && (
            <div className="animate-in fade-in slide-in-from-left-4 duration-300">
              <div className="text-center mb-8">
                <h1 className="text-xl font-semibold tracking-tight text-[#09090b]">Sign in to Lazuar</h1>
                <p className="text-[13px] text-[#71717a] mt-1.5">Welcome back to your ecosystem.</p>
              </div>

              {error && (
                <div className="mb-6 p-4 bg-rose-50 border border-rose-200">
                  <p className="text-[10px] font-bold tracking-wide uppercase text-rose-600">{error}</p>
                </div>
              )}

              <form onSubmit={handleLoginSubmit} className="space-y-4">
                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Email</label>
                  <input
                    type="email"
                    required
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    className="flex h-11 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]"
                    placeholder="name@example.com"
                  />
                </div>

                <div className="space-y-1.5">
                  <div className="flex items-center justify-between">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Password</label>
                    <button type="button" className="text-[11px] font-medium text-[#09090b] hover:underline">Forgot?</button>
                  </div>
                  <input
                    type="password"
                    required
                    value={password}
                    onChange={(e) => setPassword(e.target.value)}
                    className="flex h-11 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]"
                    placeholder="••••••••"
                  />
                </div>

                <button
                  type="submit"
                  disabled={isLoading}
                  className="w-full h-11 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest rounded-none flex items-center justify-center hover:bg-[#27272a] disabled:opacity-50 transition-colors mt-2"
                >
                  {isLoading ? <Loader2 size={16} className="animate-spin" /> : "Sign In"}
                </button>
              </form>

              <div className="mt-8 text-center">
                <p className="text-[12px] text-[#71717a]">
                  Don't have an account? <span className="text-[#09090b] font-semibold">Contact your administrator</span>
                </p>
              </div>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
