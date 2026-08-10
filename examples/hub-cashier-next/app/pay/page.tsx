"use client";

import Link from "next/link";
import { FormEvent, useState } from "react";

export default function PayPage() {
  const [amount, setAmount] = useState("25.00");
  const [email, setEmail] = useState("guest@example.com");
  const [description, setDescription] = useState("Demo sample order");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setError(null);
    setLoading(true);
    try {
      const res = await fetch("/api/checkout", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          amount: Number(amount),
          currency: "MYR",
          customer_email: email,
          description,
        }),
      });
      const data = (await res.json()) as {
        checkout_url?: string;
        order_id?: string;
        message?: string;
        detail?: string;
        code?: string;
        error?: string;
      };

      if (!res.ok) {
        setError(
          data.message ||
            data.detail ||
            data.error ||
            `Checkout failed (${res.status})`,
        );
        return;
      }

      if (!data.checkout_url) {
        setError(
          "Hub returned no checkout_url. Check gateway BYOK and Hub logs.",
        );
        return;
      }

      // Redirect browser to Hub hosted checkout
      window.location.href = data.checkout_url;
    } catch (err) {
      setError(err instanceof Error ? err.message : "Network error");
    } finally {
      setLoading(false);
    }
  }

  return (
    <>
      <h1>Pay</h1>
      <p className="muted">
        Creates a <strong>local order</strong>, then a Hub hosted checkout
        server-side. Domain stays here; money rails on Hub.
      </p>

      <section className="card">
        <form onSubmit={onSubmit} className="form">
          <label>
            Amount (MYR)
            <input
              type="number"
              step="0.01"
              min="2"
              required
              value={amount}
              onChange={(e) => setAmount(e.target.value)}
            />
          </label>
          <label>
            Customer email
            <input
              type="email"
              required
              value={email}
              onChange={(e) => setEmail(e.target.value)}
            />
          </label>
          <label>
            Description
            <input
              type="text"
              maxLength={200}
              value={description}
              onChange={(e) => setDescription(e.target.value)}
            />
          </label>

          {error ? (
            <p className="error" role="alert">
              {error}
            </p>
          ) : null}

          <button type="submit" disabled={loading}>
            {loading ? "Starting checkout…" : "Pay with Hub"}
          </button>
        </form>
      </section>

      <p style={{ marginTop: "1.25rem" }}>
        <Link href="/orders">Orders list</Link>
        {" · "}
        <Link href="/">Home</Link>
      </p>
    </>
  );
}
