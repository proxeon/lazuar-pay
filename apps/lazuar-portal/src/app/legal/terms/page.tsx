import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Terms of Service | Lazuar",
  description: "Terms and conditions for using the Lazuar platform.",
};

export default function TermsOfServicePage() {
  return (
    <article className="prose prose-sm prose-neutral dark:prose-invert max-w-none font-sans">
      <h1 className="text-2xl font-semibold tracking-tight mb-2 text-foreground">Terms of Service</h1>
      <p className="text-xs font-mono text-muted-foreground mb-8 uppercase tracking-widest">Last updated: June 2026</p>

      <p className="lead text-muted-foreground">
        By accessing or using the Lazuar platform to purchase subscriptions or digital products, you agree to be bound by these Terms of Service.
      </p>

      <h2 className="text-foreground border-b border-border/60 pb-2 mt-8">1. Role of Lazuar</h2>
      <p>
        Lazuar is a technology platform that enables independent creators, educators, and businesses ("Creators") to sell subscriptions and digital products. Lazuar is not a party to the transaction between you and the Creator. We are not responsible for the quality, accuracy, delivery, or legality of the content provided by the Creator.
      </p>

      <h2 className="text-foreground border-b border-border/60 pb-2 mt-8">2. Transactions and Liability</h2>
      <p>
        Your purchase is a direct transaction between you and the Creator. Lazuar acts only as the software infrastructure facilitating the checkout and access delivery. 
      </p>
      <p>
        Lazuar is not liable for any unfulfilled promises, undelivered goods, offensive content published by a Creator, or changes to the Creator's pricing. Any claims, disputes, or requests for refunds must be brought directly against the Creator.
      </p>

      <h2 className="text-foreground border-b border-border/60 pb-2 mt-8">3. Access and Uptime</h2>
      <p>
        While Lazuar strives to maintain 99.9% platform uptime, we are not liable for temporary service interruptions that may prevent access to a Creator's checkout page, buyer portal, or digital content. 
      </p>

      <h2 className="text-foreground border-b border-border/60 pb-2 mt-8">4. Account Security</h2>
      <p>
        Access to your purchases is managed via passwordless "magic links" sent to your email. You are responsible for maintaining the security of your email account. Lazuar is not liable for unauthorized access to your Buyer Dashboard resulting from a compromised email inbox.
      </p>

      <h2 className="text-foreground border-b border-border/60 pb-2 mt-8">5. Governing Law</h2>
      <p>
        These Terms are governed by the laws of Malaysia. Any disputes arising directly with the Lazuar software platform shall be subject to the exclusive jurisdiction of the courts of Malaysia. Disputes regarding the actual product or subscription purchased must be resolved in accordance with the Creator's local jurisdiction.
      </p>
    </article>
  );
}
