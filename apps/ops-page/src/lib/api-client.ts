import createClient from "openapi-fetch";
import type { paths, components } from "@repo/api-types-ts";

export const API_URL = import.meta.env.VITE_API_URL || "http://localhost:8080/api/v1";

export const client = createClient<paths>({ 
  baseUrl: API_URL,
  fetch: (url, init) => fetch(url, { ...init, credentials: "include" })
});

export type ProposedActionDto = components["schemas"]["Ops.ProposedActionDto"];
export type ChatStreamChunkDto = components["schemas"]["Ops.ChatStreamChunkDto"];
