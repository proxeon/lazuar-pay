import { useState } from "react";
import { Link } from "react-router-dom";
import { Loader2 } from "lucide-react";
import { client } from "../lib/api-client";

export default function ForgotPasswordPage() {
  const [email, setEmail] = useState("");
  const [isLoading, setIsLoading] = useState(false);
  const [done, setDone] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);
    try {
      await client.POST("/one/auth/forgot-password", { body: { email } });
      setDone(true);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="flex h-screen w-full items-center justify-center bg-[#f5f5f5] font-sans">
      <div className="w-full max-w-[380px] mx-4 bg-white border border-[#e5e5e5] p-8">
        <h1 className="text-xl font-semibold tracking-tight text-[#09090b] mb-4">Forgot password</h1>
        {done ? (
          <p className="text-[13px] text-[#52525b]">
            If that email exists, we sent a reset link. <Link to="/login" className="font-semibold underline">Sign in</Link>
          </p>
        ) : (
          <form onSubmit={handleSubmit} className="space-y-4">
            <input type="email" required value={email} onChange={(e) => setEmail(e.target.value)} placeholder="name@example.com" className="flex h-11 w-full border border-[#e5e5e5] px-3 text-sm" />
            <button type="submit" disabled={isLoading} className="w-full h-11 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest">
              {isLoading ? <Loader2 size={16} className="animate-spin mx-auto" /> : "Send reset link"}
            </button>
          </form>
        )}
      </div>
    </div>
  );
}
