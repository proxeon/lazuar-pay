import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Privacy Policy | Lazuar Education",
  description: "How we collect, use, and protect your personal data.",
};

export default function PrivacyPolicyPage() {
  return (
    <article className="prose prose-sm prose-neutral dark:prose-invert max-w-none">
      <h1 className="text-2xl font-semibold tracking-tight mb-2">Privacy Policy</h1>
      <p className="text-sm text-muted-foreground mb-8">Last updated: January 2025</p>

      <p>
        Lazuar Education ("we", "our", "us") is committed to protecting your personal data in accordance
        with the Personal Data Protection Act 2010 (PDPA) of Malaysia. This Privacy Policy explains how
        we collect, use, store, and protect your information when you use our community subscription services.
      </p>

      <h2>1. Information We Collect</h2>
      <p>When you subscribe to our community programs, we collect:</p>
      <ul>
        <li><strong>Identity data:</strong> Full name</li>
        <li><strong>Contact data:</strong> Email address, WhatsApp phone number</li>
        <li><strong>Payment data:</strong> Transaction references and payment status (we do not store credit card numbers — these are handled securely by our payment processors)</li>
        <li><strong>Usage data:</strong> Subscription status, attendance records</li>
      </ul>

      <h2>2. How We Use Your Information</h2>
      <p>We use your personal data to:</p>
      <ul>
        <li>Provide access to your subscribed community programs</li>
        <li>Send weekly class links and session reminders via WhatsApp and email</li>
        <li>Process subscription payments and send receipts</li>
        <li>Send service-related notifications (schedule changes, cancellations)</li>
        <li>Improve our programs based on aggregated, anonymised usage data</li>
      </ul>

      <h2>3. Third-Party Processors</h2>
      <p>We share your data with the following third-party service providers, solely for the purpose of delivering our service:</p>
      <ul>
        <li><strong>Billplz / Stripe:</strong> Payment processing</li>
        <li><strong>WhatsApp (Meta):</strong> Session reminders and class links</li>
        <li><strong>Telegram:</strong> Private community group access</li>
        <li><strong>Zoom:</strong> Live class delivery</li>
        <li><strong>Resend:</strong> Transactional email delivery</li>
      </ul>
      <p>Each processor has their own privacy policy governing your data on their platforms.</p>

      <h2>4. Data Retention</h2>
      <p>
        We retain your personal data for as long as your subscription is active, plus 90 days after
        cancellation for accounting purposes. After this period, your data is permanently deleted from
        our systems. Payment transaction records are retained for 7 years as required by Malaysian tax law.
      </p>

      <h2>5. Your Rights Under PDPA</h2>
      <p>You have the right to:</p>
      <ul>
        <li>Access your personal data held by us</li>
        <li>Correct inaccurate personal data</li>
        <li>Withdraw consent for marketing communications</li>
        <li>Request deletion of your personal data</li>
      </ul>

      <h2>6. Data Security</h2>
      <p>
        We implement industry-standard security measures including encryption in transit (TLS),
        encrypted storage for sensitive credentials, and access controls limiting who can view your data.
      </p>

      <h2>7. Marketing Communications</h2>
      <p>
        We may send you promotional offers related to our educational programs. You can opt out at any time
        by replying "STOP" to any WhatsApp message, clicking the unsubscribe link in emails, or contacting us directly.
      </p>

      <h2>8. Contact Us</h2>
      <p>
        For any questions about this Privacy Policy, data access requests, or deletion requests, please contact:
      </p>
      <ul>
        <li><strong>Email:</strong> support@lazuar.com</li>
        <li><strong>WhatsApp:</strong> +60 12-345 6789</li>
      </ul>
    </article>
  );
}
