import { useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { Loader2 } from "lucide-react";
import { client } from "../lib/api-client";

export default function ResetPasswordPage() {
  const [searchParams] = useSearchParams();
  const email = searchParams.get("email") ?? "";
  const token = searchParams.get("token") ?? "";
  const [password, setPassword] = useState("");
  const [confirm, setConfirm] = useState("");
  const [isLoading, setIsLoading] = useState(false);
  const [error, setError] = useState("");
  const [done, setDone] = useState(false);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (password !== confirm) {
      setError("Passwords do not match.");
      return;
    }
    setIsLoading(true);
    setError("");
    try {
      const { error: apiError } = await client.POST("/one/auth/reset-password", {
        body: { email, token, new_password: password },
      });
      if (apiError) throw new Error(apiError.detail || "Reset failed.");
      setDone(true);
    } catch (err: unknown) {
      setError(err instanceof Error ? err.message : "Reset failed.");
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="flex h-screen w-full items-center justify-center bg-[#f5f5f5] font-sans">
      <div className="w-full max-w-[380px] mx-4 bg-white border border-[#e5e5e5] p-8">
        <h1 className="text-xl font-semibold tracking-tight text-[#09090b] mb-4">Reset password</h1>
        {error && <p className="mb-4 text-[12px] text-rose-600">{error}</p>}
        {done ? (
          <p className="text-[13px] text-[#52525b]">
            Password updated. <Link to="/login" className="font-semibold underline">Sign in</Link>
          </p>
        ) : (
          <form onSubmit={handleSubmit} className="space-y-4">
            <input type="password" required value={password} onChange={(e) => setPassword(e.target.value)} placeholder="New password" className="flex h-11 w-full border border-[#e5e5e5] px-3 text-sm" />
            <input type="password" required value={confirm} onChange={(e) => setConfirm(e.target.value)} placeholder="Confirm password" className="flex h-11 w-full border border-[#e5e5e5] px-3 text-sm" />
            <button type="submit" disabled={isLoading || !email || !token} className="w-full h-11 bg-[#09090b] text-white text-[11px] font-bold uppercase tracking-widest disabled:opacity-50">
              {isLoading ? <Loader2 size={16} className="animate-spin mx-auto" /> : "Save password"}
            </button>
          </form>
        )}
      </div>
    </div>
  );
}
