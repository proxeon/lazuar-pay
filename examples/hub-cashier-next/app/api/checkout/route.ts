import { NextResponse } from "next/server";

/**
 * Stub — S42 implements Hub checkout create.
 * POST { order_id } → Hub integrations/payments/checkouts → { checkout_url }
 */
export async function POST() {
  return NextResponse.json(
    {
      error: "not_implemented",
      message: "Checkout route stub (S31). Implement Hub create checkout in S42.",
    },
    { status: 501 },
  );
}
