import Link from "next/link";
import { listOrders } from "@/lib/orders-store";

export const dynamic = "force-dynamic";

export default function OrdersPage() {
  const orders = listOrders();

  return (
    <>
      <h1>Orders</h1>
      <p className="muted">
        Local toy domain only (file store under <code>.data/</code>). No Hub DB.
      </p>

      <section className="card">
        {orders.length === 0 ? (
          <p className="muted">No orders yet. Create one from /pay.</p>
        ) : (
          <ul className="order-list">
            {orders.map((o) => (
              <li key={o.id}>
                <Link href={`/orders/${o.id}`}>
                  <code>{o.id.slice(0, 8)}…</code>
                </Link>{" "}
                <span className={`status status-${o.status}`}>{o.status}</span>{" "}
                <span className="muted">
                  {o.amount} {o.currency} · {o.customerEmail}
                </span>
              </li>
            ))}
          </ul>
        )}
      </section>

      <p style={{ marginTop: "1.25rem" }}>
        <Link href="/pay">Pay</Link>
        {" · "}
        <Link href="/">Home</Link>
      </p>
    </>
  );
}
