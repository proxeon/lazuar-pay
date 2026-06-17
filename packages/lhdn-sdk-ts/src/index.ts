import { ApiKeyAuthenticationProvider, ApiKeyLocation } from "@microsoft/kiota-abstractions";
import { FetchRequestAdapter } from "@microsoft/kiota-http-fetchlibrary";
import { v4 as uuidv4 } from "uuid";
import { LhdnClient } from "./generated/lhdnClient.js";

export * from "./generated/models/index.js";
export { LhdnClient };

export interface LhdnClientOptions {
  apiKey: string;
  baseUrl?: string;
}

export function createLhdnClient(options: LhdnClientOptions): LhdnClient {
  const authProvider = new ApiKeyAuthenticationProvider(
    options.apiKey,
    "Authorization",
    ApiKeyLocation.Header
  );

  const customFetch = async (input: RequestInfo | URL, init?: RequestInit) => {
    const request = new Request(input, init);
    
    // Automatically inject Idempotency-Key if performing a POST and the user forgot to provide it
    if (request.method.toUpperCase() === "POST" && !request.headers.has("Idempotency-Key")) {
      request.headers.set("Idempotency-Key", uuidv4());
    }
    
    return fetch(request);
  };

  // FetchRequestAdapter natively includes exponential backoff retry middleware
  const adapter = new FetchRequestAdapter(authProvider, undefined, undefined, customFetch);
  
  if (options.baseUrl) {
    adapter.baseUrl = options.baseUrl;
  }

  return new LhdnClient(adapter);
}
