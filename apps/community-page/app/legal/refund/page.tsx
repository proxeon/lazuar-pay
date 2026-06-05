import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Refund Policy | Lazuar Education",
  description: "Our satisfaction guarantee and refund process.",
};

export default function RefundPolicyPage() {
  return (
    <article className="prose prose-sm prose-neutral dark:prose-invert max-w-none">
      <h1 className="text-2xl font-semibold tracking-tight mb-2">Refund Policy</h1>
      <p className="text-sm text-muted-foreground mb-8">Last updated: January 2025</p>

      <p>
        At Lazuar Education, we want you to be completely satisfied with your community subscription.
        If our program doesn't meet your expectations, we offer a straightforward refund process.
      </p>

      <h2>1. 7-Day Satisfaction Guarantee</h2>
      <p>
        If you are not satisfied with your subscription for any reason, you may request a
        <strong> full refund within 7 days</strong> of your initial payment. No questions asked.
      </p>
      <ul>
        <li>This guarantee applies to your <strong>first subscription payment only</strong>.</li>
        <li>Renewal payments are not eligible for the 7-day guarantee (you should cancel before renewal instead).</li>
      </ul>

      <h2>2. How to Request a Refund</h2>
      <p>To request a refund, contact us through either channel:</p>
      <ul>
        <li><strong>WhatsApp:</strong> +60 12-345 6789 — simply message "I'd like a refund" with your name and email.</li>
        <li><strong>Email:</strong> support@lazuar.com — include your full name and the email used during checkout.</li>
      </ul>

      <h2>3. After 7 Days</h2>
      <p>
        After the 7-day satisfaction window, refunds are generally not provided. Instead, you may:
      </p>
      <ul>
        <li><strong>Cancel your subscription</strong> to stop future charges. Your access remains active until the end of your current billing period.</li>
        <li><strong>Pause (coming soon)</strong> — temporarily suspend your subscription if you need a break.</li>
      </ul>

      <h2>4. Exceptional Circumstances</h2>
      <p>
        We may provide refunds outside the 7-day window in exceptional circumstances, such as:
      </p>
      <ul>
        <li>Duplicate or accidental charges</li>
        <li>Extended service outage (more than 7 consecutive days of unavailability)</li>
        <li>Billing errors on our end</li>
      </ul>
      <p>Each case is reviewed individually. Contact us to discuss your situation.</p>

      <h2>5. Processing Time</h2>
      <ul>
        <li>Approved refunds are processed within <strong>5–10 business days</strong>.</li>
        <li>Refunds are returned to the original payment method used during checkout.</li>
        <li>Bank processing times may add an additional 3–5 business days depending on your bank.</li>
      </ul>

      <h2>6. Access After Refund</h2>
      <p>
        Once a refund is processed, your access to the private Telegram group and weekly Zoom sessions
        will be revoked immediately. Any downloaded materials remain yours to keep.
      </p>

      <h2>7. Contact</h2>
      <p>For refund requests or questions about this policy:</p>
      <ul>
        <li><strong>Email:</strong> support@lazuar.com</li>
        <li><strong>WhatsApp:</strong> +60 12-345 6789</li>
      </ul>
    </article>
  );
}
