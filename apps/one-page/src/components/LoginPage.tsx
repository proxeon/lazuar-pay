import { useState } from "react";
import { useNavigate } from "react-router-dom";
import { Loader2, CheckCircle2, ArrowLeft, Mail } from "lucide-react";

// Mock SVG for Google
const GoogleIcon = () => (
  <svg viewBox="0 0 24 24" width="16" height="16" xmlns="http://www.w3.org/2000/svg">
    <path d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z" fill="#4285F4"/>
    <path d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z" fill="#34A853"/>
    <path d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.07H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.93l2.85-2.22.81-.62z" fill="#FBBC05"/>
    <path d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.07l3.66 2.84c.87-2.6 3.3-4.53 6.16-4.53z" fill="#EA4335"/>
  </svg>
);

// Mock SVG for Apple
const AppleIcon = () => (
  <svg viewBox="0 0 24 24" width="16" height="16" xmlns="http://www.w3.org/2000/svg" fill="currentColor">
    <path d="M12.152 6.896c-.948 0-2.415-1.078-3.96-1.04-2.04.027-3.91 1.183-4.961 3.014-2.117 3.675-.546 9.103 1.519 12.09 1.013 1.454 2.208 3.126 3.802 3.08 1.498-.046 2.096-.948 3.926-.948 1.815 0 2.37.948 3.926.91 1.637-.038 2.65-1.516 3.633-3.004 1.144-1.688 1.615-3.327 1.637-3.418-.035-.015-3.197-1.222-3.228-4.85-.027-3.04 2.484-4.513 2.597-4.582-1.428-2.09-3.623-2.37-4.408-2.417-1.921-.194-3.818 1.166-4.502 1.166zm1.513-4.71c.84-1.016 1.403-2.428 1.25-3.836-1.185.048-2.65.787-3.513 1.802-.77.876-1.428 2.325-1.25 3.705 1.328.102 2.673-.65 3.513-1.67z" />
  </svg>
);

type AuthMode = "password" | "magic_link" | "magic_link_sent";

