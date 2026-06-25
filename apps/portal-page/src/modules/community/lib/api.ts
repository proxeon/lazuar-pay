// apps/portal-page/src/modules/community/lib/api.ts
import createClient from "openapi-fetch";
import type { paths, components } from "@repo/api-types-ts";

const BROWSER_API_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:8080/api/v1";

export const browserClient = createClient<paths>({
  baseUrl: BROWSER_API_URL,
  fetch: (url, init) => fetch(url, { ...init, credentials: "include" })
});

export type CommunityPlanDto = components["schemas"]["Community.CommunityPlanDto"];
export type PublicCheckoutRequestDto = components["schemas"]["Community.PublicCheckoutRequestDto"];
export type ValidateCouponResponseDto = components["schemas"]["Community.ValidateCouponResponseDto"];

export async function validateCouponCode(tenantSlug: string, planSlug: string, code: string) {
  const { data, error } = await browserClient.GET("/public/community/{tenantSlug}/validate-coupon", {
    params: {
      path: { tenantSlug },
      query: { code, plan_slug: planSlug }
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

export async function submitCheckout(payload: PublicCheckoutRequestDto) {
  const { data, error } = await browserClient.POST("/public/community/checkout", {
    body: payload
  });

  if (error || !data) {
    throw new Error(error?.detail || "Checkout submission failed.");
  }

  return data;
}
