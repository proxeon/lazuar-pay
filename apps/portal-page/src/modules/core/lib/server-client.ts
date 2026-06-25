// apps/portal-page/src/modules/core/lib/server-client.ts
import createClient from "openapi-fetch";
import type { paths } from "@repo/api-types-ts";
import { cookies } from "next/headers";

const SERVER_API_URL = process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || "http://localhost:8080/api/v1";

export const serverClient = createClient<paths>({ 
  baseUrl: SERVER_API_URL,
  fetch: async (url, init) => {
    const cookieStore = await cookies();
    const authCookie = cookieStore.get("lazuar_auth");
    
    const headers = new Headers(init?.headers);
    if (authCookie) {
      headers.set("Cookie", `${authCookie.name}=${authCookie.value}`);
    }
    
    return fetch(url, { ...init, headers });
  }
});
