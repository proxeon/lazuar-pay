"use client";

import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { Suspense, useEffect, useState } from "react";

function CancelInner() {
  const search = useSearchParams();
  const orderId = search.get("order_id") ?? "";
  const [note, setNote] = useState<string | null>(null);

  useEffect(() => {
    if (!orderId) return;
    void (async () => {
      try {
        const res = await fetch(
          `/api/orders/${encodeURIComponent(orderId)}/cancel`,
          { method: "POST" },
        );
        const data = (await res.json()) as {
          order?: { status: string };
          error?: string;
        };
        if (res.status === 409) {
          setNote(
            "Order is already paid via webhook — cancel page does not reverse that.",
          );
          return;
        }
        if (data.order) {
          setNote(
            `Local order marked “${data.order.status}” (not paid). Cancel never unlocks.`,
          );
        }
      } catch {
        setNote("Could not update local order status (demo still not paid).");
      }
    })();
  }, [orderId]);

  return (
    <>
      <h1>Payment cancelled</h1>
      <section className="card">
        <p>
          You left the hosted checkout without completing payment. This page
          does <strong>not</strong> mark the order paid.
        </p>
        {orderId ? (
          <p className="muted">
            Order <code>{orderId}</code>
          </p>
        ) : null}
        {note ? <p className="muted">{note}</p> : null}
        <p>
          <Link href="/pay">Try again</Link>
          {" · "}
          {orderId ? (
            <>
              <Link href={`/orders/${orderId}`}>Order</Link>
              {" · "}
            </>
          ) : null}
          <Link href="/orders">Orders</Link>
          {" · "}
          <Link href="/">Home</Link>
        </p>
      </section>
    </>
  );
}

export default function PayCancelPage() {
  return (
    <Suspense fallback={<p className="muted">Loading…</p>}>
      <CancelInner />
    </Suspense>
  );
}
