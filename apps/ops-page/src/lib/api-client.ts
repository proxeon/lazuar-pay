import createClient from "openapi-fetch";
import type { paths, components } from "@repo/api-types-ts";

export const API_URL = import.meta.env.VITE_API_URL || "http://localhost:8080/api/v1";

export const client = createClient<paths>({ 
  baseUrl: API_URL,
  fetch: (input, init) => fetch(input, { ...init, credentials: "include" })
});

client.use({
  onRequest({ request }) {
    const tenantId = localStorage.getItem("ops_active_workspace_id");

    // Always attach workspace context when selected. Required for platform
    // credentials under /one/api-keys (and harmless for other One routes).
    if (tenantId) {
      request.headers.set("X-Tenant-Id", tenantId);
    }

    return request;
  }
});

export type ProposedActionDto = components["schemas"]["Ops.ProposedActionDto"];
export type ChatStreamChunkDto = components["schemas"]["Ops.ChatStreamChunkDto"];
export type OpsConversationDto = components["schemas"]["Ops.OpsConversationDto"];
export type OpsMessageDto = components["schemas"]["Ops.OpsMessageDto"];
export type AuthUser = components["schemas"]["One.AuthUser"];
export type EntitlementDto = components["schemas"]["One.EntitlementDto"];
export type CommerceSubscriptionDto = components["schemas"]["Commerce.CommerceSubscriptionDto"];