export default function LoginPage() {
  const navigate = useNavigate();
  const [mode, setMode] = useState<AuthMode>("password");
  const [isLoading, setIsLoading] = useState(false);
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");

  // Simulates a standard password or OAuth login
  const handleLogin = (e?: React.FormEvent) => {
    if (e) e.preventDefault();
    setIsLoading(true);
    
    // Mock 1-second network latency, then redirect
    setTimeout(() => {
      setIsLoading(false);
      navigate("/launchpad");
    }, 1000);
  };

  // Simulates sending a magic link
  const handleMagicLinkSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);

    // Mock 1-second latency, then show success state
    setTimeout(() => {
      setIsLoading(false);
      setMode("magic_link_sent");
    }, 1000);
  };

  return (
    <div className="flex h-screen w-full items-center justify-center bg-[#f5f5f5] font-sans">
      <div className="w-full max-w-[380px] mx-4 animate-in fade-in zoom-in-95 duration-300">
        
        {/* The Auth Card */}
        <div className="bg-white border border-[#e5e5e5] p-8 shadow-sm">
          
          {/* STATE 1: Standard Password Login */}
          {mode === "password" && (
            <>
              <div className="text-center mb-8">
                <h1 className="text-xl font-semibold tracking-tight text-[#09090b]">Sign in to Lazuar</h1>
                <p className="text-[13px] text-[#71717a] mt-1.5">Welcome back to your ecosystem.</p>
              </div>

              <form onSubmit={handleLogin} className="space-y-4">
                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Email</label>
                  <input
                    type="email"
                    required
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    className="flex h-11 w-full border border-[#e5e5e5] bg-white px-3 py-1 text-sm shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]"
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
                    className="flex h-11 w-full border border-[#e5e5e5] bg-white px-3 py-1 text-sm shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]"
                    placeholder="••••••••"
                  />
                </div>

                <button
                  type="submit"
                  disabled={isLoading}
                  className="w-full h-11 bg-[#09090b] text-white text-sm font-semibold tracking-wide flex items-center justify-center hover:bg-[#27272a] disabled:opacity-50 transition-colors mt-2"
                >
                  {isLoading ? <Loader2 size={16} className="animate-spin" /> : "Sign In"}
                </button>
              </form>

              <div className="relative my-6">
                <div className="absolute inset-0 flex items-center"><span className="w-full border-t border-[#e5e5e5]" /></div>
                <div className="relative flex justify-center text-xs uppercase"><span className="bg-white px-2 text-[#a1a1aa] font-medium tracking-widest text-[10px]">Or continue with</span></div>
              </div>

              <div className="space-y-2.5">
                <button type="button" onClick={() => handleLogin()} disabled={isLoading} className="w-full h-11 border border-[#e5e5e5] bg-white text-[#09090b] text-[13px] font-medium flex items-center justify-center gap-2 hover:bg-[#fafafa] disabled:opacity-50 transition-colors shadow-sm">
                  <GoogleIcon /> Google
                </button>
                <button type="button" onClick={() => handleLogin()} disabled={isLoading} className="w-full h-11 border border-[#e5e5e5] bg-white text-[#09090b] text-[13px] font-medium flex items-center justify-center gap-2 hover:bg-[#fafafa] disabled:opacity-50 transition-colors shadow-sm">
                  <AppleIcon /> Apple
                </button>
                <button type="button" onClick={() => setMode("magic_link")} disabled={isLoading} className="w-full h-11 border border-[#e5e5e5] bg-white text-[#09090b] text-[13px] font-medium flex items-center justify-center gap-2 hover:bg-[#fafafa] disabled:opacity-50 transition-colors shadow-sm">
                  <Mail size={16} /> Passwordless / Magic Link
                </button>
              </div>
            </>
          )}

          {/* STATE 2: Magic Link Form */}
          {mode === "magic_link" && (
            <div className="animate-in fade-in slide-in-from-right-4 duration-300">
              <div className="text-center mb-8">
                <h1 className="text-xl font-semibold tracking-tight text-[#09090b]">Passwordless Sign In</h1>
                <p className="text-[13px] text-[#71717a] mt-1.5 leading-relaxed">Enter your email and we'll send a secure login link directly to your inbox.</p>
              </div>

              <form onSubmit={handleMagicLinkSubmit} className="space-y-5">
                <div className="space-y-1.5">
                  <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Email Address</label>
                  <input
                    type="email"
                    required
                    value={email}
                    onChange={(e) => setEmail(e.target.value)}
                    className="flex h-11 w-full border border-[#e5e5e5] bg-white px-3 py-1 text-sm shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]"
                    placeholder="name@example.com"
                  />
                </div>

                <button
                  type="submit"
                  disabled={isLoading || !email}
                  className="w-full h-11 bg-[#09090b] text-white text-sm font-semibold tracking-wide flex items-center justify-center hover:bg-[#27272a] disabled:opacity-50 transition-colors"
                >
                  {isLoading ? <Loader2 size={16} className="animate-spin" /> : "Send Magic Link"}
                </button>
              </form>

              <div className="mt-6 text-center">
                <button type="button" onClick={() => setMode("password")} className="text-[12px] font-medium text-[#71717a] hover:text-[#09090b] flex items-center justify-center gap-1.5 mx-auto transition-colors">
                  <ArrowLeft size={14} /> Back to password login
                </button>
              </div>
            </div>
          )}

          {/* STATE 3: Magic Link Success */}
          {mode === "magic_link_sent" && (
            <div className="animate-in fade-in zoom-in-95 duration-300 text-center py-4">
              <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-full bg-emerald-50 mb-6">
                <CheckCircle2 className="h-7 w-7 text-emerald-600" />
              </div>
              <h1 className="text-xl font-semibold tracking-tight text-[#09090b] mb-2">Check your email</h1>
              <p className="text-[13px] text-[#71717a] leading-relaxed mb-8">
                We sent a secure link to <strong className="font-semibold text-[#09090b]">{email || "your inbox"}</strong>. Click it to access your account.
              </p>
              <button type="button" onClick={() => setMode("password")} className="text-[12px] font-medium text-[#71717a] hover:text-[#09090b] flex items-center justify-center gap-1.5 mx-auto transition-colors">
                <ArrowLeft size={14} /> Back to login
              </button>
            </div>
          )}

        </div>
      </div>
    </div>
  );
}
