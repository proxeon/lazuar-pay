/**
 * Server-side env helpers. Never import secrets into client components.
 */

function trimSlash(url: string): string {
  return url.replace(/\/$/, "");
}

/** Hub base including /api/v1. */
export function getHubBaseUrl(): string {
  const raw =
    process.env.LAZUAR_HUB_BASE_URL ??
    process.env.HUB_API_BASE_URL ??
    "http://localhost:8080/api/v1";
  return trimSlash(raw);
}

/** Machine API key (sk_test_… / sk_live_…). Server-only. */
export function getHubApiKey(): string | undefined {
  const key =
    process.env.LAZUAR_SK_TEST_KEY ??
    process.env.LAZUAR_API_KEY ??
    process.env.HUB_API_KEY;
  const trimmed = key?.trim();
  return trimmed || undefined;
}

/** Full whsec_… string — do not strip prefix. Server-only. */
export function getWebhookSecret(): string | undefined {
  const secret =
    process.env.LAZUAR_WEBHOOK_SECRET ?? process.env.HUB_WEBHOOK_SECRET;
  const trimmed = secret?.trim();
  return trimmed || undefined;
}

/**
 * Absolute public base of this sample (success/cancel URLs).
 * Prefer NEXT_PUBLIC_APP_URL; PUBLIC_APP_URL / APP_BASE_URL as aliases.
 */
export function getAppBaseUrl(): string {
  const raw =
    process.env.NEXT_PUBLIC_APP_URL ??
    process.env.PUBLIC_APP_URL ??
    process.env.APP_BASE_URL ??
    "http://127.0.0.1:3020";
  return trimSlash(raw);
}

export function getDefaultCurrency(): string {
  return (process.env.DEFAULT_CURRENCY ?? "MYR").trim().toUpperCase() || "MYR";
}
