import createClient from "openapi-fetch";
import type { paths, components } from "@repo/api-types-ts";

const API_URL = import.meta.env.VITE_API_URL || "http://localhost:8080/api/v1";

export const client = createClient<paths>({ 
  baseUrl: API_URL,
  // This ensures HttpOnly cookies are attached to all cross-origin requests automatically
  fetch: (url, init) => fetch(url, { ...init, credentials: "include" })
});

// Type Aliases
export type AuthUser = components["schemas"]["One.AuthUser"];
