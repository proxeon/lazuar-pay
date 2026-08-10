/**
 * Unit vectors for webhook signature (mirrors lib/webhook-verify.ts).
 * Run: node scripts/test-webhook-verify.mjs
 *
 * SSoT: apps/lazuar-api/Modules/One/Infrastructure/Workers/OutboundWebhookSignature.cs
 */
import assert from "node:assert/strict";
import { createHmac, timingSafeEqual } from "node:crypto";

function parseLazuarSignatureHeader(headerValue) {
  let t;
  let v1;
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

function computeLazuarSignatureHeader(secret, body, unixTimestampSeconds) {
  const signedPayload = `${unixTimestampSeconds}.${body}`;
  // Full secret including whsec_ prefix as UTF-8 key material
  const hex = createHmac("sha256", Buffer.from(secret, "utf8"))
    .update(signedPayload, "utf8")
    .digest("hex");
  return `t=${unixTimestampSeconds},v1=${hex}`;
}

function verifyLazuarSignature(secret, body, headerValue, options = {}) {
  if (!secret || !headerValue) return false;
  const parsed = parseLazuarSignatureHeader(headerValue);
  if (!parsed) return false;
  const tolerance = options.toleranceSeconds ?? 300;
  if (tolerance > 0) {
    const now = options.nowUnixSeconds ?? Math.floor(Date.now() / 1000);
    if (Math.abs(now - parsed.t) > tolerance) return false;
  }
  const expectedHeader = computeLazuarSignatureHeader(secret, body, parsed.t);
  const expected = parseLazuarSignatureHeader(expectedHeader);
  if (!expected) return false;
  const left = Buffer.from(parsed.v1.toLowerCase(), "utf8");
  const right = Buffer.from(expected.v1.toLowerCase(), "utf8");
  if (left.length !== right.length) return false;
  return timingSafeEqual(left, right);
}

// --- Fixed vectors ---
const secret = "whsec_test_secret";
const body =
  '{"id":"1","event_type":"payment.completed","created_at":"2026-01-01T00:00:00Z","data":{"event_id":"e1","checkout_id":"00000000-0000-0000-0000-000000000001","status":"completed","metadata":{"order_id":"ord_1"}}}';
const t = 1700000000;

const knownHeader = computeLazuarSignatureHeader(secret, body, t);
const knownV1 = parseLazuarSignatureHeader(knownHeader).v1;

const pyHex = createHmac("sha256", Buffer.from(secret, "utf8"))
  .update(`${t}.${body}`, "utf8")
  .digest("hex");
assert.equal(knownV1, pyHex, "header hex must match HMAC of t.body");

assert.equal(
  verifyLazuarSignature(secret, body, knownHeader, { nowUnixSeconds: t }),
  true,
  "valid signature accepts",
);

assert.equal(
  verifyLazuarSignature(secret, body + "x", knownHeader, { nowUnixSeconds: t }),
  false,
  "body one character change rejects",
);

assert.equal(
  verifyLazuarSignature("whsec_other", body, knownHeader, {
    nowUnixSeconds: t,
  }),
  false,
  "wrong secret rejects",
);

assert.equal(
  verifyLazuarSignature(secret, body, knownHeader, {
    toleranceSeconds: 30,
    nowUnixSeconds: t + 120,
  }),
  false,
  "stale t rejects",
);

assert.equal(
  verifyLazuarSignature(secret, body, `T=${t},V1=${knownV1}`, {
    nowUnixSeconds: t,
  }),
  true,
  "case-insensitive t/v1 keys",
);

// Full whsec_ prefix is key material (stripping would fail)
const stripped = secret.replace(/^whsec_/, "");
assert.equal(
  verifyLazuarSignature(stripped, body, knownHeader, { nowUnixSeconds: t }),
  false,
  "must keep whsec_ prefix in secret",
);

console.log("ok — webhook-verify vectors passed");
console.log("  secret: whsec_test_secret (full prefix as key)");
console.log("  t:", t);
console.log("  v1:", knownV1);
console.log("  header:", knownHeader);
