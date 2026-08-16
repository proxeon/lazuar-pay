import createClient from "openapi-fetch";
import type { paths, components } from "@repo/api-types-ts";

const BROWSER_API_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:8080/api/v1";

export const browserClient = createClient<paths>({
  baseUrl: BROWSER_API_URL,
  fetch: (request) => fetch(new Request(request, { credentials: "include" })),
});

export type ProductDto = components["schemas"]["Commerce.ProductDto"];
export type PublicCheckoutRequestDto = components["schemas"]["Commerce.PublicCheckoutRequestDto"];
export type ValidateCouponResponseDto = components["schemas"]["Commerce.ValidateCouponResponseDto"];

export async function validateCouponCode(tenantSlug: string, productSlug: string, code: string) {
  const { data, error } = await browserClient.GET("/public/commerce/{tenantSlug}/validate-coupon", {
    params: {
      path: { tenantSlug },
      query: { code, product_slug: productSlug }
    }
  });

  if (error || !data) {
    throw new Error("Invalid promo code.");
  }

  if (!data.is_valid) {
    throw new Error(data.error_message || "This code cannot be applied.");
  }

  return data;
}

function checkoutIdempotencyKey(tenantSlug: string, productSlug: string) {
  const storageKey = `lazuar-checkout-idem:${tenantSlug}:${productSlug}`;
  try {
    const existing = sessionStorage.getItem(storageKey);
    if (existing) return existing;
    const next = crypto.randomUUID();
    sessionStorage.setItem(storageKey, next);
    return next;
  } catch {
    return crypto.randomUUID();
  }
}

export async function validateTin(tenantSlug: string, tin: string, idType: string, idValue: string) {
  const res = await fetch(`${BROWSER_API_URL}/public/commerce/${encodeURIComponent(tenantSlug)}/validate-tin`, {
    method: "POST",
    credentials: "include",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ tin, id_type: idType, id_value: idValue }),
  });
  const data = await res.json().catch(() => null);
  if (!res.ok) {
    throw new Error(data?.detail || "Merchant has not connected MyInvois.");
  }
  return data as { is_valid: boolean; tin: string; taxpayer_name?: string };
}

export async function submitCheckout(payload: PublicCheckoutRequestDto) {
  const { data, error } = await browserClient.POST("/public/commerce/checkout", {
    body: payload,
    headers: {
      "Idempotency-Key": checkoutIdempotencyKey(payload.tenant_slug, payload.product_slug),
    },
  });

  if (error || !data) {
    throw new Error(error?.detail || "Checkout submission failed.");
  }

  return data;
}

export async function getCheckoutStatus(tenantSlug: string, sessionId: string) {
  const { data, error } = await browserClient.GET(
    "/public/commerce/{tenantSlug}/checkout/{sessionId}/status",
    {
      params: {
        path: { tenantSlug, sessionId },
      },
    },
  );

  if (error || !data) {
    throw new Error("Status check failed.");
  }

  return data;
}
