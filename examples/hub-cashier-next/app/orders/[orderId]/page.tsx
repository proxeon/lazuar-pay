import Link from "next/link";
import { notFound } from "next/navigation";
import { getOrder } from "@/lib/orders-store";

export const dynamic = "force-dynamic";

export default async function OrderDetailPage({
  params,
}: {
  params: Promise<{ orderId: string }>;
}) {
  const { orderId } = await params;
  const order = getOrder(orderId);
  if (!order) notFound();

  return (
    <>
      <h1>Order</h1>
      <section className="card">
        <p>
          <code>{order.id}</code>
        </p>
        <p>
          Status:{" "}
          <span className={`status status-${order.status}`}>{order.status}</span>
        </p>
        <p>
          {order.amount} {order.currency}
        </p>
        <p className="muted">{order.description}</p>
        <p className="muted">{order.customerEmail}</p>
        {order.hubCheckoutId ? (
          <p className="muted">
            Hub checkout: <code>{order.hubCheckoutId}</code>
          </p>
        ) : null}
        {order.paidAt ? (
          <p>
            Paid at {order.paidAt}
            {order.lastDeliveryId ? (
              <>
                {" "}
                · delivery <code>{order.lastDeliveryId}</code>
              </>
            ) : null}
          </p>
        ) : (
          <p className="warn">
            Not paid until a signed <code>payment.completed</code> webhook
            arrives. success_url alone never unlocks.
          </p>
        )}
        {order.status === "paid" ? (
          <p>
            Unlocked via signed Hub webhook (not browser redirect).
          </p>
        ) : null}
      </section>

      <p style={{ marginTop: "1.25rem" }}>
        <Link href="/orders">← Orders</Link>
        {" · "}
        <Link href="/pay">Pay</Link>
        {" · "}
        <Link href="/">Home</Link>
      </p>
    </>
  );
}
