import { NextResponse } from "next/server";

/**
 * Hub outbound webhook receiver.
 *
 * URL: POST /webhooks/hub/payments
 * (App Router path: app/webhooks/hub/payments/route.ts — not under /api)
 *
 * Runtime: nodejs (required for crypto HMAC + raw body; do not use edge).
 * S44–S45: verify X-Lazuar-Signature over raw body, then fulfill order.
 */
export const runtime = "nodejs";

export async function POST() {
  return NextResponse.json(
    {
      error: "not_implemented",
      message:
        "Webhook stub (S31). Implement HMAC verify + fulfill in S44–S45. Use request.text() for raw body.",
    },
    { status: 501 },
  );
}
