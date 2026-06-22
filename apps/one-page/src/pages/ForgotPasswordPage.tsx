import { useState } from "react";
import { Link } from "react-router-dom";
import { Loader2 } from "lucide-react";
import { client } from "../lib/api-client";

export default function ForgotPasswordPage() {
  const [isLoading, setIsLoading] = useState(false);
  const [success, setSuccess] = useState(false);
  const [email, setEmail] = useState("");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    await client.POST("/one/auth/forgot-password", { body: { email } });
    setSuccess(true);
    setIsLoading(false);
  };

  return (
    <div className="flex h-screen w-full items-center justify-center bg-[#f5f5f5] font-sans">
      <div className="w-full max-w-[380px] mx-4 bg-white border border-[#e5e5e5] p-8 rounded-none">
        <div className="text-center mb-8">
          <h1 className="text-xl font-semibold tracking-tight text-[#09090b]">Reset Password</h1>
        </div>

        {success ? (
          <div className="text-center space-y-4">
            <p className="text-[13px] text-[#71717a] leading-relaxed">If an account exists for that email, a password reset link has been sent.</p>
            <Link to="/login" className="inline-block mt-4 text-[11px] font-bold uppercase tracking-widest text-[#09090b] hover:underline">Return to Login</Link>
          </div>
        ) : (
          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="space-y-1.5">
              <label className="text-[11px] font-bold uppercase tracking-widest text-[#71717a]">Email Address</label>
              <input type="email" required value={email} onChange={e => setEmail(e.target.value)} className="flex h-11 w-full rounded-none border border-[#e5e5e5] bg-white px-3 py-1 text-sm focus:outline-none focus:border-[#09090b]" />
            </div>
            <button type="submit" disabled={isLoading} className="w-full h-11 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest rounded-none flex items-center justify-center hover:bg-[#27272a] disabled:opacity-50 transition-colors mt-2">
              {isLoading ? <Loader2 size={16} className="animate-spin" /> : "Send Reset Link"}
            </button>
            <div className="text-center mt-4">
              <Link to="/login" className="text-[12px] text-[#71717a] hover:text-[#09090b]">Cancel</Link>
            </div>
          </form>
        )}
      </div>
    </div>
  );
}
