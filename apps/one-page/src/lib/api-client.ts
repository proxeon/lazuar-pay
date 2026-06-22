import createClient from "openapi-fetch";
import type { paths, components } from "@repo/api-types-ts";

export const API_URL = import.meta.env.VITE_API_URL || "http://localhost:8080/api/v1";
export const AUTH_URL = import.meta.env.VITE_AUTH_URL || "http://localhost:3001";
export const OPS_URL = import.meta.env.VITE_OPS_URL || "http://localhost:3003";

export const client = createClient<paths>({ 
  baseUrl: API_URL,
  fetch: (input, init) => fetch(input, { ...init, credentials: "include" })
});

export type AuthUser = components["schemas"]["One.AuthUser"];
export type EntitlementDto = components["schemas"]["One.EntitlementDto"];
export type MyGlobalSubscriptionDto = components["schemas"]["Community.MyGlobalSubscriptionDto"];
export type MyPendingInvitationDto = components["schemas"]["One.MyPendingInvitationDto"];
