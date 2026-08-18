import { useEffect, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { API_URL } from "../lib/api-client";

export default function VerifyEmailPage() {
  const [searchParams] = useSearchParams();
  const email = searchParams.get("email") ?? "";
  const token = searchParams.get("token") ?? "";
  const [status, setStatus] = useState<"working" | "ok" | "error">("working");
  const [detail, setDetail] = useState("");

  useEffect(() => {
    if (!email || !token) {
      setStatus("error");
      setDetail("This verification link is missing email or token.");
      return;
    }

    const run = async () => {
      try {
        const res = await fetch(
          `${API_URL}/one/auth/verify-email?email=${encodeURIComponent(email)}`,
          {
            method: "POST",
            credentials: "include",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ token }),
          }
        );
        if (!res.ok) {
          const body = await res.json().catch(() => null);
          throw new Error(body?.detail || "Verification failed.");
        }
        setStatus("ok");
      } catch (err: unknown) {
        setStatus("error");
        setDetail(err instanceof Error ? err.message : "Verification failed.");
      }
    };

    void run();
  }, [email, token]);

  return (
    <div className="flex h-screen w-full items-center justify-center bg-[#f5f5f5] font-sans">
      <div className="w-full max-w-[380px] mx-4 bg-white border border-[#e5e5e5] p-8 text-[13px] text-[#52525b]">
        {status === "working" && <p>Verifying your email…</p>}
        {status === "ok" && (
          <p>
            Email verified. <Link to="/login" className="font-semibold underline">Sign in</Link>
          </p>
        )}
        {status === "error" && <p className="text-rose-600">{detail}</p>}
      </div>
    </div>
  );
}
