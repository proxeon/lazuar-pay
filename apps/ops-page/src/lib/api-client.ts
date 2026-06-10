import createClient from "openapi-fetch";
import type { paths, components } from "@repo/api-types-ts";

export const API_URL = import.meta.env.VITE_API_URL || "http://localhost:8080/api/v1";

export const client = createClient<paths>({ 
  baseUrl: API_URL,
  // Pass credentials: "include" so HttpOnly cookies are attached to all requests.
  // By spreading init, we preserve any Request objects passed by openapi-fetch.
  fetch: (input, init) => fetch(input, { ...init, credentials: "include" })
});

// Official openapi-fetch middleware to safely inject headers
client.use({
  onRequest({ request }) {
    const tenantId = localStorage.getItem("ops_active_workspace_id");
    
    // Safely mutate the Request headers without breaking Content-Type
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
