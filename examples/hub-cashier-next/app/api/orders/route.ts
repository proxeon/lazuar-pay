import { NextRequest, NextResponse } from "next/server";
import { getDefaultCurrency } from "@/lib/env";
import { createOrder, listOrders } from "@/lib/orders-store";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

/** GET /api/orders — list local orders (demo). */
export async function GET() {
  const orders = listOrders();
  return NextResponse.json({ orders });
}

/** POST /api/orders — create draft order without Hub. */
export async function POST(req: NextRequest) {
  let body: {
    amount?: number;
    currency?: string;
    description?: string;
    customer_email?: string;
    email?: string;
  };
  try {
    body = await req.json();
  } catch {
    return NextResponse.json({ error: "invalid_json" }, { status: 400 });
  }

  const amount = Number(body.amount);
  const currency = (body.currency ?? getDefaultCurrency()).trim().toUpperCase();
  const email = (body.customer_email ?? body.email ?? "").trim();
  const description = (body.description ?? "Sample order").trim();

  if (!email.includes("@")) {
    return NextResponse.json(
      { error: "invalid_email", message: "customer_email must contain @" },
      { status: 400 },
    );
  }
  if (!Number.isFinite(amount) || amount <= 0) {
    return NextResponse.json(
      { error: "invalid_amount", message: "amount must be positive" },
      { status: 400 },
    );
  }

  const order = createOrder({
    amount,
    currency,
    description: description.slice(0, 200),
    customerEmail: email,
  });

  return NextResponse.json({ order }, { status: 201 });
}
