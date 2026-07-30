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
export type PortalSubscriptionDto = components["schemas"]["Commerce.PortalSubscriptionDto"];

export async function validateCouponCode(tenantSlug: string, productSlug: string, code: string) {
  const { data, error } = await browserClient.GET("/public/commerce/{tenantSlug}/validate-coupon", {
    params: {
      path: { tenantSlug },
      query: { code, product_slug: productSlug },
    },
  });

  if (error || !data) {
    throw new Error("Invalid promo code.");
  }

  if (!data.is_valid) {
    throw new Error(data.error_message || "This code cannot be applied.");
  }

  return data;
}

export async function submitCheckout(payload: PublicCheckoutRequestDto) {
  const { data, error } = await browserClient.POST("/public/commerce/checkout", {
    body: payload,
  });

  if (error || !data) {
    throw new Error(error?.detail || "Checkout submission failed.");
  }

  return data;
}

export async function getCheckoutStatus(subId: string) {
  const { data, error } = await browserClient.GET("/public/commerce/checkout/{subId}/status", {
    params: {
      path: { subId },
    },
  });

  if (error || !data) {
    throw new Error("Status check failed.");
  }

  return data;
}
