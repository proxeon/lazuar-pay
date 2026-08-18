import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Privacy Policy | Lazuar",
  description: "Data processing, collection, and protection policies.",
};

export default function PrivacyPolicyPage() {
  return (
    <article className="prose prose-sm prose-neutral dark:prose-invert max-w-none font-sans">
      <h1 className="text-2xl font-semibold tracking-tight mb-2 text-foreground">Privacy Policy</h1>
      <p className="text-xs font-mono text-muted-foreground mb-8 uppercase tracking-widest">Last updated: August 2026</p>

      <p className="lead text-muted-foreground">
        This Privacy Policy explains how your personal data is collected, processed, and protected when you use the Lazuar platform to interact with Creators.
      </p>

      <h2 className="text-foreground border-b border-border/60 pb-2 mt-8">1. Data Controller vs. Data Processor</h2>
      <p>
        Under the Malaysian Personal Data Protection Act 2010 (PDPA) and the General Data Protection Regulation (GDPR), the Creator you are purchasing from acts as the <strong>Data Controller</strong>. They decide how and why your data is used. 
      </p>
      <p>
        Lazuar acts solely as the <strong>Data Processor</strong>, storing and processing your information strictly on behalf of the Creator to facilitate their business operations.
      </p>

      <h2 className="text-foreground border-b border-border/60 pb-2 mt-8">2. Information Processed</h2>
      <p>To facilitate your purchase and grant you access to receipts and the buyer portal, the Creator uses our platform to collect:</p>
      <ul>
        <li><strong>Identity data:</strong> Full name</li>
        <li><strong>Contact data:</strong> Email address and phone number</li>
        <li><strong>Usage data:</strong> Subscription status and transaction history</li>
      </ul>
      <p>
        <strong>Payment Security:</strong> Payment details (such as full credit card numbers) are transmitted directly to third-party payment gateways (e.g., Stripe, Billplz) and are <strong>never stored on Lazuar's servers</strong>.
      </p>

      <h2 className="text-foreground border-b border-border/60 pb-2 mt-8">3. Sub-Processors</h2>
      <p>To deliver the service on behalf of the Creator, Lazuar utilizes secure third-party sub-processors:</p>
      <ul>
        <li><strong>Resend:</strong> For delivering transactional emails (receipts, magic links).</li>
        <li><strong>Cloudflare:</strong> For secure edge routing and content delivery.</li>
      </ul>

      <h2 className="text-foreground border-b border-border/60 pb-2 mt-8">4. Data Deletion (Right to be Forgotten)</h2>
      <p>
        Because the Creator is the Data Controller, you must contact the Creator directly to request the deletion or anonymization of your personal data from their workspace. 
      </p>
      <p>
        Alternatively, you may contact <strong>privacy@lazuar.com</strong>, and we will formally forward your deletion request to the respective Creator for authorization and execution.
      </p>
      <p>
        Creators can anonymize a buyer from Subscribers → Anonymize.
      </p>
    </article>
  );
}
