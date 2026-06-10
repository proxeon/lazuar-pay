import createClient from "openapi-fetch";
import type { paths, components } from "@repo/api-types-ts";

export const API_URL = import.meta.env.VITE_API_URL || "http://localhost:8080/api/v1";

export const client = createClient<paths>({ 
  baseUrl: API_URL,
  fetch: (url, init) => {
    const tenantId = localStorage.getItem("ops_active_workspace_id");
    const headers = new Headers(init?.headers);
    if (tenantId) headers.set("X-Tenant-Id", tenantId);
    
    return fetch(url, { ...init, headers, credentials: "include" });
  }
});

export type ProposedActionDto = components["schemas"]["Ops.ProposedActionDto"];
export type ChatStreamChunkDto = components["schemas"]["Ops.ChatStreamChunkDto"];
export type OpsConversationDto = components["schemas"]["Ops.OpsConversationDto"];
export type OpsMessageDto = components["schemas"]["Ops.OpsMessageDto"];
export type AuthUser = components["schemas"]["One.AuthUser"];
export type EntitlementDto = components["schemas"]["One.EntitlementDto"];
