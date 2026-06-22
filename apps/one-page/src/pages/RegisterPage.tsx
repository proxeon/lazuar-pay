import { useState } from "react";
import { useSearchParams, useNavigate } from "react-router-dom";
import { Loader2, UserPlus } from "lucide-react";
import { toast } from "sonner";

const API_URL = import.meta.env.VITE_API_URL || "http://localhost:8080/api/v1";

export default function RegisterPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [isSubmitting, setIsSubmitting] = useState(false);

  const handleRegisterSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsSubmitting(true);

    try {
      const response = await fetch(`${API_URL}/one/public/register`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, password, name }),
        credentials: "include"
      });

      const data = await response.json();

      if (!response.ok) {
        throw new Error(data.detail || "Account registration rejected.");
      }

      toast.success("Identity profile successfully provisioned.");

      const returnUrl = searchParams.get("returnUrl");
      if (returnUrl) {
        window.location.href = decodeURIComponent(returnUrl);
      } else {
        navigate("/launchpad");
      }
    } catch (err: any) {
      toast.error("Registration Failed", { description: err.message });
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-zinc-50 font-sans text-zinc-900 antialiased">
      <div className="bg-white border border-zinc-200 p-8 w-full max-w-md shadow-sm">
        <div className="flex flex-col items-center mb-8">
          <div className="p-2.5 bg-zinc-50 border border-zinc-200 text-zinc-800 mb-3">
            <UserPlus size={20} />
          </div>
          <h2 className="text-[14px] font-bold uppercase tracking-widest">Create Your Identity</h2>
          <p className="text-xs text-zinc-500 mt-1">Register a new master profile</p>
        </div>

        <form onSubmit={handleRegisterSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <label className="text-[11px] font-bold uppercase tracking-widest text-zinc-500">Full Name</label>
            <input 
              type="text" 
              value={name} 
              onChange={e => setName(e.target.value)} 
              disabled={isSubmitting}
              placeholder="Ahmad Firdaus"
              className="flex h-10 w-full border border-zinc-200 bg-white px-3 text-[13px] focus:outline-none focus:border-zinc-900 disabled:opacity-50" 
            />
          </div>

          <div className="space-y-1.5">
            <label className="text-[11px] font-bold uppercase tracking-widest text-zinc-500">Email Address *</label>
            <input 
              required 
              type="email" 
              value={email} 
              onChange={e => setEmail(e.target.value)} 
              disabled={isSubmitting}
              className="flex h-10 w-full border border-zinc-200 bg-white px-3 text-[13px] focus:outline-none focus:border-zinc-900 disabled:opacity-50" 
            />
          </div>

          <div className="space-y-1.5">
            <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Password *</label>
            <input 
              required 
              type="password" 
              value={password} 
              onChange={e => setPassword(e.target.value)} 
              disabled={isSubmitting}
              className="flex h-10 w-full border border-zinc-200 bg-white px-3 text-[13px] focus:outline-none focus:border-[#09090b] disabled:opacity-50" 
            />
          </div>

          <div className="pt-2">
            <button type="submit" disabled={isSubmitting || !email.trim() || !password} className="w-full h-10 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest hover:bg-[#27272a] transition-colors disabled:opacity-50 flex items-center justify-center gap-2">
              {isSubmitting && <Loader2 size={13} className="animate-spin" />} Provision Profile
            </button>
          </div>
        </form>
      </div>
    </div>
  );
}
