import createClient from "openapi-fetch";
import type { paths, components } from "@repo/api-types-ts";

const SERVER_API_URL = process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || "http://localhost:8080/api/v1";
const CLIENT_API_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:8080/api/v1";

// Use this strictly inside Server Components (page.tsx, layout.tsx)
export const serverClient = createClient<paths>({ baseUrl: SERVER_API_URL });

// Use this strictly inside Client Components ("use client")
export const browserClient = createClient<paths>({ baseUrl: CLIENT_API_URL });

// Type Aliases
export type CommunityPlan = components["schemas"]["LazuarApi.Community.CommunityPlanDto"];
export type CommunitySubscription = components["schemas"]["LazuarApi.Community.CommunitySubscriptionDto"];
