import Link from "next/link";

export default function PayPage() {
  return (
    <>
      <h1>Pay</h1>
      <p className="muted">
        Placeholder for the demo order / checkout UI (S41–S43).
      </p>
      <section className="card">
        <p>
          Later phases add a form that creates a local order and starts a Hub
          checkout.
        </p>
        <p>
          <Link href="/">← Home</Link>
          {" · "}
          <Link href="/pay/success">Success</Link>
          {" · "}
          <Link href="/pay/cancel">Cancel</Link>
        </p>
      </section>
    </>
  );
}
