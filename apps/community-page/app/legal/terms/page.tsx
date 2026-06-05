import type { Metadata } from "next";

export const metadata: Metadata = {
  title: "Terms of Service | Lazuar Education",
  description: "Terms and conditions for using our community subscription services.",
};

export default function TermsOfServicePage() {
  return (
    <article className="prose prose-sm prose-neutral dark:prose-invert max-w-none">
      <h1 className="text-2xl font-semibold tracking-tight mb-2">Terms of Service</h1>
      <p className="text-sm text-muted-foreground mb-8">Last updated: January 2025</p>

      <p>
        By subscribing to any community program offered by Lazuar Education ("we", "our", "us"),
        you agree to be bound by these Terms of Service. Please read them carefully before subscribing.
      </p>

      <h2>1. Service Description</h2>
      <p>
        We provide online educational community subscriptions that include live weekly classes
        (via Zoom or equivalent), access to a private Telegram group, recorded session replays,
        and supplementary learning materials. The specific inclusions for each program are described
        on the respective program page.
      </p>

      <h2>2. Subscription & Billing</h2>
      <ul>
        <li>Subscriptions are billed on a monthly or yearly basis as indicated at the time of purchase.</li>
        <li>Payment is collected via our payment processor (Billplz or Stripe) at the time of subscription.</li>
        <li>We operate on a <strong>manual renewal model</strong> — you will receive a WhatsApp reminder
            with a payment link 3 days before your billing period ends.</li>
        <li>If payment is not received within 7 days of the renewal date, your subscription will be
            marked as expired and access will be revoked.</li>
        <li>Prices are in Malaysian Ringgit (MYR) unless otherwise stated.</li>
      </ul>

      <h2>3. Access & Delivery</h2>
      <ul>
        <li>Upon successful payment, you will receive access links (Telegram group invite and Zoom link)
            via WhatsApp and email within minutes.</li>
        <li>Access is personal and non-transferable. Sharing your access links or login credentials
            with others is prohibited.</li>
        <li>We reserve the right to remove members who violate community guidelines from the Telegram group.</li>
      </ul>

      <h2>4. Cancellation</h2>
      <ul>
        <li>You may cancel your subscription at any time by contacting us via WhatsApp or email.</li>
        <li>Upon cancellation, you retain access until the end of your current billing period.</li>
        <li>No partial refunds are given for unused days within a billing period (except within the 7-day satisfaction guarantee).</li>
      </ul>

      <h2>5. Intellectual Property</h2>
      <p>
        All course materials, recordings, slides, and content shared within the community programs
        are the intellectual property of Lazuar Education. You may not reproduce, distribute, or
        commercially exploit any content without prior written permission.
      </p>

      <h2>6. Limitation of Liability</h2>
      <p>
        Our programs are provided on an "as is" basis. While we strive to deliver high-quality
        educational content, we do not guarantee specific learning outcomes. Our total liability
        to you shall not exceed the amount you paid for your current subscription period.
      </p>

      <h2>7. Changes to These Terms</h2>
      <p>
        We may update these Terms of Service from time to time. Changes will be communicated via
        email or WhatsApp at least 14 days before they take effect. Continued use of the service
        after changes take effect constitutes acceptance.
      </p>

      <h2>8. Governing Law</h2>
      <p>
        These Terms are governed by the laws of Malaysia. Any disputes arising from these Terms
        shall be subject to the exclusive jurisdiction of the courts of Malaysia.
      </p>

      <h2>9. Contact</h2>
      <p>For questions about these Terms, please contact:</p>
      <ul>
        <li><strong>Email:</strong> support@lazuar.com</li>
        <li><strong>WhatsApp:</strong> +60 12-345 6789</li>
      </ul>
    </article>
  );
}
