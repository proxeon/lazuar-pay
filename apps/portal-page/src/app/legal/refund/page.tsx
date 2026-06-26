import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Refund Policy | Lazuar",
  description: "Platform refund policies and merchant of record information.",
};

export default function RefundPolicyPage() {
  return (
    <article className="prose prose-sm prose-neutral dark:prose-invert max-w-none font-sans">
      <h1 className="text-2xl font-semibold tracking-tight mb-2 text-foreground">Refund Policy</h1>
      <p className="text-xs font-mono text-muted-foreground mb-8 uppercase tracking-widest">Last updated: June 2026</p>

      <p className="lead text-muted-foreground">
        This policy outlines the refund procedures for purchases made through the Lazuar portal. Please read carefully to understand who processes your transaction and how to request a refund.
      </p>

      <h2 className="text-foreground border-b border-border/60 pb-2 mt-8">1. Merchant of Record</h2>
      <p>
        Lazuar operates exclusively as a software platform provider. When you make a purchase on this portal, you are entering into a direct commercial agreement with the <strong>Creator (Merchant)</strong>. Lazuar does not process, hold, or manage the funds for this transaction.
      </p>

      <h2 className="text-foreground border-b border-border/60 pb-2 mt-8">2. Refund Requests</h2>
      <p>
        Because Lazuar does not hold your funds, <strong>Lazuar cannot issue refunds</strong>. All refund requests, payment disputes, or subscription cancellation inquiries must be directed to the Creator from whom you purchased.
      </p>
      <p>
        You can find the Creator's contact information in your purchase confirmation email or by replying directly to the welcome communications you received upon subscribing.
      </p>

      <h2 className="text-foreground border-b border-border/60 pb-2 mt-8">3. Default Policy</h2>
      <p>
        Unless the Creator has explicitly stated their own refund guarantee on their sales page or marketing materials, you should assume that <strong>all sales are final</strong>.
      </p>
      
      <h2 className="text-foreground border-b border-border/60 pb-2 mt-8">4. Managing Your Subscription</h2>
      <p>
        While we cannot issue refunds for past charges, you maintain full control over future charges. You can cancel your recurring subscription at any time by logging into your secure Buyer Dashboard via the magic link provided in your email. Canceling your subscription will immediately stop all future automated charges.
      </p>
    </article>
  );
}
