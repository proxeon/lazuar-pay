import { useState } from "react";
import { Link } from "react-router-dom";
import { useMutation } from "@tanstack/react-query";
import { Loader2 } from "lucide-react";
import { toast } from "sonner";
import { client } from "../lib/api-client";
import { useAuth } from "../hooks/useAuth";

export default function Register() {
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const { handleSmartRouting } = useAuth();

  const registerMutation = useMutation({
    mutationFn: async () => {
      const { error } = await client.POST("/one/public/register", {
        body: { name: name.trim(), email: email.trim(), password }
      });
      if (error) throw new Error(error.detail);
    },
    onSuccess: async () => {
      await handleSmartRouting();
    },
    onError: (err: any) => {
      toast.error("Registration Failed", { description: err.message });
    }
  });

  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    if (password.length < 8) {
      toast.error("Password must be at least 8 characters long.");
      return;
    }
    registerMutation.mutate();
  };

  return (
    <div className="min-h-screen flex items-center justify-center bg-[#f5f5f5] p-4 font-sans text-[#1a1a1a]">
      <div className="w-full max-w-sm bg-white border border-[#e5e5e5] p-8">
        <div className="mb-8 text-center">
          <h1 className="text-xl font-bold tracking-tight text-[#09090b]">Create Account</h1>
          <p className="text-[13px] text-[#71717a] mt-1">Join the Lazuar ecosystem</p>
        </div>

        <form onSubmit={handleSubmit} className="space-y-4">
          <div className="space-y-1.5">
            <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Full Name</label>
            <input 
              type="text" 
              required
              value={name}
              onChange={e => setName(e.target.value)}
              disabled={registerMutation.isPending}
              className="w-full h-10 px-3 border border-[#e5e5e5] text-[13px] focus:outline-none focus:border-[#09090b]"
            />
          </div>

          <div className="space-y-1.5">
            <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Email</label>
            <input 
              type="email" 
              required
              value={email}
              onChange={e => setEmail(e.target.value)}
              disabled={registerMutation.isPending}
              className="w-full h-10 px-3 border border-[#e5e5e5] text-[13px] focus:outline-none focus:border-[#09090b]"
            />
          </div>

          <div className="space-y-1.5">
            <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Password</label>
            <input 
              type="password" 
              required
              value={password}
              onChange={e => setPassword(e.target.value)}
              disabled={registerMutation.isPending}
              className="w-full h-10 px-3 border border-[#e5e5e5] text-[13px] focus:outline-none focus:border-[#09090b]"
            />
          </div>

          <button 
            type="submit" 
            disabled={registerMutation.isPending}
            className="w-full h-10 mt-2 bg-[#09090b] text-white text-[12px] font-bold uppercase tracking-widest hover:bg-[#27272a] transition-colors disabled:opacity-50 flex items-center justify-center gap-2"
          >
            {registerMutation.isPending ? <Loader2 size={16} className="animate-spin" /> : "Register"}
          </button>
        </form>

        <p className="text-center text-[12px] text-[#71717a] mt-6">
          Already have an account? <Link to="/login" className="text-[#09090b] font-medium hover:underline">Log in</Link>
        </p>
      </div>
    </div>
  );
}
