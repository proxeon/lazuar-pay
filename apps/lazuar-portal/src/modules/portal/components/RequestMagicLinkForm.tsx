"use client";

import { useState } from "react";

export function RequestMagicLinkForm({ tenantSlug }: { tenantSlug: string }) {
  const [email, setEmail] = useState("");
  const [submitted, setSubmitted] = useState(false);
  const [pending, setPending] = useState(false);

  async function onSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setPending(true);
    try {
      const base = process.env.NEXT_PUBLIC_API_URL || "http://localhost:8080/api/v1";
      await fetch(`${base}/public/commerce/${encodeURIComponent(tenantSlug)}/portal/magic-link`, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email }),
      });
      setSubmitted(true);
    } finally {
      setPending(false);
    }
  }

  if (submitted) {
    return (
      <p className="text-sm text-muted-foreground max-w-md mt-6">
        If that email has a subscription, we sent a link that expires in 24 hours.
      </p>
    );
  }

  return (
    <form onSubmit={onSubmit} className="mt-6 flex w-full max-w-sm flex-col gap-3">
      <input
        type="email"
        required
        value={email}
        onChange={(event) => setEmail(event.target.value)}
        placeholder="you@example.com"
        autoComplete="email"
        className="h-10 border border-border bg-background px-3 text-sm text-foreground"
      />
      <button
        type="submit"
        disabled={pending}
        className="h-10 bg-foreground text-background text-[11px] font-bold uppercase tracking-widest disabled:opacity-60"
      >
        {pending ? "Sending…" : "Email me a link"}
      </button>
    </form>
  );
}
