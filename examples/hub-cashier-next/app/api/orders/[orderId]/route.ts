import { NextRequest, NextResponse } from "next/server";
import { getOrder } from "@/lib/orders-store";

export const runtime = "nodejs";
export const dynamic = "force-dynamic";

/** GET /api/orders/:orderId — JSON for success-page polling. */
export async function GET(
  _req: NextRequest,
  ctx: { params: Promise<{ orderId: string }> },
) {
  const { orderId } = await ctx.params;
  const order = getOrder(orderId);
  if (!order) {
    return NextResponse.json({ error: "order_not_found" }, { status: 404 });
  }
  return NextResponse.json({ order });
}
