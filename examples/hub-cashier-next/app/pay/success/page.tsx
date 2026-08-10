import Link from "next/link";

export default function PaySuccessPage() {
  return (
    <>
      <h1>Payment return</h1>
      <section className="card">
        <p>
          <strong>Do not treat this page as payment confirmation.</strong>
        </p>
        <p className="muted">
          Placeholder <code>success_url</code> target. Later phases will poll
          local order status until a signed Hub webhook marks the order paid.
        </p>
        <p>
          <Link href="/">← Home</Link>
        </p>
      </section>
    </>
  );
}
