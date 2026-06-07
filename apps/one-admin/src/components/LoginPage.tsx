import { useState } from "react";
import { client } from "../lib/api-client";

interface LoginPageProps {
  onLogin: (user: { email: string; name: string; role: string; is_system_admin: boolean }) => void;
}

export default function LoginPage({ onLogin }: LoginPageProps) {
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [isLoading, setIsLoading] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError("");
    setIsLoading(true);

    try {
      const { data, error: apiError } = await client.POST("/one/auth/login", {
        body: { email, password }
      });

      if (apiError) throw new Error(apiError.detail || "Invalid credentials.");

      if (data && data.user) {
        if (!data.user.is_system_admin) {
           throw new Error("Access denied. System Administrator privileges required.");
        }

        // Process Return URL for SSO redirection (e.g. back to community-admin)
        const searchParams = new URLSearchParams(window.location.search);
        const returnUrl = searchParams.get("returnUrl");
        
        if (returnUrl) {
          window.location.href = returnUrl;
          return; // Stop execution so the component doesn't unmount while redirecting
        }

        onLogin({
          email: data.user.email,
          name: data.user.name,
          role: data.user.role,
          is_system_admin: data.user.is_system_admin
        });
      }
    } catch (err: any) {
      setError(err.message || "Invalid credentials.");
    } finally {
      setIsLoading(false);
    }
  }

  return (
    <div className="flex h-screen w-full items-center justify-center bg-[#f5f5f5] font-sans">
      <div className="w-full max-w-[380px] mx-4 bg-white border border-[#e5e5e5] rounded-none p-8 shadow-[8px_8px_0px_0px_rgba(0,0,0,0.05)]">
        <div className="text-center mb-8">
          <h1 className="text-xl font-bold tracking-tight text-[#09090b] uppercase">Lazuar One</h1>
          <p className="text-[11px] font-bold uppercase tracking-[0.2em] text-[#71717a] mt-2">God-Mode Access</p>
        </div>

        {error && (
          <div className="mb-6 p-4 bg-rose-50 border border-rose-200">
            <p className="text-[10px] font-bold tracking-wide uppercase text-rose-600">{error}</p>
          </div>
        )}

        <form onSubmit={handleSubmit} className="space-y-5">
          <div className="space-y-2">
            <label className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">Email</label>
            <input type="email" required value={email} onChange={e => setEmail(e.target.value)} className="flex h-11 w-full rounded-none border border-[#e5e5e5] bg-white px-3 text-sm focus:outline-none focus:ring-1 focus:ring-[#09090b]" placeholder="admin@lazuars.io" />
          </div>
          <div className="space-y-2">
            <label className="text-[11px] font-bold uppercase tracking-widest text-[#09090b]">Password</label>
            <input type="password" required value={password} onChange={e => setPassword(e.target.value)} className="flex h-11 w-full rounded-none border border-[#e5e5e5] bg-white px-3 text-sm focus:outline-none focus:ring-1 focus:ring-[#09090b]" placeholder="••••••••" />
          </div>
          <div className="pt-2">
            <button type="submit" disabled={isLoading} className="w-full h-12 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest rounded-none hover:bg-[#27272a] disabled:opacity-50 transition-colors">
              {isLoading ? "Authenticating..." : "Authorize"}
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
