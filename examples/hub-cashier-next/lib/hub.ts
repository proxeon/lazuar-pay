/**
 * Server-only Hub M2M client (plain fetch — no gateway SDKs).
 * Do not import from client components.
 */
import { getHubApiKey, getHubBaseUrl } from "./env";

export class HubHttpError extends Error {
  constructor(
    public status: number,
    public code: string | undefined,
    public detail: string,
  ) {
    super(detail);
    this.name = "HubHttpError";
  }
}

export type CreateCheckoutInput = {
  amount: number;
  currency: string;
  description: string;
  customer_email: string;
  customer_name?: string;
  success_url: string;
  cancel_url: string;
  metadata: Record<string, string>;
  idempotency_key: string;
};

export type IntegrationCheckoutResponse = {
  checkout_id: string;
  checkout_url?: string | null;
  gateway: string;
  status: string;
  amount: number;
  currency: string;
  provider_session_id?: string | null;
  gateway_transaction_id?: string | null;
  expires_at?: string | null;
  metadata?: Record<string, string>;
};

function hubUrl(path: string): string {
  const base = getHubBaseUrl();
  return `${base}${path.startsWith("/") ? path : `/${path}`}`;
}

function parseProblem(text: string, status: number): HubHttpError {
  let code: string | undefined;
  let detail = text || `Hub request failed (${status})`;
  try {
    const pd = JSON.parse(text) as {
      title?: string;
      detail?: string;
      code?: string;
      status?: number;
    };
    code = pd.code ?? pd.title;
    detail = pd.detail ?? pd.title ?? detail;
  } catch {
    /* raw body */
  }
  return new HubHttpError(status, code, detail);
}

/**
 * POST {HUB}/integrations/payments/checkouts
 * Headers: Authorization Bearer sk_, Content-Type, Idempotency-Key
 * Body snake_case.
 */
export async function createCheckout(
  input: CreateCheckoutInput,
): Promise<IntegrationCheckoutResponse> {
  const key = getHubApiKey();
  if (!key) {
    throw new HubHttpError(
      500,
      "MISCONFIGURED",
      "LAZUAR_SK_TEST_KEY (or LAZUAR_API_KEY) is not set on the sample server.",
    );
  }

  const res = await fetch(hubUrl("/integrations/payments/checkouts"), {
    method: "POST",
    headers: {
      Authorization: `Bearer ${key}`,
      "Content-Type": "application/json",
      "Idempotency-Key": input.idempotency_key,
    },
    body: JSON.stringify({
      amount: input.amount,
      currency: input.currency,
      description: input.description,
      customer_email: input.customer_email,
      ...(input.customer_name ? { customer_name: input.customer_name } : {}),
      success_url: input.success_url,
      cancel_url: input.cancel_url,
      metadata: input.metadata,
    }),
    cache: "no-store",
  });

  const text = await res.text();
  if (!res.ok) {
    throw parseProblem(text, res.status);
  }

  return JSON.parse(text) as IntegrationCheckoutResponse;
}

/** Map Hub problem codes → preferred HTTP status for the sample browser. */
export function mapHubErrorToHttpStatus(err: HubHttpError): number {
  switch (err.code) {
    case "PAYMENTS_NOT_CONFIGURED":
      return 422;
    case "IDEMPOTENCY_CONFLICT":
      return 409;
    case "UNAUTHORIZED":
      return 401;
    case "FORBIDDEN":
      return 403;
    case "AMOUNT_INVALID":
    case "AMOUNT_BELOW_MINIMUM":
    case "CURRENCY_INVALID":
    case "URLS_REQUIRED":
    case "METADATA_INVALID":
    case "INVALID_REQUEST":
      return 400;
    case "MISCONFIGURED":
      return 500;
    case "GATEWAY_ERROR":
      return 502;
    default:
      return err.status >= 400 && err.status < 600 ? err.status : 502;
  }
}

/** Human-readable guidance for common Hub codes. */
export function hubErrorUserMessage(err: HubHttpError): string {
  switch (err.code) {
    case "PAYMENTS_NOT_CONFIGURED":
      return "No active payment gateway on this workspace. Configure BYOK in Hub Ops (Billplz/Stripe/etc.), then retry.";
    case "IDEMPOTENCY_CONFLICT":
      return "Idempotency conflict: order fields changed under the same key. Create a new order.";
    case "UNAUTHORIZED":
    case "FORBIDDEN":
      return "Hub rejected the API key or scope. Check LAZUAR_SK_TEST_KEY and payments.checkouts:write.";
    case "AMOUNT_INVALID":
    case "AMOUNT_BELOW_MINIMUM":
      return err.detail || "Amount is invalid or below the gateway minimum (MYR often ≥ 2.00).";
    case "CURRENCY_INVALID":
      return "Currency is invalid. Use a 3-letter code such as MYR.";
    case "URLS_REQUIRED":
      return "success_url / cancel_url must be absolute http(s). Check NEXT_PUBLIC_APP_URL.";
    case "GATEWAY_ERROR":
      return "Gateway error creating checkout. Retry later or check processor test keys.";
    case "MISCONFIGURED":
      return err.detail;
    default:
      return err.detail || "Checkout failed talking to Hub.";
  }
}
