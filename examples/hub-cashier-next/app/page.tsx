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
          <Link href="/pay">Start a payment →</Link>
          {" · "}
          <Link href="/orders">Orders</Link>
        </p>
      </section>

      <section className="card">
        <h2>Architecture (text)</h2>
        <pre className="ascii">
{`Browser  →  Sample (:3020)  →  Hub (:8080/api/v1)
   │              │                    │
   │         local order            create checkout
   │         .data/                    │
   │              │                    ▼
   │              │            hosted checkout URL
   │              │◄── redirect ───────┘
   │
   │   success_url  →  /pay/success  (poll local; never unlock)
   │
Hub worker ──POST /webhooks/hub/payments──► verify HMAC → mark paid`}
        </pre>
        <p className="muted">
          Domain stays in the sample. Money rails and gateway secrets stay on
          Hub (BYOK).
        </p>
      </section>

      <p className="muted" style={{ marginTop: "1.5rem" }}>
        Demo-only · port <code>3020</code> · store <code>.data/</code>
      </p>
    </>
  );
}
