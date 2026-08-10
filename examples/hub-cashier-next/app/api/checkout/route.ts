import { NextRequest, NextResponse } from "next/server";
import { getAppBaseUrl, getDefaultCurrency, getHubApiKey } from "@/lib/env";
import {
  createCheckout,
  HubHttpError,
  hubErrorUserMessage,
  mapHubErrorToHttpStatus,
} from "@/lib/hub";
import { createOrder, getOrder, updateOrder } from "@/lib/orders-store";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

type CheckoutBody = {
  order_id?: string;
  amount?: number;
  currency?: string;
  description?: string;
  customer_email?: string;
  email?: string;
};

function isValidEmail(email: string): boolean {
  return email.includes("@") && email.length >= 3 && email.length <= 254;
}

function validateAmount(amount: number, currency: string): string | null {
  if (!Number.isFinite(amount) || amount <= 0) {
    return "amount must be a positive number";
  }
  if (currency === "MYR" && amount < 2) {
    return "MYR amount should be ≥ 2.00 (gateway minimum awareness)";
  }
  return null;
}

/**
 * POST /api/checkout
 * Body: either { order_id } for an existing draft, or { amount, customer_email, … }
 * to create a local order then Hub checkout.
 * Returns { checkout_url, checkout_id, order_id } — never sk_ secrets.
 */
export async function POST(req: NextRequest) {
  if (!getHubApiKey()) {
    return NextResponse.json(
      {
        error: "misconfigured",
        code: "MISCONFIGURED",
        message:
          "LAZUAR_SK_TEST_KEY (or LAZUAR_API_KEY) is not set. Copy .env.example → .env.local.",
      },
      { status: 500 },
    );
  }

  let body: CheckoutBody;
  try {
    body = (await req.json()) as CheckoutBody;
  } catch {
    return NextResponse.json({ error: "invalid_json" }, { status: 400 });
  }

  let order = body.order_id ? getOrder(body.order_id.trim()) : undefined;

  if (!order) {
    const amount = Number(body.amount);
    const currency = (body.currency ?? getDefaultCurrency())
      .trim()
      .toUpperCase();
    const email = (body.customer_email ?? body.email ?? "").trim();
    const description = (body.description ?? "Sample order").trim();

    if (!isValidEmail(email)) {
      return NextResponse.json(
        { error: "invalid_email", message: "customer_email must contain @" },
        { status: 400 },
      );
    }
    if (!/^[A-Z]{3}$/.test(currency)) {
      return NextResponse.json(
        {
          error: "invalid_currency",
          message: "currency must be a 3-letter code (default MYR)",
        },
        { status: 400 },
      );
    }
    const amountErr = validateAmount(amount, currency);
    if (amountErr) {
      return NextResponse.json(
        { error: "invalid_amount", message: amountErr },
        { status: 400 },
      );
    }

    order = createOrder({
      amount,
      currency,
      description: description.slice(0, 200),
      customerEmail: email,
    });
  } else if (order.status === "paid") {
    return NextResponse.json(
      {
        error: "already_paid",
        order_id: order.id,
        checkout_id: order.hubCheckoutId,
      },
      { status: 409 },
    );
  }

  const appBase = getAppBaseUrl();
  const successUrl = `${appBase}/pay/success?order_id=${encodeURIComponent(order.id)}`;
  const cancelUrl = `${appBase}/pay/cancel?order_id=${encodeURIComponent(order.id)}`;

  try {
    const session = await createCheckout({
      amount: order.amount,
      currency: order.currency,
      description: order.description.slice(0, 200),
      customer_email: order.customerEmail,
      success_url: successUrl,
      cancel_url: cancelUrl,
      idempotency_key: `sample-order-${order.id}`,
      metadata: {
        order_id: order.id,
        type: "sample_order",
        source: "hub-cashier-next",
        ...order.metadata,
      },
    });

    updateOrder(order.id, {
      status: "checkout_open",
      hubCheckoutId: session.checkout_id,
      checkoutUrl: session.checkout_url ?? undefined,
    });

    return NextResponse.json({
      order_id: order.id,
      checkout_id: session.checkout_id,
      checkout_url: session.checkout_url,
      status: session.status,
      gateway: session.gateway,
    });
  } catch (e) {
    if (e instanceof HubHttpError) {
      return NextResponse.json(
        {
          error: "hub_error",
          code: e.code,
          message: hubErrorUserMessage(e),
          detail: e.detail,
        },
        { status: mapHubErrorToHttpStatus(e) },
      );
    }
    const message = e instanceof Error ? e.message : "checkout_failed";
    return NextResponse.json(
      {
        error: "hub_error",
        code: "NETWORK",
        message: "Network error calling Hub. Is Hub running on :8080?",
        detail: message,
      },
      { status: 502 },
    );
  }
}
