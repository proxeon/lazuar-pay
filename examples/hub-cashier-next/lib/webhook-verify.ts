/**
 * Hub outbound webhook signature verification.
 *
 * SSoT (C#): apps/lazuar-api/Modules/One/Infrastructure/Workers/OutboundWebhookSignature.cs
 * Algorithm: Standard Webhooks–style t=<unix>,v1=<hex> over `${t}.${rawBody}`.
 * HMAC-SHA256 key = full secret string as UTF-8 (keep `whsec_` prefix — do not strip).
 *
 * Node-only (node:crypto). Do not use on Edge runtime.
 */
import { createHmac, timingSafeEqual } from "node:crypto";

export type ParsedSignature = { t: number; v1: string };

/**
 * Parse Standard Webhooks–style header: t=<unix>,v1=<hex>
 * Matches OutboundWebhookSignature.TryParseHeader
 */
export function parseLazuarSignatureHeader(
  headerValue: string,
): ParsedSignature | null {
  let t: number | undefined;
  let v1: string | undefined;

  for (const part of headerValue.split(",")) {
    const trimmed = part.trim();
    const eq = trimmed.indexOf("=");
    if (eq <= 0) continue;
    const key = trimmed.slice(0, eq);
    const value = trimmed.slice(eq + 1);
    if (key.toLowerCase() === "t") {
      const n = Number(value);
      if (Number.isFinite(n)) t = n;
    } else if (key.toLowerCase() === "v1") {
      v1 = value;
    }
  }

  if (t === undefined || !v1) return null;
  return { t, v1 };
}

/**
 * Matches OutboundWebhookSignature.ComputeHeaderValue
 */
export function computeLazuarSignatureHeader(
  secret: string,
  body: string,
  unixTimestampSeconds: number,
): string {
  const signedPayload = `${unixTimestampSeconds}.${body}`;
  // Full secret including whsec_ prefix as UTF-8 key material
  const hex = createHmac("sha256", Buffer.from(secret, "utf8"))
    .update(signedPayload, "utf8")
    .digest("hex"); // lowercase
  return `t=${unixTimestampSeconds},v1=${hex}`;
}

function fixedTimeEqualHex(a: string, b: string): boolean {
  const left = Buffer.from(a.toLowerCase(), "utf8");
  const right = Buffer.from(b.toLowerCase(), "utf8");
  if (left.length !== right.length) return false;
  return timingSafeEqual(left, right);
}

/**
 * Matches OutboundWebhookSignature.TryVerify
 * @param toleranceSeconds default 300; pass 0 to skip skew check
 */
export function verifyLazuarSignature(
  secret: string,
  body: string,
  headerValue: string | null | undefined,
  options?: { toleranceSeconds?: number; nowUnixSeconds?: number },
): boolean {
  if (!secret || !headerValue) return false;

  const parsed = parseLazuarSignatureHeader(headerValue);
  if (!parsed) return false;

  const tolerance = options?.toleranceSeconds ?? 300;
  if (tolerance > 0) {
    const now = options?.nowUnixSeconds ?? Math.floor(Date.now() / 1000);
    if (Math.abs(now - parsed.t) > tolerance) return false;
  }

  const expectedHeader = computeLazuarSignatureHeader(secret, body, parsed.t);
  const expected = parseLazuarSignatureHeader(expectedHeader);
  if (!expected) return false;

  return fixedTimeEqualHex(parsed.v1, expected.v1);
}

/** Alias used by route handlers. */
export const verifySignature = verifyLazuarSignature;
