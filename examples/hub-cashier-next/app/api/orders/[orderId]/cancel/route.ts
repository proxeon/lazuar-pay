import { NextRequest, NextResponse } from "next/server";
import { getOrder, updateOrder } from "@/lib/orders-store";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

/** POST /api/orders/:orderId/cancel — mark cancelled if still draft/open (never paid). */
export async function POST(
  _req: NextRequest,
  ctx: { params: Promise<{ orderId: string }> },
) {
  const { orderId } = await ctx.params;
  const order = getOrder(orderId);
  if (!order) {
    return NextResponse.json({ error: "order_not_found" }, { status: 404 });
  }
  if (order.status === "paid") {
    return NextResponse.json(
      { error: "already_paid", order_id: order.id },
      { status: 409 },
    );
  }
  if (order.status === "cancelled") {
    return NextResponse.json({ order });
  }
  const next = updateOrder(order.id, { status: "cancelled" });
  return NextResponse.json({ order: next });
}
