import { useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { Loader2 } from "lucide-react";
import { client } from "../lib/api-client";
import { cn } from "../lib/utils";

type AuthMode = "signin" | "signup" | "app_selection";

const AVAILABLE_APPS = [
  { id: "COMMUNITY", name: "Community" },
  { id: "OPS", name: "Operations AI" },
  { id: "VAULT", name: "Vault" },
  { id: "FUNNEL", name: "Funnel Builder" }
];

export default function LoginPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const [mode, setMode] = useState<AuthMode>("signin");
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState("");
  
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [requestedApps, setRequestedApps] = useState<string[]>([]);

  const handleLoginSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    setError("");

    try {
      const { data, error: apiError } = await client.POST("/one/auth/login", {
        body: { email, password }
      });

      if (apiError) throw new Error(apiError.detail || "Invalid credentials.");

      if (data?.user?.role === "SUPER_ADMIN") {
        window.location.href = "http://localhost:3000/dashboard";
        return;
      }

      const returnUrl = searchParams.get("returnUrl");
      if (returnUrl) {
        window.location.href = returnUrl;
      } else {
        navigate("/launchpad");
      }
    } catch (err: any) {
      setError(err.message || "Invalid credentials.");
    } finally {
      setIsLoading(false);
    }
  };

  const handleSignUpSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    setError("");

    if (password !== confirmPassword) {
      setError("Passwords do not match.");
      setIsLoading(false);
      return;
    }

    try {
      const { error: apiError } = await client.POST("/one/public/register", {
        body: { email, password, name: email.split("@")[0] }
      });

      if (apiError) throw new Error(apiError.detail || "Registration failed.");

      setMode("app_selection");
    } catch (err: any) {
      setError(err.message || "Registration failed.");
    } finally {
      setIsLoading(false);
    }
  };

  const handleAppSelectionSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (requestedApps.length === 0) {
      setError("Please select at least one application.");
      return;
    }

    setIsLoading(true);
    setError("");

    try {
      const { error: apiError } = await client.POST("/one/me/access-requests", {
        body: { requested_apps: requestedApps }
      });

      if (apiError) throw new Error(apiError.detail || "Failed to submit request.");

      const returnUrl = searchParams.get("returnUrl");
      if (returnUrl) {
        window.location.href = returnUrl;
      } else {
        navigate("/launchpad");
      }
    } catch (err: any) {
      setError(err.message || "Failed to request access.");
    } finally {
      setIsLoading(false);
    }
  };

  const toggleAppSelection = (appId: string) => {
    setRequestedApps((prev) => 
      prev.includes(appId) ? prev.filter((id) => id !== appId) : [...prev, appId]
    );
  };

  return (
    <div className="flex h-screen w-full items-center justify-center bg-[#f5f5f5] font-sans">
      <div className="w-full max-w-[380px] mx-4 animate-in fade-in zoom-in-95 duration-300">
        
        <div className="bg-white border border-[#e5e5e5] p-8 rounded-none shadow-[8px_8px_0px_0px_rgba(0,0,0,0.05)]">
          
          {error && (
            <div className="mb-6 p-4 bg-rose-50 border border-rose-200">
              <p className="text-[10px] font-bold tracking-wide uppercase text-rose-600">{error}</p>
            </div>
          )}

          {mode === "signin" && (
            <div className="animate-in fade-in slide-in-from-left-4 duration-300">
              <div className="text-center mb-8">
                <h1 className="text-xl font-semibold tracking-tight text-[#09090b]">Sign in to Lazuar</h1>
                <p className="text-[13px] text-[#71717a] mt-1.5">Welcome back to your ecosystem.</p>
              </div>

              <form onSubmit={handleLoginSubmit} className="space-y-4">
                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Email</label>
                  <input type="email" required value={email} onChange={(e) => setEmail(e.target.value)} className="flex h-11 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]" placeholder="name@example.com" />
                </div>

                <div className="space-y-1.5">
                  <div className="flex items-center justify-between">
                    <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Password</label>
                  </div>
                  <input type="password" required value={password} onChange={(e) => setPassword(e.target.value)} className="flex h-11 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]" placeholder="••••••••" />
                </div>

                <button type="submit" disabled={isLoading} className="w-full h-11 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest rounded-none flex items-center justify-center hover:bg-[#27272a] disabled:opacity-50 transition-colors mt-2">
                  {isLoading ? <Loader2 size={16} className="animate-spin" /> : "Sign In"}
                </button>
              </form>

              <div className="mt-8 text-center">
                <p className="text-[12px] text-[#71717a]">
                  Don't have an account? <button onClick={() => setMode("signup")} className="text-[#09090b] font-semibold hover:underline">Sign up</button>
                </p>
              </div>
            </div>
          )}

          {mode === "signup" && (
            <div className="animate-in fade-in slide-in-from-right-4 duration-300">
              <div className="text-center mb-8">
                <h1 className="text-xl font-semibold tracking-tight text-[#09090b]">Create Account</h1>
                <p className="text-[13px] text-[#71717a] mt-1.5">Register a global identity.</p>
              </div>

              <form onSubmit={handleSignUpSubmit} className="space-y-4">
                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Email Address</label>
                  <input type="email" required value={email} onChange={(e) => setEmail(e.target.value)} className="flex h-11 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]" placeholder="name@example.com" />
                </div>

                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Password</label>
                  <input type="password" required value={password} onChange={(e) => setPassword(e.target.value)} className="flex h-11 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]" placeholder="••••••••" />
                </div>

                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Confirm Password</label>
                  <input type="password" required value={confirmPassword} onChange={(e) => setConfirmPassword(e.target.value)} className="flex h-11 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]" placeholder="••••••••" />
                </div>

                <button type="submit" disabled={isLoading} className="w-full h-11 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest rounded-none flex items-center justify-center hover:bg-[#27272a] disabled:opacity-50 transition-colors mt-2">
                  {isLoading ? <Loader2 size={16} className="animate-spin" /> : "Continue"}
                </button>
              </form>

              <div className="mt-8 text-center">
                <p className="text-[12px] text-[#71717a]">
                  Already have an account? <button onClick={() => setMode("signin")} className="text-[#09090b] font-semibold hover:underline">Sign in</button>
                </p>
              </div>
            </div>
          )}

          {mode === "app_selection" && (
            <div className="animate-in fade-in slide-in-from-bottom-4 duration-300">
              <div className="text-center mb-8">
                <h1 className="text-xl font-semibold tracking-tight text-[#09090b]">Request Access</h1>
                <p className="text-[13px] text-[#71717a] mt-1.5">Select the ecosystem applications you need access to.</p>
              </div>

              <form onSubmit={handleAppSelectionSubmit} className="space-y-6">
                <div className="grid grid-cols-2 gap-3">
                  {AVAILABLE_APPS.map(app => (
                    <label 
                      key={app.id}
                      onClick={() => toggleAppSelection(app.id)}
                      className={cn(
                        "flex flex-col items-center justify-center p-3 border cursor-pointer select-none transition-colors rounded-none text-center",
                        requestedApps.includes(app.id) 
                          ? "bg-emerald-50/60 border-emerald-300 text-emerald-950" 
                          : "border-[#e5e5e5] bg-white text-[#71717a] hover:bg-[#fafafa]"
                      )}
                    >
                      <span className="text-[11px] font-bold tracking-widest uppercase mt-1">{app.name}</span>
                    </label>
                  ))}
                </div>

                <button type="submit" disabled={isLoading} className="w-full h-11 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest rounded-none flex items-center justify-center hover:bg-[#27272a] disabled:opacity-50 transition-colors">
                  {isLoading ? <Loader2 size={16} className="animate-spin" /> : "Submit Request"}
                </button>
              </form>
            </div>
          )}

        </div>
      </div>
    </div>
  );
}
