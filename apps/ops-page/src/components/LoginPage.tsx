import { useState } from "react";
import { useNavigate, useSearchParams } from "react-router-dom";
import { Loader2 } from "lucide-react";

interface LoginPageProps {
  onLoginSuccess: () => void;
}

export default function LoginPage({ onLoginSuccess }: LoginPageProps) {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const [isLoading, setIsLoading] = useState(false);
  
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");

  const handleLoginSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);

    // Simulated short delay to show active feedback before client-side session approval
    setTimeout(() => {
      onLoginSuccess();
      
      const returnUrl = searchParams.get("returnUrl");
      if (returnUrl) {
        window.location.href = returnUrl;
      } else {
        navigate("/chat");
      }
      setIsLoading(false);
    }, 400);
  };

  return (
    <div className="flex h-screen w-full items-center justify-center bg-[#f5f5f5] font-sans">
      <div className="w-full max-w-[380px] mx-4 animate-in fade-in zoom-in-95 duration-300">
        
        {/* Neobrutalist card styling containing flat hard shadow block */}
        <div className="bg-white border border-[#e5e5e5] p-8 rounded-none shadow-brutal">
          
          <div className="text-center mb-8">
            <h1 className="text-xl font-semibold tracking-tight text-[#09090b]">Sign in to Lazuar</h1>
            <p className="text-[13px] text-[#71717a] mt-1.5">Ecosystem Operations Panel Access</p>
          </div>

          <form onSubmit={handleLoginSubmit} className="space-y-4">
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Email</label>
              <input
                type="email"
                required
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                autoComplete="email"
                className="flex h-11 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm shadow-sm transition-colors focus:outline-none focus:ring-1 focus:ring-[#09090b]"
                placeholder="admin@lazuar.io"
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
                autoComplete="current-password"
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
              Credential issue? <span className="text-[#09090b] font-semibold">Contact site admin</span>
            </p>
          </div>
        </div>
      </div>
    </div>
  );
}
