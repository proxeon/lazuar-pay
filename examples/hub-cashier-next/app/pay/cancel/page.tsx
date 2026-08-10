import Link from "next/link";

export default function PayCancelPage() {
  return (
    <>
      <h1>Payment cancelled</h1>
      <section className="card">
        <p className="muted">
          Placeholder <code>cancel_url</code> target. Shopper left the hosted
          checkout without completing payment.
        </p>
        <p>
          <Link href="/pay">Try again</Link>
          {" · "}
          <Link href="/">Home</Link>
        </p>
      </section>
    </>
  );
}
