import createClient from "openapi-fetch";
import type { paths, components } from "@repo/api-types-ts";

const CLIENT_API_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:8080/api/v1";

// Use this strictly inside Client Components ("use client")
export const browserClient = createClient<paths>({ 
  baseUrl: CLIENT_API_URL,
  // Client-side fetch wrapper: Automatically attaches browser HttpOnly cookies
  fetch: (url, init) => fetch(url, { ...init, credentials: "include" })
});

// Type Aliases
export type CommunityPlan = components["schemas"]["Community.CommunityPlanDto"];
export type CommunitySubscription = components["schemas"]["Community.CommunitySubscriptionDto"];
