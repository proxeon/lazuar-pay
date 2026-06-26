import createClient from "openapi-fetch";
import type { paths, components } from "@repo/api-types-ts";

export const API_URL = import.meta.env.VITE_API_URL || "http://localhost:8080/api/v1";

export const client = createClient<paths>({ 
  baseUrl: API_URL,
  fetch: (input, init) => fetch(input, { ...init, credentials: "include" })
});

export type AuthUser = components["schemas"]["One.AuthUser"];
