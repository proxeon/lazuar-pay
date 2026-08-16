"use client";

import { useEffect, useState } from "react";

type Plan = { id: string; name: string; interval: string; amount: number; currency: string };

const API = process.env.NEXT_PUBLIC_API_URL || "http://localhost:8080/api/v1";

export function PortalPlanChange({
  tenantSlug,
  token,
  subscriptionId,
  paidThrough,
  pendingProductName,
}: {
  tenantSlug: string;
  token: string;
  subscriptionId: string;
  paidThrough?: string | null;
  pendingProductName?: string | null;
}) {
  const [plans, setPlans] = useState<Plan[]>([]);
  const [selected, setSelected] = useState("");
  const [message, setMessage] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    let cancelled = false;
    fetch(`${API}/public/commerce/${encodeURIComponent(tenantSlug)}/portal/plans?token=${encodeURIComponent(token)}`, {
      credentials: "include",
    })
      .then(async (res) => (res.ok ? res.json() : []))
      .then((data) => {
        if (!cancelled) setPlans(Array.isArray(data) ? data : []);
      })
      .catch(() => {
        if (!cancelled) setPlans([]);
      });
    return () => {
      cancelled = true;
    };
  }, [tenantSlug, token]);

  if (plans.length === 0 && !pendingProductName) {
    return null;
  }

  const submit = async (productId: string | null) => {
    setBusy(true);
    setMessage(null);
    try {
      const res = await fetch(
        `${API}/public/commerce/${encodeURIComponent(tenantSlug)}/portal/change-plan?token=${encodeURIComponent(token)}`,
        {
          method: "POST",
          credentials: "include",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ subscription_id: subscriptionId, product_id: productId }),
        },
      );
      const body = await res.json().catch(() => null);
      if (!res.ok) {
        throw new Error(body?.status || body?.detail || "Could not change plan.");
      }
      setMessage(
        productId
          ? `No charge today. Starts ${paidThrough ? new Date(paidThrough).toLocaleDateString() : "at next renewal"}.`
          : "Pending plan change cleared.",
      );
      window.location.reload();
    } catch (err: any) {
      setMessage(err.message || "Could not change plan.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="w-full space-y-2 mt-2">
      {pendingProductName && (
        <p className="text-xs text-muted-foreground">
          Scheduled: {pendingProductName}. No charge today. Starts {paidThrough ? new Date(paidThrough).toLocaleDateString() : "at next renewal"}.
        </p>
      )}
      <div className="flex flex-col sm:flex-row gap-2">
        <select
          value={selected}
          onChange={(e) => setSelected(e.target.value)}
          className="h-9 border border-border bg-background px-2 text-xs"
        >
          <option value="">Choose a plan</option>
          {plans.map((p) => (
            <option key={p.id} value={p.id}>
              {p.name} · {p.interval} · {p.currency} {p.amount.toFixed(2)}
            </option>
          ))}
        </select>
        <button
          type="button"
          disabled={busy || !selected}
          onClick={() => submit(selected)}
          className="h-9 px-3 border border-border text-[10px] font-bold uppercase tracking-widest disabled:opacity-50"
        >
          Change plan
        </button>
        {pendingProductName && (
          <button
            type="button"
            disabled={busy}
            onClick={() => submit(null)}
            className="h-9 px-3 border border-border text-[10px] font-bold uppercase tracking-widest"
          >
            Keep current
          </button>
        )}
      </div>
      {message && <p className="text-[11px] text-muted-foreground">{message}</p>}
    </div>
  );
}
