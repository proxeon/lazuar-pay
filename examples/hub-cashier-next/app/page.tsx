import Link from "next/link";

export default function HomePage() {
  return (
    <>
      <h1>Hub Cashier Sample</h1>
      <p className="muted">
        Minimal Next.js App Router app that proves Hub as a multi-app payments
        cashier. Browser never holds Hub secrets; fulfillment is via signed Hub
        webhooks only.
      </p>

      <section className="card">
        <h2>What this proves</h2>
        <ul>
          <li>Create a local order, then a Hub hosted checkout (server-side).</li>
          <li>Redirect the shopper to Hub&apos;s hosted page.</li>
          <li>
            Mark the order paid only after a verified{" "}
            <code>payment.completed</code> webhook.
          </li>
          <li>
            No Billplz/Stripe SDK and no monorepo <code>@repo/*</code> packages.
          </li>
        </ul>
        <p>
          <Link href="/pay">Go to /pay (scaffold)</Link>
        </p>
      </section>

      <p className="muted" style={{ marginTop: "1.5rem" }}>
        Scaffold only (S31). Checkout + webhook logic lands in later sample
        phases. Dev port: <code>3020</code>.
      </p>
    </>
  );
}
