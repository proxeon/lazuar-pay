"use client";

import Link from "next/link";
import { useSearchParams } from "next/navigation";
import { Suspense, useCallback, useEffect, useState } from "react";

type OrderView = {
  id: string;
  status: string;
  amount: number;
  currency: string;
  paidAt?: string;
  lastDeliveryId?: string;
};

function SuccessInner() {
  const search = useSearchParams();
  const orderId = search.get("order_id") ?? "";
  const [order, setOrder] = useState<OrderView | null>(null);
  const [error, setError] = useState<string | null>(null);

  const load = useCallback(async () => {
    if (!orderId) return;
    try {
      const res = await fetch(`/api/orders/${encodeURIComponent(orderId)}`, {
        cache: "no-store",
      });
      if (!res.ok) {
        setError(res.status === 404 ? "Order not found" : `HTTP ${res.status}`);
        return;
      }
      const data = (await res.json()) as { order: OrderView };
      setOrder(data.order);
      setError(null);
    } catch (e) {
      setError(e instanceof Error ? e.message : "poll failed");
    }
  }, [orderId]);

  useEffect(() => {
    void load();
    if (!orderId) return;
    const id = setInterval(() => {
      void load();
    }, 2000);
    return () => clearInterval(id);
  }, [load, orderId]);

  const paid = order?.status === "paid";

  return (
    <>
      <h1>{paid ? "Payment received" : "Payment return"}</h1>
      <section className="card">
        <p>
          <strong>
            Do not treat this page as payment confirmation by itself.
          </strong>
        </p>
        <p className="muted">
          <code>success_url</code> only means the shopper returned from the
          hosted page. Fulfillment happens only after a{" "}
          <strong>signed Hub webhook</strong> (
          <code>payment.completed</code>) updates the local order store. This
          page never unlocks on load.
        </p>

        {!orderId ? (
          <p className="error">No <code>order_id</code> in the query string.</p>
        ) : null}

        {error ? <p className="error">{error}</p> : null}

        {order ? (
          <div className="status-block">
            <p>
              Order <code>{order.id}</code>
            </p>
            <p>
              Status:{" "}
              <span className={`status status-${order.status}`}>
                {order.status}
              </span>
            </p>
            <p className="muted">
              {order.amount} {order.currency}
              {order.paidAt ? ` · paid at ${order.paidAt}` : ""}
            </p>
            {paid ? (
              <p>
                Unlocked via signed <code>payment.completed</code>
                {order.lastDeliveryId
                  ? ` (delivery ${order.lastDeliveryId})`
                  : ""}
                .
              </p>
            ) : (
              <p className="warn">
                Waiting for webhook / processing… Polling local order every 2s.
              </p>
            )}
          </div>
        ) : orderId && !error ? (
          <p className="muted">Loading order…</p>
        ) : null}

        <p style={{ marginTop: "1rem" }}>
          {orderId ? (
            <>
              <Link href={`/orders/${orderId}`}>Order detail</Link>
              {" · "}
            </>
          ) : null}
          <Link href="/orders">Orders</Link>
          {" · "}
          <Link href="/pay">Pay again</Link>
          {" · "}
          <Link href="/">Home</Link>
        </p>
      </section>
    </>
  );
}

export default function PaySuccessPage() {
  return (
    <Suspense fallback={<p className="muted">Loading…</p>}>
      <SuccessInner />
    </Suspense>
  );
}
